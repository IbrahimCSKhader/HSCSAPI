using System.Reflection;
using System.Security.Claims;
using HSCSAPI.Controllers;
using HSCSAPI.Data;
using HSCSAPI.DTOs.Reminders;
using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Identity;
using HSCSAPI.Models.Profiles;
using HSCSAPI.Models.Relations;
using HSCSAPI.Models.Reminders;
using HSCSAPI.Services.Reminders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Tests;

public class ReminderServiceAndRouteTests
{
    [Fact]
    public async Task GetMyReminders_ReturnsDoctorVisibleActiveCounts()
    {
        using var context = new ReminderTestContext();
        var doctor = context.AddDoctor("Dr. Samer");
        var patient = context.AddPatient("Sarah Al-Hassan");
        var otherDoctor = context.AddDoctor("Dr. Other");
        var otherPatient = context.AddPatient("Other Patient");
        var now = DateTime.UtcNow;
        var upcoming = context.AddReminder(
            patient.Id,
            doctor.Id,
            "Morning clinic - Sarah Al-Hassan",
            "Follow-up visit scheduled for 10:00 AM in Room 3.",
            "Appointment",
            now.AddHours(1));
        var past = context.AddReminder(
            patient.Id,
            doctor.Id,
            "Review pending lab results",
            "James Mitchell's Lipid Panel is ready for review.",
            "Lab",
            now.AddHours(-2));
        context.AddReminder(
            patient.Id,
            doctor.Id,
            "Dismissed reminder",
            "Already handled.",
            "General",
            now.AddHours(2),
            dismissedAt: now);
        context.AddReminder(
            otherPatient.Id,
            otherDoctor.Id,
            "Foreign reminder",
            "Should not appear.",
            "General",
            now.AddHours(3));

        var response = await context.Service.GetMyRemindersAsync(
            status: "all",
            page: 1,
            pageSize: 20,
            ReminderTestContext.Principal(doctor.Id, UserSystemRole.Doctor));

        var list = OkValue(response);
        Assert.Equal(2, list.TotalCount);
        Assert.Equal(2, list.AllCount);
        Assert.Equal(1, list.UpcomingCount);
        Assert.Equal(1, list.PastCount);
        Assert.Equal(upcoming.ReminderId, list.Items[0].ReminderId);
        Assert.Equal(past.ReminderId, list.Items[1].ReminderId);
        Assert.All(list.Items, item => Assert.Null(item.DismissedAt));
        Assert.DoesNotContain(list.Items, item => item.Title == "Foreign reminder");
    }

    [Fact]
    public async Task GetMyReminders_FiltersUpcomingPastAndRejectsInvalidStatus()
    {
        using var context = new ReminderTestContext();
        var doctor = context.AddDoctor("Dr. Samer");
        var patient = context.AddPatient("Sarah");
        var now = DateTime.UtcNow;
        context.AddReminder(patient.Id, doctor.Id, "Upcoming", "Future visit.", "Appointment", now.AddDays(1));
        context.AddReminder(patient.Id, doctor.Id, "Past", "Old reminder.", "General", now.AddDays(-1));

        var upcomingResponse = await context.Service.GetMyRemindersAsync(
            "upcoming",
            page: 0,
            pageSize: 500,
            ReminderTestContext.Principal(patient.Id, UserSystemRole.Patient));
        var pastResponse = await context.Service.GetMyRemindersAsync(
            "past",
            page: 1,
            pageSize: 10,
            ReminderTestContext.Principal(patient.Id, UserSystemRole.Patient));
        var invalidResponse = await context.Service.GetMyRemindersAsync(
            "dismissed",
            page: 1,
            pageSize: 10,
            ReminderTestContext.Principal(patient.Id, UserSystemRole.Patient));
        var missingIdentity = await context.Service.GetMyRemindersAsync(
            null,
            page: 1,
            pageSize: 10,
            new ClaimsPrincipal(new ClaimsIdentity()));

        var upcoming = OkValue(upcomingResponse);
        var past = OkValue(pastResponse);
        Assert.Equal(1, upcoming.Page);
        Assert.Equal(100, upcoming.PageSize);
        Assert.Single(upcoming.Items);
        Assert.Equal("Upcoming", upcoming.Items[0].Status);
        Assert.Single(past.Items);
        Assert.Equal("Past", past.Items[0].Status);
        Assert.IsType<BadRequestObjectResult>(invalidResponse.Result);
        Assert.IsType<UnauthorizedObjectResult>(missingIdentity.Result);
    }

    [Fact]
    public async Task CreateReminder_CreatesForParticipantAndInfersCategory()
    {
        using var context = new ReminderTestContext();
        var doctor = context.AddDoctor("Dr. Samer");
        var patient = context.AddPatient("Sarah");
        var member = context.AddAuthorizedMember("Mona");
        context.LinkAuthorizedMember(patient.Id, member.Id);

        var response = await context.Service.CreateReminderAsync(
            new CreateReminderRequest
            {
                PatientId = patient.Id,
                DoctorId = doctor.Id,
                AuthorizedMemberId = member.Id,
                Title = "Review pending lab results",
                Message = "James Mitchell's Lipid Panel is ready for review.",
                ReminderAt = DateTime.UtcNow.AddHours(3)
            },
            ReminderTestContext.Principal(doctor.Id, UserSystemRole.Doctor));

        var created = Assert.IsType<CreatedAtActionResult>(response.Result);
        var reminder = Assert.IsType<ReminderResponse>(created.Value);
        Assert.Equal("Lab", reminder.Category);
        Assert.Equal(member.Id, reminder.AuthorizedMemberId);
        Assert.Single(context.DbContext.Reminders);
    }

    [Fact]
    public async Task CreateReminder_RejectsInvalidBodyForeignUserAndUnlinkedMember()
    {
        using var context = new ReminderTestContext();
        var doctor = context.AddDoctor("Dr. Samer");
        var patient = context.AddPatient("Sarah");
        var outsider = context.AddUser("Outsider");
        var unlinkedMember = context.AddAuthorizedMember("Unlinked");
        var validRequest = new CreateReminderRequest
        {
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            Title = "Morning clinic",
            Message = "Follow-up visit at 10:00 AM.",
            ReminderAt = DateTime.UtcNow.AddHours(2)
        };

        var missingTitle = await context.Service.CreateReminderAsync(
            new CreateReminderRequest
            {
                PatientId = validRequest.PatientId,
                DoctorId = validRequest.DoctorId,
                Title = " ",
                Message = validRequest.Message,
                ReminderAt = validRequest.ReminderAt
            },
            ReminderTestContext.Principal(doctor.Id, UserSystemRole.Doctor));
        var foreignUser = await context.Service.CreateReminderAsync(
            validRequest,
            ReminderTestContext.Principal(outsider.Id, UserSystemRole.Secretary));
        var unlinked = await context.Service.CreateReminderAsync(
            new CreateReminderRequest
            {
                PatientId = validRequest.PatientId,
                DoctorId = validRequest.DoctorId,
                AuthorizedMemberId = unlinkedMember.Id,
                Title = validRequest.Title,
                Message = validRequest.Message,
                ReminderAt = validRequest.ReminderAt
            },
            ReminderTestContext.Principal(doctor.Id, UserSystemRole.Doctor));

        Assert.IsType<BadRequestObjectResult>(missingTitle.Result);
        Assert.IsType<ForbidResult>(foreignUser.Result);
        Assert.IsType<BadRequestObjectResult>(unlinked.Result);
        Assert.Empty(context.DbContext.Reminders);
    }

    [Fact]
    public async Task GetReminder_ReturnsOwnReminderAndHidesForeignReminder()
    {
        using var context = new ReminderTestContext();
        var doctor = context.AddDoctor("Dr. Samer");
        var patient = context.AddPatient("Sarah");
        var outsider = context.AddDoctor("Dr. Other");
        var reminder = context.AddReminder(
            patient.Id,
            doctor.Id,
            "Unread messages",
            "You have 2 unread messages from Fatima Rahman.",
            "Message",
            DateTime.UtcNow.AddHours(1));

        var ownResponse = await context.Service.GetReminderAsync(
            reminder.ReminderId,
            ReminderTestContext.Principal(patient.Id, UserSystemRole.Patient));
        var foreignResponse = await context.Service.GetReminderAsync(
            reminder.ReminderId,
            ReminderTestContext.Principal(outsider.Id, UserSystemRole.Doctor));

        Assert.Equal(reminder.ReminderId, OkValue(ownResponse).ReminderId);
        Assert.IsType<NotFoundObjectResult>(foreignResponse.Result);
    }

    [Fact]
    public async Task UpdateReminder_UpdatesFieldsAndRejectsUnlinkedAuthorizedMember()
    {
        using var context = new ReminderTestContext();
        var doctor = context.AddDoctor("Dr. Samer");
        var patient = context.AddPatient("Sarah");
        var linkedMember = context.AddAuthorizedMember("Mona");
        var unlinkedMember = context.AddAuthorizedMember("Unlinked");
        context.LinkAuthorizedMember(patient.Id, linkedMember.Id);
        var reminder = context.AddReminder(
            patient.Id,
            doctor.Id,
            "Morning clinic",
            "Follow-up visit.",
            "Appointment",
            DateTime.UtcNow.AddHours(1));

        var updateResponse = await context.Service.UpdateReminderAsync(
            reminder.ReminderId,
            new UpdateReminderRequest
            {
                Title = "David Chen - imaging review",
                Message = "MRI results discussion scheduled for tomorrow at 2:30 PM.",
                Category = "Imaging",
                ReminderAt = DateTime.UtcNow.AddDays(1),
                AuthorizedMemberId = linkedMember.Id
            },
            ReminderTestContext.Principal(doctor.Id, UserSystemRole.Doctor));
        var unlinkedResponse = await context.Service.UpdateReminderAsync(
            reminder.ReminderId,
            new UpdateReminderRequest
            {
                Title = "Invalid member",
                Message = "Invalid member.",
                ReminderAt = DateTime.UtcNow.AddDays(2),
                AuthorizedMemberId = unlinkedMember.Id
            },
            ReminderTestContext.Principal(doctor.Id, UserSystemRole.Doctor));

        var updated = OkValue(updateResponse);
        Assert.Equal("David Chen - imaging review", updated.Title);
        Assert.Equal("Imaging", updated.Category);
        Assert.Equal(linkedMember.Id, updated.AuthorizedMemberId);
        Assert.IsType<BadRequestObjectResult>(unlinkedResponse.Result);
    }

    [Fact]
    public async Task DismissReminder_MarksReminderAndHidesItFromList()
    {
        using var context = new ReminderTestContext();
        var doctor = context.AddDoctor("Dr. Samer");
        var patient = context.AddPatient("Sarah");
        var reminder = context.AddReminder(
            patient.Id,
            doctor.Id,
            "Lab request follow-up",
            "Check status of pending CBC for Sarah Al-Hassan.",
            "Lab",
            DateTime.UtcNow.AddHours(1));

        var dismissResponse = await context.Service.DismissReminderAsync(
            reminder.ReminderId,
            ReminderTestContext.Principal(doctor.Id, UserSystemRole.Doctor));
        var listResponse = await context.Service.GetMyRemindersAsync(
            "all",
            1,
            20,
            ReminderTestContext.Principal(doctor.Id, UserSystemRole.Doctor));
        var secondDismissResponse = await context.Service.DismissReminderAsync(
            reminder.ReminderId,
            ReminderTestContext.Principal(doctor.Id, UserSystemRole.Doctor));

        Assert.NotNull(OkValue(dismissResponse).DismissedAt);
        Assert.Empty(OkValue(listResponse).Items);
        Assert.NotNull(OkValue(secondDismissResponse).DismissedAt);
    }

    [Fact]
    public async Task Preferences_GetDefaultsAndUpdatePersists()
    {
        using var context = new ReminderTestContext();
        var doctor = context.AddDoctor("Dr. Samer");

        var defaultsResponse = await context.Service.GetMyPreferencesAsync(
            ReminderTestContext.Principal(doctor.Id, UserSystemRole.Doctor));
        var updateResponse = await context.Service.UpdateMyPreferencesAsync(
            new UpdateReminderPreferencesRequest
            {
                AppointmentRemindersEnabled = true,
                LabResultRemindersEnabled = false,
                MessageRemindersEnabled = true,
                InAppNotificationsEnabled = true,
                EmailRemindersEnabled = true
            },
            ReminderTestContext.Principal(doctor.Id, UserSystemRole.Doctor));
        var storedResponse = await context.Service.GetMyPreferencesAsync(
            ReminderTestContext.Principal(doctor.Id, UserSystemRole.Doctor));

        var defaults = OkValue(defaultsResponse);
        Assert.True(defaults.AppointmentRemindersEnabled);
        Assert.True(defaults.LabResultRemindersEnabled);
        Assert.False(defaults.EmailRemindersEnabled);

        var updated = OkValue(updateResponse);
        Assert.False(updated.LabResultRemindersEnabled);
        Assert.True(updated.EmailRemindersEnabled);
        Assert.NotNull(updated.UpdatedAt);
        Assert.False(OkValue(storedResponse).LabResultRemindersEnabled);
    }

    [Fact]
    public async Task Preferences_RejectsMissingFlagsAndMissingIdentity()
    {
        using var context = new ReminderTestContext();
        var patient = context.AddPatient("Sarah");

        var missingFlag = await context.Service.UpdateMyPreferencesAsync(
            new UpdateReminderPreferencesRequest
            {
                AppointmentRemindersEnabled = true,
                LabResultRemindersEnabled = true,
                MessageRemindersEnabled = true,
                InAppNotificationsEnabled = true,
                EmailRemindersEnabled = null
            },
            ReminderTestContext.Principal(patient.Id, UserSystemRole.Patient));
        var missingIdentity = await context.Service.GetMyPreferencesAsync(new ClaimsPrincipal(new ClaimsIdentity()));

        Assert.IsType<BadRequestObjectResult>(missingFlag.Result);
        Assert.IsType<UnauthorizedObjectResult>(missingIdentity.Result);
    }

    [Fact]
    public void ReminderRoutes_AreCentralizedAndNotDuplicated()
    {
        var endpoints = GetControllerEndpoints().ToList();
        var duplicateEndpoints = endpoints
            .GroupBy(endpoint => $"{endpoint.HttpMethod} {endpoint.Template}", StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(endpoint => endpoint.ActionName))}")
            .ToList();
        var reminderEndpoints = endpoints
            .Where(endpoint => endpoint.ControllerName == nameof(RemindersController))
            .Select(endpoint => $"{endpoint.HttpMethod} {endpoint.Template}")
            .OrderBy(endpoint => endpoint)
            .ToList();
        var duplicatedReminderSurfaces = endpoints
            .Where(endpoint => endpoint.ControllerName != nameof(RemindersController)
                && endpoint.Template.Contains("reminders", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(duplicateEndpoints);
        Assert.Empty(duplicatedReminderSurfaces);
        Assert.Equal(
            [
                "GET /api/reminders",
                "GET /api/reminders/{reminderid:guid}",
                "GET /api/reminders/preferences",
                "PATCH /api/reminders/{reminderid:guid}/dismiss",
                "POST /api/reminders",
                "PUT /api/reminders/{reminderid:guid}",
                "PUT /api/reminders/preferences"
            ],
            reminderEndpoints);
    }

    private static T OkValue<T>(ActionResult<T> response)
    {
        var ok = Assert.IsType<OkObjectResult>(response.Result);
        return Assert.IsType<T>(ok.Value);
    }

    private static IEnumerable<ControllerEndpoint> GetControllerEndpoints()
    {
        var controllerTypes = typeof(RemindersController).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type));

        foreach (var controllerType in controllerTypes)
        {
            var controllerRoute = controllerType
                .GetCustomAttributes<RouteAttribute>(inherit: true)
                .FirstOrDefault()
                ?.Template ?? string.Empty;
            var controllerName = controllerType.Name.EndsWith("Controller", StringComparison.Ordinal)
                ? controllerType.Name[..^"Controller".Length]
                : controllerType.Name;
            controllerRoute = controllerRoute.Replace("[controller]", controllerName, StringComparison.OrdinalIgnoreCase);

            foreach (var method in controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                foreach (var httpAttribute in method.GetCustomAttributes<HttpMethodAttribute>(inherit: true))
                {
                    var template = NormalizeRoute(CombineRouteTemplates(controllerRoute, httpAttribute.Template));
                    foreach (var httpMethod in httpAttribute.HttpMethods)
                    {
                        yield return new ControllerEndpoint(
                            controllerType.Name,
                            method.Name,
                            httpMethod.ToUpperInvariant(),
                            template);
                    }
                }
            }
        }
    }

    private static string CombineRouteTemplates(string controllerRoute, string? actionRoute)
    {
        if (string.IsNullOrWhiteSpace(actionRoute))
        {
            return controllerRoute;
        }

        if (actionRoute.StartsWith("/", StringComparison.Ordinal))
        {
            return actionRoute;
        }

        return $"{controllerRoute.TrimEnd('/')}/{actionRoute.TrimStart('/')}";
    }

    private static string NormalizeRoute(string route)
    {
        return "/" + route.Trim('/').ToLowerInvariant();
    }

    private sealed record ControllerEndpoint(
        string ControllerName,
        string ActionName,
        string HttpMethod,
        string Template);
}

internal sealed class ReminderTestContext : IDisposable
{
    public ReminderTestContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        DbContext = new AppDbContext(options);
        DbContext.Database.EnsureCreated();
        Service = new RemindersService(DbContext);
    }

    public AppDbContext DbContext { get; }
    public RemindersService Service { get; }

    public User AddUser(string name)
    {
        var id = Guid.NewGuid();
        var user = new User
        {
            Id = id,
            Name = name,
            UserName = $"{id:N}@test.local",
            NormalizedUserName = $"{id:N}@TEST.LOCAL",
            Email = $"{id:N}@test.local",
            NormalizedEmail = $"{id:N}@TEST.LOCAL",
            EmailConfirmed = true,
            RegisteredAt = DateTime.UtcNow
        };

        DbContext.Users.Add(user);
        DbContext.SaveChanges();
        return user;
    }

    public User AddDoctor(string name)
    {
        var user = AddUser(name);
        DbContext.Doctors.Add(new Doctor
        {
            DoctorId = user.Id,
            ProfessionalLicenseNumber = $"DOC-{Guid.NewGuid():N}"
        });
        DbContext.SaveChanges();
        return user;
    }

    public User AddPatient(string name)
    {
        var user = AddUser(name);
        DbContext.Patients.Add(new Patient
        {
            PatientId = user.Id,
            UserID = $"PAT-{Guid.NewGuid():N}"[..12],
            Gender = Gender.Female,
            BloodType = BloodType.OPositive
        });
        DbContext.SaveChanges();
        return user;
    }

    public User AddAuthorizedMember(string name)
    {
        var user = AddUser(name);
        DbContext.AuthorizedMembers.Add(new AuthorizedMember
        {
            AuthorizedMemberId = user.Id
        });
        DbContext.SaveChanges();
        return user;
    }

    public void LinkAuthorizedMember(Guid patientId, Guid authorizedMemberId)
    {
        DbContext.PatientAuthorizedMembers.Add(new PatientAuthorizedMember
        {
            PatientId = patientId,
            AuthorizedMemberId = authorizedMemberId,
            RelationshipType = RelationshipType.Other,
            AuthorizedAt = DateTime.UtcNow
        });
        DbContext.SaveChanges();
    }

    public Reminder AddReminder(
        Guid patientId,
        Guid doctorId,
        string title,
        string message,
        string category,
        DateTime reminderAt,
        Guid? authorizedMemberId = null,
        DateTime? dismissedAt = null)
    {
        var reminder = new Reminder
        {
            PatientId = patientId,
            DoctorId = doctorId,
            AuthorizedMemberId = authorizedMemberId,
            Title = title,
            ReminderText = message,
            Category = category,
            ReminderAt = reminderAt,
            CreatedAt = DateTime.UtcNow,
            DismissedAt = dismissedAt
        };

        DbContext.Reminders.Add(reminder);
        DbContext.SaveChanges();
        return reminder;
    }

    public static ClaimsPrincipal Principal(Guid userId, UserSystemRole role)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role.ToString())
            ],
            "Test"));
    }

    public void Dispose()
    {
        DbContext.Dispose();
    }
}
