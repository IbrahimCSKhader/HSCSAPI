using System.Reflection;
using System.Security.Claims;
using HSCSAPI.Controllers;
using HSCSAPI.Data;
using HSCSAPI.DTOs.AuthorizedMember;
using HSCSAPI.Models.Appointments;
using HSCSAPI.Models.Clinics;
using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Identity;
using HSCSAPI.Models.Laboratory;
using HSCSAPI.Models.MedicalFiles;
using HSCSAPI.Models.Notifications;
using HSCSAPI.Models.Profiles;
using HSCSAPI.Models.Radiology;
using HSCSAPI.Models.Relations;
using HSCSAPI.Services.AuthorizedMembers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Tests;

public class AuthorizedMembersServiceAndRouteTests
{
    [Fact]
    public async Task GetDashboard_ReturnsLinkedPatientRecordNotificationAndAppointmentSummary()
    {
        using var context = new AuthorizedMemberPortalTestContext();
        var clinic = context.AddClinic("General Medicine");
        var member = context.AddAuthorizedMember("Roaa Hamoudah");
        var doctor = context.AddDoctor("Dr. Rami Nasser", clinic.ClinicId);
        var layla = context.AddPatient("Layla Mansour", "P-11002", clinic.ClinicId);
        var ahmad = context.AddPatient("Ahmad Darwish", "P-06543", clinic.ClinicId);
        var outsider = context.AddPatient("Outside Patient", "P-99999", clinic.ClinicId);
        context.LinkPatient(member.Id, layla.Id, RelationshipType.Guardian);
        context.LinkPatient(member.Id, ahmad.Id, RelationshipType.Brother);

        var today = DateOnly.FromDateTime(DateTime.Now);
        var linkedUpcoming = context.AddAppointment(
            doctor.Id,
            layla.Id,
            today.AddDays(2),
            new TimeOnly(9, 0));
        var linkedPast = context.AddAppointment(
            doctor.Id,
            ahmad.Id,
            today.AddDays(-1),
            new TimeOnly(11, 0));
        var outsiderAppointment = context.AddAppointment(
            doctor.Id,
            outsider.Id,
            today.AddDays(3),
            new TimeOnly(14, 0));
        context.AddMedicalRecord(linkedUpcoming, doctor.Id, "cbc-layla.pdf", SeverityLevel.Low, labTestName: "Complete Blood Count");
        context.AddMedicalRecord(linkedPast, doctor.Id, "ct-ahmad.pdf", SeverityLevel.Low, imagingTestName: "Chest CT with contrast");
        context.AddMedicalRecord(outsiderAppointment, doctor.Id, "foreign.pdf", SeverityLevel.Low);
        context.AddNotification(member.Id, "Unread one", isRead: false);
        context.AddNotification(member.Id, "Unread two", isRead: false);
        context.AddNotification(member.Id, "Read one", isRead: true);
        context.AddNotification(outsider.Id, "Foreign unread", isRead: false);

        var response = await context.Service.GetDashboardAsync(
            AuthorizedMemberPortalTestContext.Principal(member.Id));

        var dashboard = OkValue(response);
        Assert.Equal(member.Id, dashboard.AuthorizedMemberId);
        Assert.Equal(2, dashboard.LinkedPatientsCount);
        Assert.Equal(2, dashboard.MedicalRecordsCount);
        Assert.Equal(2, dashboard.UnreadNotificationsCount);
        Assert.Equal(1, dashboard.UpcomingAppointmentsCount);
        Assert.Equal(2, dashboard.LinkedPatients.Count);
        Assert.Single(dashboard.UpcomingAppointments);
        Assert.Equal(layla.Id, dashboard.UpcomingAppointments[0].PatientId);
        Assert.DoesNotContain(dashboard.LinkedPatients, patient => patient.PatientId == outsider.Id);
    }

    [Fact]
    public async Task GetMyProfileAndPatients_ReturnsAuthorizedMemberLinkedPatientDetails()
    {
        using var context = new AuthorizedMemberPortalTestContext();
        var clinic = context.AddClinic("General Medicine");
        var member = context.AddAuthorizedMember(
            "Roaa Hamoudah",
            phoneNumber: "0599000000",
            address: "Nablus",
            dateOfBirth: new DateOnly(1995, 7, 14));
        var doctor = context.AddDoctor("Dr. Lina Faris", clinic.ClinicId);
        var patient = context.AddPatient(
            "Layla Mansour",
            "P-11002",
            clinic.ClinicId,
            Gender.Female,
            BloodType.APositive);
        context.LinkPatient(member.Id, patient.Id, RelationshipType.Mother);
        var appointment = context.AddAppointment(
            doctor.Id,
            patient.Id,
            DateOnly.FromDateTime(DateTime.Now).AddDays(1),
            new TimeOnly(10, 30));
        context.AddMedicalRecord(appointment, doctor.Id, "lipid-panel.pdf", SeverityLevel.Low, labTestName: "Lipid panel");

        var profileResponse = await context.Service.GetMyProfileAsync(
            AuthorizedMemberPortalTestContext.Principal(member.Id));
        var patientsResponse = await context.Service.GetMyPatientsAsync(
            AuthorizedMemberPortalTestContext.Principal(member.Id));
        var patientResponse = await context.Service.GetMyPatientAsync(
            patient.Id,
            AuthorizedMemberPortalTestContext.Principal(member.Id));

        var profile = OkValue(profileResponse);
        var patients = OkValue(patientsResponse);
        var linkedPatient = OkValue(patientResponse);
        Assert.Equal("Roaa Hamoudah", profile.Name);
        Assert.Equal("0599000000", profile.PhoneNumber);
        Assert.Single(patients);
        Assert.Equal("Layla Mansour", linkedPatient.Name);
        Assert.Equal("P-11002", linkedPatient.PatientUserId);
        Assert.Equal("Female", linkedPatient.Gender);
        Assert.Equal("A+", linkedPatient.BloodType);
        Assert.Equal(1, linkedPatient.MedicalRecordsCount);
        Assert.Equal(1, linkedPatient.UpcomingAppointmentsCount);
        Assert.True(linkedPatient.CanViewRecords);
        Assert.True(linkedPatient.CanViewAppointments);
    }

    [Fact]
    public async Task GetMyAppointments_FiltersLinkedPatientsDateRangeAndUpcomingFlag()
    {
        using var context = new AuthorizedMemberPortalTestContext();
        var clinic = context.AddClinic("General Medicine");
        var member = context.AddAuthorizedMember("Roaa Hamoudah");
        var doctor = context.AddDoctor("Dr. Rami Nasser", clinic.ClinicId);
        var layla = context.AddPatient("Layla Mansour", "P-11002", clinic.ClinicId);
        var ahmad = context.AddPatient("Ahmad Darwish", "P-06543", clinic.ClinicId);
        var outsider = context.AddPatient("Outside Patient", "P-99999", clinic.ClinicId);
        context.LinkPatient(member.Id, layla.Id, RelationshipType.Guardian);
        context.LinkPatient(member.Id, ahmad.Id, RelationshipType.Brother);
        var today = DateOnly.FromDateTime(DateTime.Now);
        var laylaUpcoming = context.AddAppointment(doctor.Id, layla.Id, today.AddDays(1), new TimeOnly(9, 0));
        context.AddAppointment(doctor.Id, ahmad.Id, today.AddDays(3), new TimeOnly(15, 30));
        context.AddAppointment(doctor.Id, layla.Id, today.AddDays(-2), new TimeOnly(8, 0));
        context.AddAppointment(doctor.Id, outsider.Id, today.AddDays(1), new TimeOnly(12, 0));

        var allLinked = await context.Service.GetMyAppointmentsAsync(
            patientId: null,
            fromDate: null,
            toDate: null,
            upcomingOnly: false,
            AuthorizedMemberPortalTestContext.Principal(member.Id));
        var laylaUpcomingOnly = await context.Service.GetMyAppointmentsAsync(
            layla.Id,
            fromDate: null,
            toDate: null,
            upcomingOnly: true,
            AuthorizedMemberPortalTestContext.Principal(member.Id));
        var invalidRange = await context.Service.GetMyAppointmentsAsync(
            patientId: null,
            fromDate: today.AddDays(2),
            toDate: today,
            upcomingOnly: false,
            AuthorizedMemberPortalTestContext.Principal(member.Id));
        var unlinkedPatient = await context.Service.GetMyAppointmentsAsync(
            outsider.Id,
            fromDate: null,
            toDate: null,
            upcomingOnly: false,
            AuthorizedMemberPortalTestContext.Principal(member.Id));

        Assert.Equal(3, OkValue(allLinked).TotalCount);
        var filtered = OkValue(laylaUpcomingOnly);
        Assert.Single(filtered.Items);
        Assert.Equal(laylaUpcoming.AppointmentId, filtered.Items[0].AppointmentId);
        Assert.Equal("Scheduled", filtered.Items[0].Status);
        Assert.IsType<BadRequestObjectResult>(invalidRange.Result);
        Assert.IsType<NotFoundObjectResult>(unlinkedPatient.Result);
    }

    [Fact]
    public async Task GetPatientMedicalRecords_ReturnsTypeCountsFiltersAndSearch()
    {
        using var context = new AuthorizedMemberPortalTestContext();
        var clinic = context.AddClinic("General Medicine");
        var member = context.AddAuthorizedMember("Roaa Hamoudah");
        var doctor = context.AddDoctor("Dr. Rami Nasser", clinic.ClinicId);
        var patient = context.AddPatient("Layla Mansour", "P-11002", clinic.ClinicId);
        context.LinkPatient(member.Id, patient.Id, RelationshipType.Guardian);
        var appointment = context.AddAppointment(
            doctor.Id,
            patient.Id,
            DateOnly.FromDateTime(DateTime.Now).AddDays(-1),
            new TimeOnly(13, 0));
        context.AddMedicalRecord(appointment, doctor.Id, "cbc-layla.pdf", SeverityLevel.Low, labTestName: "Complete Blood Count");
        context.AddMedicalRecord(appointment, doctor.Id, "chest-ct.pdf", SeverityLevel.Low, imagingTestName: "Chest CT with contrast");
        context.AddMedicalRecord(appointment, doctor.Id, "metformin-500-mg.pdf", SeverityLevel.Low);

        var allResponse = await context.Service.GetPatientMedicalRecordsAsync(
            patient.Id,
            type: null,
            query: null,
            page: 0,
            pageSize: 500,
            AuthorizedMemberPortalTestContext.Principal(member.Id));
        var labResponse = await context.Service.GetPatientMedicalRecordsAsync(
            patient.Id,
            type: "lab-results",
            query: null,
            page: 1,
            pageSize: 20,
            AuthorizedMemberPortalTestContext.Principal(member.Id));
        var searchResponse = await context.Service.GetPatientMedicalRecordsAsync(
            patient.Id,
            type: "prescriptions",
            query: "metformin",
            page: 1,
            pageSize: 20,
            AuthorizedMemberPortalTestContext.Principal(member.Id));
        var invalidType = await context.Service.GetPatientMedicalRecordsAsync(
            patient.Id,
            type: "unknown",
            query: null,
            page: 1,
            pageSize: 20,
            AuthorizedMemberPortalTestContext.Principal(member.Id));

        var all = OkValue(allResponse);
        Assert.Equal(1, all.Page);
        Assert.Equal(100, all.PageSize);
        Assert.Equal(3, all.TotalCount);
        Assert.Equal(3, all.TypeCounts.All);
        Assert.Equal(1, all.TypeCounts.LabResults);
        Assert.Equal(1, all.TypeCounts.Imaging);
        Assert.Equal(1, all.TypeCounts.Prescriptions);
        Assert.Single(OkValue(labResponse).Items);
        Assert.Equal("LabResult", OkValue(labResponse).Items[0].RecordType);
        var searched = OkValue(searchResponse);
        Assert.Single(searched.Items);
        Assert.Equal("Prescription", searched.Items[0].RecordType);
        Assert.Equal("metformin-500-mg", searched.Items[0].Title);
        Assert.IsType<BadRequestObjectResult>(invalidType.Result);
    }

    [Fact]
    public async Task GetPatientMedicalRecordAndDownload_ProtectLinkedPatientFiles()
    {
        using var context = new AuthorizedMemberPortalTestContext();
        var clinic = context.AddClinic("General Medicine");
        var member = context.AddAuthorizedMember("Roaa Hamoudah");
        var doctor = context.AddDoctor("Dr. Rami Nasser", clinic.ClinicId);
        var patient = context.AddPatient("Layla Mansour", "P-11002", clinic.ClinicId);
        var outsider = context.AddPatient("Outside Patient", "P-99999", clinic.ClinicId);
        context.LinkPatient(member.Id, patient.Id, RelationshipType.Guardian);
        var appointment = context.AddAppointment(
            doctor.Id,
            patient.Id,
            DateOnly.FromDateTime(DateTime.Now).AddDays(-1),
            new TimeOnly(13, 0),
            notes: "Follow-up notes");
        var lowRecord = context.AddMedicalRecord(
            appointment,
            doctor.Id,
            "cbc-layla.pdf",
            SeverityLevel.Low,
            labTestName: "Complete Blood Count",
            clinicalNotes: "Hemoglobin mildly low.");
        var highRecord = context.AddMedicalRecord(
            appointment,
            doctor.Id,
            "high-risk.pdf",
            SeverityLevel.High);

        var detailResponse = await context.Service.GetPatientMedicalRecordAsync(
            patient.Id,
            lowRecord.MedicalFileId,
            AuthorizedMemberPortalTestContext.Principal(member.Id));
        var downloadResponse = await context.Service.DownloadPatientMedicalRecordAsync(
            patient.Id,
            lowRecord.MedicalFileId,
            AuthorizedMemberPortalTestContext.Principal(member.Id));
        var highDownload = await context.Service.DownloadPatientMedicalRecordAsync(
            patient.Id,
            highRecord.MedicalFileId,
            AuthorizedMemberPortalTestContext.Principal(member.Id));
        var unlinkedDetail = await context.Service.GetPatientMedicalRecordAsync(
            outsider.Id,
            lowRecord.MedicalFileId,
            AuthorizedMemberPortalTestContext.Principal(member.Id));
        var missingIdentity = await context.Service.GetDashboardAsync(new ClaimsPrincipal(new ClaimsIdentity()));

        var detail = OkValue(detailResponse);
        Assert.Equal("Complete Blood Count", detail.Title);
        Assert.Equal("Hemoglobin mildly low.", detail.Summary);
        var physicalFile = Assert.IsType<PhysicalFileResult>(downloadResponse);
        Assert.Equal("application/pdf", physicalFile.ContentType);
        Assert.Equal("cbc-layla.pdf", physicalFile.FileDownloadName);
        Assert.IsType<BadRequestObjectResult>(highDownload);
        Assert.IsType<NotFoundObjectResult>(unlinkedDetail.Result);
        Assert.IsType<UnauthorizedObjectResult>(missingIdentity.Result);
    }

    [Fact]
    public async Task InviteEndpoints_ListAcceptAndRejectOnlyCurrentMemberInvites()
    {
        using var context = new AuthorizedMemberPortalTestContext();
        var clinic = context.AddClinic("General Medicine");
        var member = context.AddAuthorizedMember("Roaa Hamoudah");
        var otherMember = context.AddAuthorizedMember("Other Member");
        var layla = context.AddPatient("Layla Mansour", "P-11002", clinic.ClinicId);
        var ahmad = context.AddPatient("Ahmad Darwish", "P-06543", clinic.ClinicId);
        var foreignPatient = context.AddPatient("Foreign Patient", "P-99999", clinic.ClinicId);
        var acceptInvite = context.AddInvite(layla.Id, member.Id, RelationshipType.Guardian);
        var rejectInvite = context.AddInvite(ahmad.Id, member.Id, RelationshipType.Brother);
        context.AddInvite(foreignPatient.Id, otherMember.Id, RelationshipType.Other);

        var listResponse = await context.Service.GetMyInvitesAsync(
            AuthorizedMemberPortalTestContext.Principal(member.Id));
        var acceptResponse = await context.Service.AcceptInviteAsync(
            acceptInvite.InviteId,
            AuthorizedMemberPortalTestContext.Principal(member.Id));
        var rejectResponse = await context.Service.RejectInviteAsync(
            rejectInvite.InviteId,
            AuthorizedMemberPortalTestContext.Principal(member.Id));
        var repeatedAccept = await context.Service.AcceptInviteAsync(
            acceptInvite.InviteId,
            AuthorizedMemberPortalTestContext.Principal(member.Id));

        var invites = OkValue(listResponse);
        Assert.Equal(2, invites.Count);
        Assert.DoesNotContain(invites, invite => invite.PatientId == foreignPatient.Id);
        Assert.Equal("Accepted", OkValue(acceptResponse).Status);
        Assert.Equal("Rejected", OkValue(rejectResponse).Status);
        Assert.True(context.DbContext.PatientAuthorizedMembers.Any(
            relation => relation.PatientId == layla.Id && relation.AuthorizedMemberId == member.Id));
        Assert.False(context.DbContext.PatientAuthorizedMembers.Any(
            relation => relation.PatientId == ahmad.Id && relation.AuthorizedMemberId == member.Id));
        Assert.IsType<BadRequestObjectResult>(repeatedAccept.Result);
    }

    [Fact]
    public void AuthorizedMemberRoutes_AreCompleteAndNotDuplicated()
    {
        var endpoints = GetControllerEndpoints().ToList();
        var duplicateEndpoints = endpoints
            .GroupBy(endpoint => $"{endpoint.HttpMethod} {endpoint.Template}", StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(endpoint => endpoint.ActionName))}")
            .ToList();
        var authorizedMemberEndpoints = endpoints
            .Where(endpoint => endpoint.ControllerName == nameof(AuthorizedMembersController))
            .Select(endpoint => $"{endpoint.HttpMethod} {endpoint.Template}")
            .OrderBy(endpoint => endpoint)
            .ToList();

        Assert.Empty(duplicateEndpoints);
        Assert.Equal(
            [
                "GET /api/authorizedmembers/dashboard",
                "GET /api/authorizedmembers/me",
                "GET /api/authorizedmembers/my-appointments",
                "GET /api/authorizedmembers/my-invites",
                "GET /api/authorizedmembers/my-patients",
                "GET /api/authorizedmembers/my-patients/{patientid:guid}",
                "GET /api/authorizedmembers/my-patients/{patientid:guid}/medical-records",
                "GET /api/authorizedmembers/my-patients/{patientid:guid}/medical-records/{medicalfileid:guid}",
                "GET /api/authorizedmembers/my-patients/{patientid:guid}/medical-records/{medicalfileid:guid}/download",
                "POST /api/authorizedmembers/my-invites/{inviteid:guid}/accept",
                "POST /api/authorizedmembers/my-invites/{inviteid:guid}/reject",
                "PUT /api/authorizedmembers/me"
            ],
            authorizedMemberEndpoints);
    }

    private static T OkValue<T>(ActionResult<T> response)
    {
        var ok = Assert.IsType<OkObjectResult>(response.Result);
        return Assert.IsType<T>(ok.Value);
    }

    private static IEnumerable<ControllerEndpoint> GetControllerEndpoints()
    {
        var controllerTypes = typeof(AuthorizedMembersController).Assembly
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

internal sealed class AuthorizedMemberPortalTestContext : IDisposable
{
    public AuthorizedMemberPortalTestContext()
    {
        ContentRootPath = Path.Combine(Path.GetTempPath(), "hscsapi-authorized-member-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ContentRootPath);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        DbContext = new AppDbContext(options);
        DbContext.Database.EnsureCreated();
        Service = new AuthorizedMembersService(DbContext, new TestWebHostEnvironment(ContentRootPath));
    }

    public string ContentRootPath { get; }
    public AppDbContext DbContext { get; }
    public AuthorizedMembersService Service { get; }

    public Clinic AddClinic(string name)
    {
        var clinic = new Clinic
        {
            ClinicId = Guid.NewGuid(),
            Name = name,
            Address = "Main Street",
            CreatedBySuperAdminUserId = Guid.NewGuid()
        };

        DbContext.Clinics.Add(clinic);
        DbContext.SaveChanges();
        return clinic;
    }

    public User AddAuthorizedMember(
        string name,
        string? phoneNumber = null,
        string? address = null,
        DateOnly? dateOfBirth = null)
    {
        var user = AddUser(name, phoneNumber: phoneNumber, address: address, dateOfBirth: dateOfBirth);
        DbContext.AuthorizedMembers.Add(new AuthorizedMember
        {
            AuthorizedMemberId = user.Id,
            User = user
        });
        DbContext.SaveChanges();
        return user;
    }

    public User AddPatient(
        string name,
        string patientUserId,
        Guid clinicId,
        Gender gender = Gender.Female,
        BloodType? bloodType = BloodType.APositive)
    {
        var user = AddUser(name, clinicId);
        DbContext.Patients.Add(new Patient
        {
            PatientId = user.Id,
            UserID = patientUserId,
            Gender = gender,
            BloodType = bloodType,
            User = user
        });
        DbContext.SaveChanges();
        return user;
    }

    public User AddDoctor(string name, Guid clinicId)
    {
        var user = AddUser(name, clinicId);
        DbContext.Doctors.Add(new Doctor
        {
            DoctorId = user.Id,
            ProfessionalLicenseNumber = $"LIC-{user.Id:N}"[..16],
            User = user
        });
        DbContext.SaveChanges();
        return user;
    }

    public void LinkPatient(Guid authorizedMemberId, Guid patientId, RelationshipType relationshipType)
    {
        DbContext.PatientAuthorizedMembers.Add(new PatientAuthorizedMember
        {
            AuthorizedMemberId = authorizedMemberId,
            PatientId = patientId,
            RelationshipType = relationshipType,
            AuthorizedAt = DateTime.UtcNow
        });
        DbContext.SaveChanges();
    }

    public Appointment AddAppointment(
        Guid doctorId,
        Guid patientId,
        DateOnly appointmentDate,
        TimeOnly appointmentTime,
        string? notes = null)
    {
        var slot = new AvailabilitySlot
        {
            DoctorId = doctorId,
            DayOfWeek = appointmentDate.DayOfWeek,
            StartTime = appointmentTime,
            EndTime = appointmentTime.AddMinutes(30),
            IsAvailable = true
        };
        var appointment = new Appointment
        {
            DoctorId = doctorId,
            PatientId = patientId,
            AvailabilitySlotId = slot.AvailabilitySlotId,
            AvailabilitySlot = slot,
            AppointmentDate = appointmentDate,
            AppointmentTime = appointmentTime,
            Notes = notes
        };

        DbContext.AvailabilitySlots.Add(slot);
        DbContext.Appointments.Add(appointment);
        DbContext.SaveChanges();
        return appointment;
    }

    public MedicalFile AddMedicalRecord(
        Appointment appointment,
        Guid doctorId,
        string fileName,
        SeverityLevel severityLevel,
        string? labTestName = null,
        string? imagingTestName = null,
        string? clinicalNotes = null)
    {
        var relativePath = Path.Combine("medical-records", fileName);
        var physicalPath = Path.Combine(ContentRootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);
        File.WriteAllText(physicalPath, $"test content for {fileName}");

        var medicalFile = new MedicalFile
        {
            MedicalFileId = Guid.NewGuid(),
            AppointmentId = appointment.AppointmentId,
            UploadedByDoctorId = doctorId,
            FileType = MedicalFileType.Pdf,
            FilePath = relativePath,
            EncryptedChecksum = Guid.NewGuid().ToString("N"),
            FileSizeInBytes = new FileInfo(physicalPath).Length,
            SeverityLevel = severityLevel,
            UploadedAt = DateTime.UtcNow
        };

        DbContext.MedicalFiles.Add(medicalFile);

        if (!string.IsNullOrWhiteSpace(labTestName))
        {
            DbContext.LabTestRequests.Add(new LabTestRequest
            {
                TestName = labTestName,
                PatientId = appointment.PatientId,
                RequestedByDoctorId = doctorId,
                ClinicalNotes = clinicalNotes,
                ResultMedicalFileId = medicalFile.MedicalFileId,
                ResultMedicalFile = medicalFile
            });
        }

        if (!string.IsNullOrWhiteSpace(imagingTestName))
        {
            DbContext.ImagingTestRequests.Add(new ImagingTestRequest
            {
                TestName = imagingTestName,
                PatientId = appointment.PatientId,
                RequestedByDoctorId = doctorId,
                ClinicalNotes = clinicalNotes,
                ResultMedicalFileId = medicalFile.MedicalFileId,
                ResultMedicalFile = medicalFile
            });
        }

        DbContext.SaveChanges();
        return medicalFile;
    }

    public Notification AddNotification(Guid userId, string title, bool isRead)
    {
        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            IsRead = isRead,
            CreatedAt = DateTime.UtcNow
        };

        DbContext.Notifications.Add(notification);
        DbContext.SaveChanges();
        return notification;
    }

    public Invite AddInvite(Guid patientId, Guid authorizedMemberId, RelationshipType relationshipType)
    {
        var invite = new Invite
        {
            PatientId = patientId,
            AuthorizedMemberId = authorizedMemberId,
            RelationshipType = relationshipType,
            Status = InviteStatus.Pending,
            SentAt = DateTime.UtcNow
        };

        DbContext.Invites.Add(invite);
        DbContext.SaveChanges();
        return invite;
    }

    public static ClaimsPrincipal Principal(Guid userId)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, UserSystemRole.AuthorizedMember.ToString())
            ],
            "Test"));
    }

    public void Dispose()
    {
        DbContext.Dispose();
        if (Directory.Exists(ContentRootPath))
        {
            Directory.Delete(ContentRootPath, recursive: true);
        }
    }

    private User AddUser(
        string name,
        Guid? clinicId = null,
        string? phoneNumber = null,
        string? address = null,
        DateOnly? dateOfBirth = null)
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
            PhoneNumber = phoneNumber,
            Address = address,
            DateOfBirth = dateOfBirth,
            ClinicId = clinicId,
            RegisteredAt = DateTime.UtcNow
        };

        DbContext.Users.Add(user);
        DbContext.SaveChanges();
        return user;
    }
}
