using System.Reflection;
using System.Security.Claims;
using HSCSAPI.Controllers;
using HSCSAPI.Data;
using HSCSAPI.DTOs.Notifications;
using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Identity;
using HSCSAPI.Models.Notifications;
using HSCSAPI.Services.Notifications;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Tests;

public class NotificationServiceAndRouteTests
{
    [Fact]
    public async Task GetMyNotifications_ReturnsCurrentDoctorNotificationsWithReadCounts()
    {
        using var context = new NotificationTestContext();
        var doctor = context.AddUser("Dr. Samer");
        var patient = context.AddUser("Sarah");
        var olderUnread = context.AddNotification(
            doctor.Id,
            "Appointment in 1 hour",
            "Sarah Al-Hassan follow-up visit at 10:00 AM.",
            isRead: false,
            createdAt: DateTime.UtcNow.AddHours(-2));
        var read = context.AddNotification(
            doctor.Id,
            "Lab result available",
            "James Mitchell's Lipid Panel results are ready for review.",
            isRead: true,
            createdAt: DateTime.UtcNow.AddHours(-1));
        var newerUnread = context.AddNotification(
            doctor.Id,
            "Imaging result available",
            "Layla Khoury's Chest X-Ray results are ready for review.",
            isRead: false,
            createdAt: DateTime.UtcNow);
        context.AddNotification(
            patient.Id,
            "Patient-only notification",
            "This should never appear in the doctor's inbox.",
            isRead: false,
            createdAt: DateTime.UtcNow.AddMinutes(1));

        var response = await context.Service.GetMyNotificationsAsync(
            status: null,
            page: 1,
            pageSize: 10,
            NotificationTestContext.Principal(doctor.Id, UserSystemRole.Doctor));

        var inbox = OkValue(response);
        Assert.Equal(3, inbox.TotalCount);
        Assert.Equal(2, inbox.UnreadCount);
        Assert.Equal(1, inbox.ReadCount);
        Assert.Equal(newerUnread.NotificationId, inbox.Items[0].NotificationId);
        Assert.Equal(olderUnread.NotificationId, inbox.Items[1].NotificationId);
        Assert.Equal(read.NotificationId, inbox.Items[2].NotificationId);
        Assert.Equal("Imaging", inbox.Items[0].Category);
        Assert.DoesNotContain(inbox.Items, item => item.Title == "Patient-only notification");
    }

    [Fact]
    public async Task GetMyNotifications_FiltersPatientUnreadAndNormalizesPaging()
    {
        using var context = new NotificationTestContext();
        var patient = context.AddUser("Sarah");
        context.AddNotification(patient.Id, "Unread appointment", null, isRead: false, createdAt: DateTime.UtcNow);
        context.AddNotification(patient.Id, "Unread message", null, isRead: false, createdAt: DateTime.UtcNow.AddMinutes(-1));
        context.AddNotification(patient.Id, "Read system note", null, isRead: true, createdAt: DateTime.UtcNow.AddMinutes(-2));

        var response = await context.Service.GetMyNotificationsAsync(
            status: "unread",
            page: 0,
            pageSize: 500,
            NotificationTestContext.Principal(patient.Id, UserSystemRole.Patient));

        var inbox = OkValue(response);
        Assert.Equal(1, inbox.Page);
        Assert.Equal(100, inbox.PageSize);
        Assert.Equal(2, inbox.TotalCount);
        Assert.Equal(2, inbox.UnreadCount);
        Assert.Equal(1, inbox.ReadCount);
        Assert.All(inbox.Items, item => Assert.False(item.IsRead));
    }

    [Fact]
    public async Task GetMyNotifications_RejectsInvalidStatusAndMissingIdentity()
    {
        using var context = new NotificationTestContext();
        var user = context.AddUser("Sarah");

        var invalidStatus = await context.Service.GetMyNotificationsAsync(
            status: "archived",
            page: 1,
            pageSize: 20,
            NotificationTestContext.Principal(user.Id, UserSystemRole.Patient));
        var missingIdentity = await context.Service.GetMyNotificationsAsync(
            status: null,
            page: 1,
            pageSize: 20,
            new ClaimsPrincipal(new ClaimsIdentity()));

        Assert.IsType<BadRequestObjectResult>(invalidStatus.Result);
        Assert.IsType<UnauthorizedObjectResult>(missingIdentity.Result);
    }

    [Fact]
    public async Task UpdateReadStatus_CanMarkNotificationReadAndUnread()
    {
        using var context = new NotificationTestContext();
        var patient = context.AddUser("Sarah");
        var notification = context.AddNotification(
            patient.Id,
            "New patient message",
            "Fatima Rahman sent a message about her lab request.",
            isRead: false,
            createdAt: DateTime.UtcNow);

        var readResponse = await context.Service.UpdateReadStatusAsync(
            notification.NotificationId,
            new UpdateNotificationReadStatusRequest { IsRead = true },
            NotificationTestContext.Principal(patient.Id, UserSystemRole.Patient));
        var unreadResponse = await context.Service.UpdateReadStatusAsync(
            notification.NotificationId,
            new UpdateNotificationReadStatusRequest { IsRead = false },
            NotificationTestContext.Principal(patient.Id, UserSystemRole.Patient));

        Assert.True(OkValue(readResponse).IsRead);
        Assert.False(OkValue(unreadResponse).IsRead);
        Assert.False(context.DbContext.Notifications.Single().IsRead);
    }

    [Fact]
    public async Task UpdateReadStatus_RejectsMissingFlagAndForeignNotification()
    {
        using var context = new NotificationTestContext();
        var owner = context.AddUser("Owner");
        var outsider = context.AddUser("Outsider");
        var notification = context.AddNotification(
            owner.Id,
            "System maintenance",
            "Scheduled maintenance tonight.",
            isRead: false,
            createdAt: DateTime.UtcNow);

        var missingFlag = await context.Service.UpdateReadStatusAsync(
            notification.NotificationId,
            new UpdateNotificationReadStatusRequest(),
            NotificationTestContext.Principal(owner.Id, UserSystemRole.Patient));
        var foreignNotification = await context.Service.UpdateReadStatusAsync(
            notification.NotificationId,
            new UpdateNotificationReadStatusRequest { IsRead = true },
            NotificationTestContext.Principal(outsider.Id, UserSystemRole.Doctor));

        Assert.IsType<BadRequestObjectResult>(missingFlag.Result);
        Assert.IsType<NotFoundObjectResult>(foreignNotification.Result);
        Assert.False(context.DbContext.Notifications.Single().IsRead);
    }

    [Fact]
    public async Task MarkAllAsRead_UpdatesOnlyCurrentUsersUnreadNotifications()
    {
        using var context = new NotificationTestContext();
        var patient = context.AddUser("Sarah");
        var doctor = context.AddUser("Dr. Samer");
        context.AddNotification(patient.Id, "Unread one", null, isRead: false, createdAt: DateTime.UtcNow);
        context.AddNotification(patient.Id, "Unread two", null, isRead: false, createdAt: DateTime.UtcNow.AddMinutes(-1));
        context.AddNotification(patient.Id, "Already read", null, isRead: true, createdAt: DateTime.UtcNow.AddMinutes(-2));
        context.AddNotification(doctor.Id, "Doctor unread", null, isRead: false, createdAt: DateTime.UtcNow.AddMinutes(-3));

        var response = await context.Service.MarkAllAsReadAsync(
            NotificationTestContext.Principal(patient.Id, UserSystemRole.Patient));

        var result = OkValue(response);
        Assert.Equal(2, result.UpdatedNotifications);
        Assert.All(
            context.DbContext.Notifications.Where(notification => notification.UserId == patient.Id),
            notification => Assert.True(notification.IsRead));
        Assert.False(context.DbContext.Notifications.Single(notification => notification.UserId == doctor.Id).IsRead);
    }

    [Fact]
    public void NotificationRoutes_AreCentralizedAndNotDuplicated()
    {
        var endpoints = GetControllerEndpoints().ToList();
        var duplicateEndpoints = endpoints
            .GroupBy(endpoint => $"{endpoint.HttpMethod} {endpoint.Template}", StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(endpoint => endpoint.ActionName))}")
            .ToList();
        var notificationEndpoints = endpoints
            .Where(endpoint => endpoint.ControllerName == nameof(NotificationsController))
            .Select(endpoint => $"{endpoint.HttpMethod} {endpoint.Template}")
            .OrderBy(endpoint => endpoint)
            .ToList();
        var patientProfileNotificationEndpoints = endpoints
            .Where(endpoint => endpoint.ControllerName == nameof(PatientProfileController)
                && endpoint.Template.Contains("notifications", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(duplicateEndpoints);
        Assert.Empty(patientProfileNotificationEndpoints);
        Assert.Equal(
            [
                "DELETE /api/notifications/{notificationid:guid}",
                "GET /api/notifications",
                "PATCH /api/notifications/{notificationid:guid}/read-status",
                "PATCH /api/notifications/read-all"
            ],
            notificationEndpoints);
    }

    private static T OkValue<T>(ActionResult<T> response)
    {
        var ok = Assert.IsType<OkObjectResult>(response.Result);
        return Assert.IsType<T>(ok.Value);
    }

    private static IEnumerable<ControllerEndpoint> GetControllerEndpoints()
    {
        var controllerTypes = typeof(NotificationsController).Assembly
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

internal sealed class NotificationTestContext : IDisposable
{
    public NotificationTestContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        DbContext = new AppDbContext(options);
        DbContext.Database.EnsureCreated();
        Service = new NotificationsService(DbContext);
    }

    public AppDbContext DbContext { get; }
    public NotificationsService Service { get; }

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

    public Notification AddNotification(
        Guid userId,
        string title,
        string? message,
        bool isRead,
        DateTime createdAt)
    {
        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            IsRead = isRead,
            CreatedAt = createdAt
        };

        DbContext.Notifications.Add(notification);
        DbContext.SaveChanges();
        return notification;
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
