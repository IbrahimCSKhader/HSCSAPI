using System.Security.Claims;
using HSCSAPI.Data;
using HSCSAPI.DTOs.Appointment;
using HSCSAPI.DTOs.Common;
using HSCSAPI.DTOs.Secretary;
using HSCSAPI.Models.Appointments;
using HSCSAPI.Models.Clinics;
using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Identity;
using HSCSAPI.Models.Profiles;
using HSCSAPI.Models.Secretaries;
using HSCSAPI.Services.Auth;
using HSCSAPI.Services.Secretaries;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HSCSAPI.Tests;

public class SecretarySelfServiceTests
{
    [Fact]
    public async Task GetMyProfile_ReturnsCurrentSecretaryProfile()
    {
        using var context = new SecretarySelfTestContext();
        var clinic = context.AddClinic("Central Clinic");
        var secretary = await context.AddSecretaryAsync(clinic.ClinicId);

        var response = await context.Service.GetMyProfileAsync(
            SecretarySelfTestContext.Principal(secretary.Id),
            CancellationToken.None);

        var profile = OkValue(response);
        Assert.Equal(secretary.Id, profile.SecretaryId);
        Assert.Equal("Mira Secretary", profile.Name);
        Assert.Equal("secretary@test.local", profile.Email);
        Assert.Equal(clinic.ClinicId, profile.ClinicId);
        Assert.Equal("Central Clinic", profile.ClinicName);
        Assert.True(profile.IsActive);
    }

    [Fact]
    public async Task UpdateMyProfile_UpdatesOnlyTheCurrentSecretaryAccount()
    {
        using var context = new SecretarySelfTestContext();
        var clinic = context.AddClinic("Central Clinic");
        var secretary = await context.AddSecretaryAsync(clinic.ClinicId, email: "old.secretary@test.local");
        var otherSecretary = await context.AddSecretaryAsync(clinic.ClinicId, email: "other.secretary@test.local");

        var response = await context.Service.UpdateMyProfileAsync(
            new UpdateSecretaryRequest
            {
                Name = "Updated Secretary",
                Email = "new.secretary@test.local",
                PhoneNumber = " 0599000000 ",
                Address = " Ramallah ",
                DateOfBirth = new DateOnly(1995, 4, 12)
            },
            SecretarySelfTestContext.Principal(secretary.Id),
            CancellationToken.None);

        var profile = OkValue(response);
        Assert.Equal("Updated Secretary", profile.Name);
        Assert.Equal("new.secretary@test.local", profile.Email);
        Assert.Equal("0599000000", profile.PhoneNumber);
        Assert.Equal("Ramallah", profile.Address);
        Assert.Equal(new DateOnly(1995, 4, 12), profile.DateOfBirth);

        var refreshed = await context.UserManager.FindByIdAsync(secretary.Id.ToString());
        Assert.Equal("new.secretary@test.local", refreshed!.Email);
        Assert.Equal("new.secretary@test.local", refreshed.UserName);
        Assert.Equal("other.secretary@test.local", (await context.UserManager.FindByIdAsync(otherSecretary.Id.ToString()))!.Email);
    }

    [Fact]
    public async Task ChangeMyPassword_UpdatesPasswordAndTimestamp()
    {
        using var context = new SecretarySelfTestContext();
        var clinic = context.AddClinic("Central Clinic");
        var secretary = await context.AddSecretaryAsync(clinic.ClinicId, password: "OldPass123");

        var response = await context.Service.ChangeMyPasswordAsync(
            new ChangePasswordRequest
            {
                CurrentPassword = "OldPass123",
                NewPassword = "NewPass123",
                ConfirmNewPassword = "NewPass123"
            },
            SecretarySelfTestContext.Principal(secretary.Id),
            CancellationToken.None);

        var result = OkValue(response);
        Assert.True(result.Success);
        Assert.NotNull(result.PasswordLastUpdatedIso);
        Assert.True(await context.UserManager.CheckPasswordAsync(secretary, "NewPass123"));
        Assert.False(await context.UserManager.CheckPasswordAsync(secretary, "OldPass123"));

        var refreshed = await context.UserManager.FindByIdAsync(secretary.Id.ToString());
        Assert.NotNull(refreshed!.PasswordLastUpdatedAt);
    }

    [Fact]
    public async Task ChangeMyPassword_RejectsWrongCurrentPassword()
    {
        using var context = new SecretarySelfTestContext();
        var clinic = context.AddClinic("Central Clinic");
        var secretary = await context.AddSecretaryAsync(clinic.ClinicId, password: "OldPass123");

        var response = await context.Service.ChangeMyPasswordAsync(
            new ChangePasswordRequest
            {
                CurrentPassword = "WrongPass123",
                NewPassword = "NewPass123",
                ConfirmNewPassword = "NewPass123"
            },
            SecretarySelfTestContext.Principal(secretary.Id),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(response.Result);
        var result = Assert.IsType<ChangePasswordResponse>(badRequest.Value);
        Assert.False(result.Success);
        Assert.True(await context.UserManager.CheckPasswordAsync(secretary, "OldPass123"));
    }

    [Fact]
    public async Task UpdateMyProfile_RejectsDuplicateEmail()
    {
        using var context = new SecretarySelfTestContext();
        var clinic = context.AddClinic("Central Clinic");
        var secretary = await context.AddSecretaryAsync(clinic.ClinicId, email: "secretary@test.local");
        await context.AddSecretaryAsync(clinic.ClinicId, email: "taken@test.local");

        var response = await context.Service.UpdateMyProfileAsync(
            new UpdateSecretaryRequest
            {
                Name = "Mira Secretary",
                Email = "taken@test.local"
            },
            SecretarySelfTestContext.Principal(secretary.Id),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Equal("Email already registered.", badRequest.Value);
    }

    [Fact]
    public async Task UpdateDoctorAvailabilitySlot_UpdatesUnbookedSlot()
    {
        using var context = new SecretarySelfTestContext();
        var clinic = context.AddClinic("Central Clinic");
        var secretary = await context.AddSecretaryAsync(clinic.ClinicId);
        var doctor = context.AddDoctor(clinic.ClinicId, "Dr. Sami");
        var slot = context.AddAvailabilitySlot(doctor.Id, new DateOnly(2026, 7, 20), new TimeOnly(9, 0), new TimeOnly(9, 30));

        var response = await context.Service.UpdateDoctorAvailabilitySlotAsync(
            doctor.Id,
            slot.AvailabilitySlotId,
            new UpdateAvailabilitySlotRequest
            {
                SlotDate = new DateOnly(2026, 7, 21),
                StartTime = new TimeOnly(10, 0),
                EndTime = new TimeOnly(10, 45),
                Notes = " Updated note "
            },
            SecretarySelfTestContext.Principal(secretary.Id),
            CancellationToken.None);

        var updated = OkValue(response);
        Assert.Equal(new DateOnly(2026, 7, 21), updated.SlotDate);
        Assert.Equal(new TimeOnly(10, 0), updated.StartTime);
        Assert.Equal(new TimeOnly(10, 45), updated.EndTime);
        Assert.Equal("Updated note", updated.Notes);
        Assert.Equal(DayOfWeek.Tuesday, context.DbContext.AvailabilitySlots.Single().DayOfWeek);
    }

    [Fact]
    public async Task GenerateReport_FiltersPatientDoctorAndAppointmentCountsByDateRange()
    {
        using var context = new SecretarySelfTestContext();
        var clinic = context.AddClinic("Central Clinic");
        var secretary = await context.AddSecretaryAsync(clinic.ClinicId);
        var insideDoctor = context.AddDoctor(clinic.ClinicId, "Dr. Inside", new DateTime(2026, 7, 8, 9, 0, 0, DateTimeKind.Utc));
        var outsideDoctor = context.AddDoctor(clinic.ClinicId, "Dr. Outside", new DateTime(2026, 6, 8, 9, 0, 0, DateTimeKind.Utc));
        var insidePatient = context.AddPatient(clinic.ClinicId, "P-100", "Inside Patient", new DateTime(2026, 7, 9, 9, 0, 0, DateTimeKind.Utc));
        var outsidePatient = context.AddPatient(clinic.ClinicId, "P-200", "Outside Patient", new DateTime(2026, 6, 9, 9, 0, 0, DateTimeKind.Utc));
        context.AddAppointment(insideDoctor.Id, insidePatient.Id, new DateOnly(2026, 7, 10), new TimeOnly(9, 0));
        context.AddAppointment(outsideDoctor.Id, outsidePatient.Id, new DateOnly(2026, 6, 10), new TimeOnly(9, 0));
        await context.DbContext.SaveChangesAsync();

        var response = await context.Service.GenerateReportAsync(
            new GenerateSecretaryReportRequest
            {
                ReportType = "ClinicOverview",
                FileFormat = "Csv",
                FromDate = new DateOnly(2026, 7, 1),
                ToDate = new DateOnly(2026, 7, 31)
            },
            SecretarySelfTestContext.Principal(secretary.Id),
            CancellationToken.None);

        var report = OkCreatedValue(response);
        var storedFile = context.DbContext.ReportInformations.Single();
        var csvPath = Path.Combine(Directory.GetCurrentDirectory(), storedFile.FilePath.Replace('/', Path.DirectorySeparatorChar));
        var csv = await File.ReadAllTextAsync(csvPath);

        Assert.Contains("period,\"01 Jul 2026 - 31 Jul 2026\"", csv);
        Assert.Contains("patients,1", csv);
        Assert.Contains("doctors,1", csv);
        Assert.Contains("appointments,1", csv);

        File.Delete(csvPath);
    }

    private static T OkValue<T>(ActionResult<T> response)
    {
        var ok = Assert.IsType<OkObjectResult>(response.Result);
        return Assert.IsType<T>(ok.Value);
    }

    private static T OkCreatedValue<T>(ActionResult<T> response)
    {
        var created = Assert.IsType<CreatedAtActionResult>(response.Result);
        return Assert.IsType<T>(created.Value);
    }
}

internal sealed class SecretarySelfTestContext : IDisposable
{
    private readonly ServiceProvider _serviceProvider;

    public SecretarySelfTestContext()
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
        Service = new SecretariesService(DbContext, UserManager);
    }

    public AppDbContext DbContext { get; }
    public UserManager<User> UserManager { get; }
    public SecretariesService Service { get; }

    public Clinic AddClinic(string name)
    {
        var clinic = new Clinic
        {
            ClinicId = Guid.NewGuid(),
            Name = name,
            CreatedBySuperAdminUserId = Guid.NewGuid(),
            IsActive = true
        };

        DbContext.Clinics.Add(clinic);
        DbContext.SaveChanges();
        return clinic;
    }

    public async Task<User> AddSecretaryAsync(
        Guid clinicId,
        string email = "secretary@test.local",
        string password = "SecretaryPass123")
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Mira Secretary",
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            RegisteredAt = DateTime.UtcNow,
            ClinicId = clinicId,
            IsActive = true
        };

        var result = await UserManager.CreateAsync(user, password);
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Description)));

        DbContext.Secretaries.Add(new Secretary
        {
            SecretaryId = user.Id,
            User = user
        });
        await DbContext.SaveChangesAsync();
        return user;
    }

    public User AddDoctor(Guid clinicId, string name, DateTime? registeredAt = null)
    {
        var user = AddUser(clinicId, name, registeredAt);
        DbContext.Doctors.Add(new Doctor
        {
            DoctorId = user.Id,
            ProfessionalLicenseNumber = $"DOC-{Guid.NewGuid():N}",
            User = user
        });
        DbContext.SaveChanges();
        return user;
    }

    public User AddPatient(Guid clinicId, string patientUserId, string name, DateTime? registeredAt = null)
    {
        var user = AddUser(clinicId, name, registeredAt);
        DbContext.Patients.Add(new Patient
        {
            PatientId = user.Id,
            UserID = patientUserId,
            Gender = Gender.Female,
            BloodType = BloodType.APositive,
            User = user
        });
        DbContext.SaveChanges();
        return user;
    }

    public AvailabilitySlot AddAvailabilitySlot(Guid doctorId, DateOnly date, TimeOnly start, TimeOnly end)
    {
        var slot = new AvailabilitySlot
        {
            DoctorId = doctorId,
            SlotDate = date,
            DayOfWeek = date.DayOfWeek,
            StartTime = start,
            EndTime = end,
            IsAvailable = true
        };
        DbContext.AvailabilitySlots.Add(slot);
        DbContext.SaveChanges();
        return slot;
    }

    public Appointment AddAppointment(Guid doctorId, Guid patientId, DateOnly date, TimeOnly time)
    {
        var slot = AddAvailabilitySlot(doctorId, date, time, time.AddMinutes(30));
        var appointment = new Appointment
        {
            DoctorId = doctorId,
            PatientId = patientId,
            AvailabilitySlotId = slot.AvailabilitySlotId,
            AppointmentDate = date,
            AppointmentTime = time
        };
        DbContext.Appointments.Add(appointment);
        return appointment;
    }

    public static ClaimsPrincipal Principal(Guid userId)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, nameof(UserSystemRole.Secretary))
            ],
            "Test"));
    }

    public void Dispose()
    {
        DbContext.Dispose();
        _serviceProvider.Dispose();
    }

    private User AddUser(Guid clinicId, string name, DateTime? registeredAt)
    {
        var id = Guid.NewGuid();
        var email = $"{id:N}@test.local";
        var user = new User
        {
            Id = id,
            Name = name,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            RegisteredAt = registeredAt ?? DateTime.UtcNow,
            ClinicId = clinicId,
            IsActive = true
        };

        DbContext.Users.Add(user);
        return user;
    }
}
