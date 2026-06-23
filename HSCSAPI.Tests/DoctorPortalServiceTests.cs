using System.Security.Claims;
using HSCSAPI.Data;
using HSCSAPI.DTOs.Doctor;
using HSCSAPI.Models.Appointments;
using HSCSAPI.Models.Chats;
using HSCSAPI.Models.Clinics;
using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Identity;
using HSCSAPI.Models.Laboratory;
using HSCSAPI.Models.Profiles;
using HSCSAPI.Models.Radiology;
using HSCSAPI.Services.Auth;
using HSCSAPI.Services.Doctors;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HSCSAPI.Tests;

public class DoctorPortalServiceTests
{
    [Fact]
    public async Task GetMyDashboard_ReturnsFrontendSummary()
    {
        using var context = new DoctorPortalTestContext();
        var clinic = context.AddClinic("Central Clinic");
        var doctor = await context.AddDoctorAsync(clinic.ClinicId);
        var patient = await context.AddPatientAsync(clinic.ClinicId, "pat-001", "Sarah Al-Hassan");
        var today = DateOnly.FromDateTime(DateTime.Now);

        context.AddAppointment(doctor.Id, patient.Id, today, new TimeOnly(9, 0));
        context.AddAppointment(doctor.Id, patient.Id, today, new TimeOnly(10, 0));
        context.AddAppointment(doctor.Id, patient.Id, today.AddDays(1), new TimeOnly(11, 0));
        await context.AddPendingClinicalRequestsAsync(clinic.ClinicId, labCount: 2, imagingCount: 2);
        context.AddUnreadMessages(doctor.Id, patient.Id, count: 3);
        await context.DbContext.SaveChangesAsync();

        var response = await context.Service.GetMyDashboardAsync(
            DoctorPortalTestContext.Principal(doctor.Id),
            CancellationToken.None);

        var dashboard = OkValue(response);
        Assert.Equal(doctor.Id, dashboard.DoctorId);
        Assert.Equal(2, dashboard.TodayAppointmentsCount);
        Assert.Equal(2, dashboard.PendingLabRequestsCount);
        Assert.Equal(2, dashboard.PendingImagingRequestsCount);
        Assert.Equal(4, dashboard.TotalPendingClinicalRequestsCount);
        Assert.Equal(3, dashboard.UnreadMessagesCount);
        Assert.Equal(3, dashboard.UpcomingAppointments.Count);
        Assert.Equal(7, dashboard.WeeklyAppointments.Count);
        Assert.Equal(4, dashboard.ClinicalRequests.TotalPending);
    }

    [Fact]
    public async Task GetMyAppointmentsSchedule_ReturnsGroupedDaysAndOwnAppointments()
    {
        using var context = new DoctorPortalTestContext();
        var clinic = context.AddClinic("Central Clinic");
        var doctor = await context.AddDoctorAsync(clinic.ClinicId);
        var otherDoctor = await context.AddDoctorAsync(clinic.ClinicId, email: "other.doctor@test.local");
        var patient = await context.AddPatientAsync(clinic.ClinicId, "pat-002", "James Mitchell");
        var fromDate = new DateOnly(2026, 6, 22);
        var toDate = fromDate.AddDays(2);

        context.AddAppointment(doctor.Id, patient.Id, fromDate, new TimeOnly(9, 0), durationMinutes: 45);
        context.AddAppointment(doctor.Id, patient.Id, fromDate.AddDays(2), new TimeOnly(14, 30), durationMinutes: 30);
        context.AddAppointment(otherDoctor.Id, patient.Id, fromDate.AddDays(1), new TimeOnly(11, 0));
        await context.DbContext.SaveChangesAsync();

        var response = await context.Service.GetMyAppointmentsScheduleAsync(
            fromDate,
            toDate,
            DoctorPortalTestContext.Principal(doctor.Id),
            CancellationToken.None);

        var schedule = OkValue(response);
        Assert.Equal(fromDate, schedule.FromDate);
        Assert.Equal(toDate, schedule.ToDate);
        Assert.Equal(2, schedule.TotalAppointmentsCount);
        Assert.Equal(3, schedule.Days.Count);
        Assert.Single(schedule.Days[0].Appointments);
        Assert.Empty(schedule.Days[1].Appointments);
        Assert.Single(schedule.Days[2].Appointments);
        Assert.Equal(45, schedule.Days[0].Appointments[0].DurationMinutes);
        Assert.Equal("Scheduled", schedule.Days[0].Appointments[0].Status);
        Assert.Equal("pat-002", schedule.Days[0].Appointments[0].PatientUserId);
    }

    [Fact]
    public async Task GetMyAppointmentDetail_ReturnsOnlyTheCurrentDoctorsAppointment()
    {
        using var context = new DoctorPortalTestContext();
        var clinic = context.AddClinic("Central Clinic");
        var doctor = await context.AddDoctorAsync(clinic.ClinicId);
        var otherDoctor = await context.AddDoctorAsync(clinic.ClinicId, email: "second.doctor@test.local");
        var patient = await context.AddPatientAsync(clinic.ClinicId, "pat-003", "Layla Khoury");
        var ownAppointment = context.AddAppointment(
            doctor.Id,
            patient.Id,
            new DateOnly(2026, 6, 25),
            new TimeOnly(9, 0),
            notes: "Pre-operative assessment");
        var otherAppointment = context.AddAppointment(
            otherDoctor.Id,
            patient.Id,
            new DateOnly(2026, 6, 26),
            new TimeOnly(15, 0));
        await context.DbContext.SaveChangesAsync();

        var ownResponse = await context.Service.GetMyAppointmentDetailAsync(
            ownAppointment.AppointmentId,
            DoctorPortalTestContext.Principal(doctor.Id),
            CancellationToken.None);
        var otherResponse = await context.Service.GetMyAppointmentDetailAsync(
            otherAppointment.AppointmentId,
            DoctorPortalTestContext.Principal(doctor.Id),
            CancellationToken.None);

        var detail = OkValue(ownResponse);
        Assert.Equal(ownAppointment.AppointmentId, detail.AppointmentId);
        Assert.Equal("Layla Khoury", detail.PatientName);
        Assert.Equal("Pre-operative assessment", detail.ReasonForVisit);
        Assert.IsType<NotFoundObjectResult>(otherResponse.Result);
    }

    [Fact]
    public async Task ChangeMyPassword_UpdatesDoctorPassword()
    {
        using var context = new DoctorPortalTestContext();
        var clinic = context.AddClinic("Central Clinic");
        var doctor = await context.AddDoctorAsync(clinic.ClinicId, password: "OldPass123");

        var response = await context.Service.ChangeMyPasswordAsync(
            new ChangeDoctorPasswordRequest
            {
                CurrentPassword = "OldPass123",
                NewPassword = "NewPass123",
                ConfirmNewPassword = "NewPass123"
            },
            DoctorPortalTestContext.Principal(doctor.Id),
            CancellationToken.None);

        var result = OkValue(response);
        Assert.True(result.Success);
        Assert.True(await context.UserManager.CheckPasswordAsync(doctor, "NewPass123"));
        Assert.False(await context.UserManager.CheckPasswordAsync(doctor, "OldPass123"));
    }

    [Fact]
    public async Task ChangeMyPassword_RejectsWrongCurrentPassword()
    {
        using var context = new DoctorPortalTestContext();
        var clinic = context.AddClinic("Central Clinic");
        var doctor = await context.AddDoctorAsync(clinic.ClinicId, password: "OldPass123");

        var response = await context.Service.ChangeMyPasswordAsync(
            new ChangeDoctorPasswordRequest
            {
                CurrentPassword = "WrongPass123",
                NewPassword = "NewPass123",
                ConfirmNewPassword = "NewPass123"
            },
            DoctorPortalTestContext.Principal(doctor.Id),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(response.Result);
        var result = Assert.IsType<ChangeDoctorPasswordResponse>(badRequest.Value);
        Assert.False(result.Success);
        Assert.True(await context.UserManager.CheckPasswordAsync(doctor, "OldPass123"));
    }

    private static T OkValue<T>(ActionResult<T> response)
    {
        var ok = Assert.IsType<OkObjectResult>(response.Result);
        return Assert.IsType<T>(ok.Value);
    }
}

internal sealed class DoctorPortalTestContext : IDisposable
{
    private readonly ServiceProvider _serviceProvider;

    public DoctorPortalTestContext()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));
        services
            .AddIdentityCore<User>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<Role>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();
        services.AddScoped<IPasswordHasher<User>, LegacyCompatiblePasswordHasher>();

        _serviceProvider = services.BuildServiceProvider();
        DbContext = _serviceProvider.GetRequiredService<AppDbContext>();
        UserManager = _serviceProvider.GetRequiredService<UserManager<User>>();
        Service = new DoctorsService(DbContext, UserManager);
    }

    public AppDbContext DbContext { get; }
    public UserManager<User> UserManager { get; }
    public DoctorsService Service { get; }

    public Clinic AddClinic(string name)
    {
        var clinic = new Clinic
        {
            ClinicId = Guid.NewGuid(),
            Name = name,
            CreatedBySuperAdminUserId = Guid.NewGuid()
        };

        DbContext.Clinics.Add(clinic);
        DbContext.SaveChanges();
        return clinic;
    }

    public async Task<User> AddDoctorAsync(
        Guid clinicId,
        string email = "doctor@test.local",
        string password = "DoctorPass123")
    {
        var user = await AddUserAsync(email, "Dr. Samer Khalil", clinicId, password);
        DbContext.Doctors.Add(new Doctor
        {
            DoctorId = user.Id,
            ProfessionalLicenseNumber = $"DOC-{Guid.NewGuid():N}"
        });
        await DbContext.SaveChangesAsync();
        return user;
    }

    public async Task<User> AddPatientAsync(Guid clinicId, string patientUserId, string name)
    {
        var user = await AddUserAsync($"{patientUserId}@test.local", name, clinicId, "PatientPass123");
        DbContext.Patients.Add(new Patient
        {
            PatientId = user.Id,
            UserID = patientUserId,
            Gender = Gender.Female,
            BloodType = BloodType.OPositive
        });
        await DbContext.SaveChangesAsync();
        return user;
    }

    public Appointment AddAppointment(
        Guid doctorId,
        Guid patientId,
        DateOnly date,
        TimeOnly time,
        int durationMinutes = 45,
        string? notes = "Routine heart checkup and vitals review")
    {
        var slot = new AvailabilitySlot
        {
            AvailabilitySlotId = Guid.NewGuid(),
            DoctorId = doctorId,
            DayOfWeek = date.DayOfWeek,
            StartTime = time,
            EndTime = time.AddMinutes(durationMinutes),
            IsAvailable = false
        };
        var appointment = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            DoctorId = doctorId,
            PatientId = patientId,
            AvailabilitySlotId = slot.AvailabilitySlotId,
            AppointmentDate = date,
            AppointmentTime = time,
            Notes = notes
        };

        DbContext.AvailabilitySlots.Add(slot);
        DbContext.Appointments.Add(appointment);
        return appointment;
    }

    public async Task AddPendingClinicalRequestsAsync(Guid clinicId, int labCount, int imagingCount)
    {
        var laboratoryTechnologist = await AddUserAsync(
            $"lab-{Guid.NewGuid():N}@test.local",
            "Rana Lab",
            clinicId,
            "LabPass123");
        DbContext.LaboratoryTechnologists.Add(new LaboratoryTechnologist
        {
            LaboratoryTechnologistId = laboratoryTechnologist.Id,
            ProfessionalLicenseNumber = $"LAB-{Guid.NewGuid():N}"
        });

        var radiologyTechnologist = await AddUserAsync(
            $"rad-{Guid.NewGuid():N}@test.local",
            "Yousef Ray",
            clinicId,
            "RadPass123");
        DbContext.RadiologyTechnologists.Add(new RadiologyTechnologist
        {
            RadiologyTechnologistId = radiologyTechnologist.Id,
            ProfessionalLicenseNumber = $"RAD-{Guid.NewGuid():N}"
        });

        for (var index = 0; index < labCount; index++)
        {
            DbContext.LabTestRequests.Add(new LabTestRequest
            {
                TestName = $"Lab request {index + 1}",
                LaboratoryTechnologistId = laboratoryTechnologist.Id,
                ResultMedicalFileId = null
            });
        }

        for (var index = 0; index < imagingCount; index++)
        {
            DbContext.ImagingTestRequests.Add(new ImagingTestRequest
            {
                TestName = $"Imaging request {index + 1}",
                RadiologyTechnologistId = radiologyTechnologist.Id,
                ResultMedicalFileId = null
            });
        }
    }

    public void AddUnreadMessages(Guid doctorId, Guid patientId, int count)
    {
        var chat = new Chat
        {
            ChatId = Guid.NewGuid(),
            UserOneId = doctorId,
            UserTwoId = patientId,
            CreatedAt = DateTime.UtcNow
        };
        DbContext.Chats.Add(chat);

        for (var index = 0; index < count; index++)
        {
            DbContext.ChatMessages.Add(new ChatMessage
            {
                ChatId = chat.ChatId,
                SenderId = patientId,
                MessageType = ChatMessageType.Text,
                Text = $"Message {index + 1}",
                CreatedAt = DateTime.UtcNow.AddMinutes(index),
                ReadAt = null
            });
        }
    }

    public static ClaimsPrincipal Principal(Guid userId)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, nameof(UserSystemRole.Doctor))
            ],
            "Test"));
    }

    public void Dispose()
    {
        DbContext.Dispose();
        _serviceProvider.Dispose();
    }

    private async Task<User> AddUserAsync(string email, string name, Guid clinicId, string password)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = name,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            RegisteredAt = DateTime.UtcNow,
            ClinicId = clinicId
        };

        var result = await UserManager.CreateAsync(user, password);
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Description)));
        return user;
    }
}
