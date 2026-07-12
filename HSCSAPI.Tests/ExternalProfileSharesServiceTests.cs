using System.Security.Claims;
using System.Text.RegularExpressions;
using HSCSAPI.Data;
using HSCSAPI.DTOs.PatientProfile;
using HSCSAPI.Models.Appointments;
using HSCSAPI.Models.Clinics;
using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Identity;
using HSCSAPI.Models.Laboratory;
using HSCSAPI.Models.MedicalFiles;
using HSCSAPI.Models.Profiles;
using HSCSAPI.Services.Email;
using HSCSAPI.Services.PatientProfile;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Tests;

public class ExternalProfileSharesServiceTests
{
    [Fact]
    public async Task SharedProfileFlow_VerifiesApprovedEmailAndReturnsPatientHistory()
    {
        using var context = new ExternalShareTestContext();
        var patient = context.AddPatient();
        var doctor = context.AddDoctor(patient.User.ClinicId!.Value);
        var medicalFile = context.AddMedicalFile(patient.PatientId, doctor.DoctorId);
        await context.DbContext.SaveChangesAsync();

        var create = await context.Service.CreateShareAsync(
            new CreateExternalProfileShareRequest
            {
                DoctorEmail = "external.doctor@example.com",
                DoctorName = "Dr. External"
            },
            ExternalShareTestContext.PatientPrincipal(patient.PatientId),
            "https://api.test",
            CancellationToken.None);

        var created = Assert.IsType<ExternalProfileShareResponse>(Assert.IsType<CreatedResult>(create.Result).Value);
        Assert.Contains("/shared-profiles/", created.ShareUrl);
        Assert.InRange(created.ExpiresAt - created.CreatedAt, TimeSpan.FromDays(1.99), TimeSpan.FromDays(2.01));
        var shareToken = created.ShareUrl.Split("/shared-profiles/", StringSplitOptions.None).Last();

        var forbidden = await context.Service.SendVerificationCodeAsync(
            shareToken,
            new ExternalShareEmailRequest { Email = "wrong@example.com" },
            "https://api.test",
            CancellationToken.None);
        Assert.Equal(403, Assert.IsType<ObjectResult>(forbidden).StatusCode);

        var sendCode = await context.Service.SendVerificationCodeAsync(
            shareToken,
            new ExternalShareEmailRequest { Email = "external.doctor@example.com" },
            "https://api.test",
            CancellationToken.None);
        Assert.IsType<OkObjectResult>(sendCode);
        var code = Regex.Match(context.EmailService.LastBody, @"\b\d{6}\b").Value;
        Assert.False(string.IsNullOrWhiteSpace(code));

        var verified = await context.Service.VerifyEmailAsync(
            shareToken,
            new ExternalShareVerifyRequest
            {
                Email = "external.doctor@example.com",
                Code = code
            },
            CancellationToken.None);
        var session = Assert.IsType<ExternalShareVerifyResponse>(Assert.IsType<OkObjectResult>(verified.Result).Value);

        var profileResponse = await context.Service.GetSharedProfileAsync(
            shareToken,
            session.AccessToken,
            CancellationToken.None);
        var profile = Assert.IsType<ExternalSharedPatientProfileResponse>(Assert.IsType<OkObjectResult>(profileResponse.Result).Value);

        Assert.Equal("Sarah Al-Hassan", profile.Patient.Name);
        Assert.Single(profile.Diagnoses);
        Assert.Equal("E11.9", profile.Diagnoses[0].DiagnosisCode);
        Assert.Single(profile.Medications);
        Assert.Equal("Metformin", profile.Medications[0].ActivityName);
        Assert.Contains(profile.Tests, test => test.TestType == "Lab" && test.Status == "Completed");
        Assert.Contains(profile.Files, file => file.MedicalFileId == medicalFile.MedicalFileId);

        var fileView = await context.Service.GetFileForViewingAsync(
            shareToken,
            medicalFile.MedicalFileId,
            session.AccessToken,
            CancellationToken.None);
        Assert.Equal("application/pdf", fileView.ContentType);
        Assert.True(File.Exists(fileView.PhysicalPath));
    }

    [Fact]
    public async Task SharedProfile_RejectsExpiredShare()
    {
        using var context = new ExternalShareTestContext();
        var patient = context.AddPatient();
        await context.DbContext.SaveChangesAsync();

        var share = new HSCSAPI.Models.PatientProfile.ExternalPatientProfileShare
        {
            PatientId = patient.PatientId,
            DoctorEmail = "doctor@example.com",
            ShareToken = "expired-token",
            ShareTokenHash = ExternalShareTestContext.HashForTest("expired-token"),
            CreatedAt = DateTime.UtcNow.AddDays(-3),
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
            IsActive = true
        };
        context.DbContext.ExternalPatientProfileShares.Add(share);
        await context.DbContext.SaveChangesAsync();

        var status = await context.Service.GetStatusAsync("expired-token", CancellationToken.None);
        var response = Assert.IsType<ExternalShareStatusResponse>(Assert.IsType<OkObjectResult>(status.Result).Value);
        Assert.False(response.IsValid);
    }

    [Fact]
    public async Task SharedProfile_RejectsInvalidAccessToken()
    {
        using var context = new ExternalShareTestContext();
        var patient = context.AddPatient();
        await context.DbContext.SaveChangesAsync();

        context.DbContext.ExternalPatientProfileShares.Add(new HSCSAPI.Models.PatientProfile.ExternalPatientProfileShare
        {
            PatientId = patient.PatientId,
            DoctorEmail = "doctor@example.com",
            ShareToken = "active-token",
            ShareTokenHash = ExternalShareTestContext.HashForTest("active-token"),
            AccessSessionTokenHash = ExternalShareTestContext.HashForTest("valid-session-token"),
            AccessSessionExpiresAt = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(2),
            IsActive = true
        });
        await context.DbContext.SaveChangesAsync();

        var profile = await context.Service.GetSharedProfileAsync(
            "active-token",
            "wrong-session-token",
            CancellationToken.None);

        var response = Assert.IsType<ObjectResult>(profile.Result);
        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public void SharedProfileHtml_RendersVerificationAndFileViewerShells()
    {
        var page = SharedPatientProfileHtml.Page("demo-token");
        Assert.Contains("Shared Patient Profile", page);
        Assert.Contains("Email verification", page);
        Assert.Contains("/api/shared-profiles/", page);

        var fileId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var viewer = SharedPatientProfileHtml.FileViewer("demo-token", fileId, "access-token");
        Assert.Contains("Secure Medical File Viewer", viewer);
        Assert.Contains("/shared-profiles/demo-token/files/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee/content?accessToken=", viewer);
        Assert.Contains("title=\"Medical file preview\"", viewer);
        Assert.DoesNotContain("sandbox=", viewer);
    }
}

internal sealed class ExternalShareTestContext : IDisposable
{
    public ExternalShareTestContext()
    {
        ContentRootPath = Path.Combine(Path.GetTempPath(), "hscsapi-external-share-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ContentRootPath);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        DbContext = new AppDbContext(options);
        DbContext.Database.EnsureCreated();
        EmailService = new RecordingEmailService();
        Service = new ExternalProfileSharesService(
            DbContext,
            EmailService,
            new TestWebHostEnvironment(ContentRootPath));
    }

    public string ContentRootPath { get; }
    public AppDbContext DbContext { get; }
    public RecordingEmailService EmailService { get; }
    public ExternalProfileSharesService Service { get; }

    public Patient AddPatient()
    {
        var clinic = new Clinic
        {
            ClinicId = Guid.NewGuid(),
            Name = "Central Clinic",
            CreatedBySuperAdminUserId = Guid.NewGuid(),
            IsActive = true
        };
        var user = AddUser("patient@test.local", "Sarah Al-Hassan", clinic.ClinicId);
        var patient = new Patient
        {
            PatientId = user.Id,
            UserID = "CLINP000001",
            Gender = Gender.Female,
            BloodType = BloodType.OPositive,
            User = user
        };

        DbContext.Clinics.Add(clinic);
        DbContext.Patients.Add(patient);
        return patient;
    }

    public Doctor AddDoctor(Guid clinicId)
    {
        var user = AddUser("doctor@test.local", "Dr. Samer", clinicId);
        var doctor = new Doctor
        {
            DoctorId = user.Id,
            ProfessionalLicenseNumber = $"DOC-{Guid.NewGuid():N}",
            Specialty = DoctorSpecialty.Cardiology,
            User = user
        };
        DbContext.Doctors.Add(doctor);
        return doctor;
    }

    public MedicalFile AddMedicalFile(Guid patientId, Guid doctorId)
    {
        var fileId = Guid.NewGuid();
        var relativePath = Path.Combine("wwwroot", "shared-test-files", $"{fileId:N}.pdf").Replace('\\', '/');
        var physicalPath = Path.Combine(ContentRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);
        File.WriteAllText(physicalPath, "%PDF-1.4 test");

        var slot = new AvailabilitySlot
        {
            AvailabilitySlotId = Guid.NewGuid(),
            DoctorId = doctorId,
            SlotDate = new DateOnly(2026, 7, 1),
            DayOfWeek = DayOfWeek.Wednesday,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(9, 30),
            IsAvailable = false
        };
        var appointment = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            DoctorId = doctorId,
            PatientId = patientId,
            AvailabilitySlotId = slot.AvailabilitySlotId,
            AppointmentDate = slot.SlotDate,
            AppointmentTime = slot.StartTime,
            TreatmentName = "Cardiology",
            Notes = "Follow-up visit"
        };
        var medicalFile = new MedicalFile
        {
            MedicalFileId = fileId,
            AppointmentId = appointment.AppointmentId,
            UploadedByDoctorId = doctorId,
            FileType = MedicalFileType.Pdf,
            FilePath = relativePath,
            EncryptedChecksum = "checksum",
            FileSizeInBytes = 12,
            SeverityLevel = SeverityLevel.Low,
            DiagnosisCode = "E11.9",
            DiagnosisName = "Type 2 diabetes mellitus without complications",
            ActivityCode = "860975",
            ActivityName = "Metformin",
            UploadedAt = DateTime.UtcNow
        };

        DbContext.AvailabilitySlots.Add(slot);
        DbContext.Appointments.Add(appointment);
        DbContext.MedicalFiles.Add(medicalFile);
        DbContext.LabTestRequests.Add(new LabTestRequest
        {
            LabTestRequestId = Guid.NewGuid(),
            TestName = "HbA1c",
            PatientId = patientId,
            RequestedByDoctorId = doctorId,
            RequestedAt = DateTime.UtcNow.AddDays(-1),
            ResultMedicalFileId = fileId
        });
        return medicalFile;
    }

    public static ClaimsPrincipal PatientPrincipal(Guid patientId) => new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, patientId.ToString()),
            new Claim(ClaimTypes.Role, nameof(UserSystemRole.Patient))
        ],
        "Test"));

    public static string HashForTest(string value)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    public void Dispose()
    {
        DbContext.Dispose();
        if (Directory.Exists(ContentRootPath))
        {
            Directory.Delete(ContentRootPath, recursive: true);
        }
    }

    private static User AddUser(string email, string name, Guid clinicId)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Name = name,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            RegisteredAt = DateTime.UtcNow,
            ClinicId = clinicId,
            IsActive = true
        };
    }
}

internal sealed class RecordingEmailService : IEmailService
{
    public string LastTo { get; private set; } = string.Empty;
    public string LastSubject { get; private set; } = string.Empty;
    public string LastBody { get; private set; } = string.Empty;

    public Task SendEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        LastTo = toEmail;
        LastSubject = subject;
        LastBody = htmlBody;
        return Task.CompletedTask;
    }
}
