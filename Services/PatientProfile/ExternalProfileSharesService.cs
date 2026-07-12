using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HSCSAPI.Data;
using HSCSAPI.DTOs.PatientProfile;
using HSCSAPI.Models.MedicalFiles;
using HSCSAPI.Models.PatientProfile;
using HSCSAPI.Services.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Services.PatientProfile;

public class ExternalProfileSharesService : IExternalProfileSharesService
{
    private static readonly TimeSpan ShareLifetime = TimeSpan.FromDays(2);
    private static readonly TimeSpan VerificationCodeLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan AccessSessionLifetime = TimeSpan.FromHours(8);

    private readonly AppDbContext _dbContext;
    private readonly IEmailService _emailService;
    private readonly string _contentRootPath;
    private readonly FileExtensionContentTypeProvider _contentTypeProvider = new();

    public ExternalProfileSharesService(
        AppDbContext dbContext,
        IEmailService emailService,
        IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _emailService = emailService;
        _contentRootPath = Path.GetFullPath(environment.ContentRootPath);
    }

    public async Task<ActionResult<ExternalProfileShareResponse>> CreateShareAsync(
        CreateExternalProfileShareRequest request,
        ClaimsPrincipal user,
        string publicBaseUrl,
        CancellationToken cancellationToken = default)
    {
        var patientId = GetCurrentUserId(user);
        if (patientId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        if (!TryNormalizeEmail(request.DoctorEmail, out var normalizedEmail, out var emailError))
        {
            return new BadRequestObjectResult(emailError);
        }

        var patient = await _dbContext.Patients
            .AsNoTracking()
            .Where(x => x.PatientId == patientId.Value && x.User.IsActive)
            .Select(x => new { x.PatientId, x.User.Name })
            .FirstOrDefaultAsync(cancellationToken);
        if (patient is null)
        {
            return new NotFoundObjectResult("Patient profile not found.");
        }

        var now = DateTime.UtcNow;
        var rawToken = GenerateToken();
        var share = new ExternalPatientProfileShare
        {
            PatientId = patient.PatientId,
            DoctorEmail = normalizedEmail,
            DoctorName = NormalizeOptional(request.DoctorName),
            ShareToken = rawToken,
            ShareTokenHash = Hash(rawToken),
            CreatedAt = now,
            ExpiresAt = now.Add(ShareLifetime),
            IsActive = true
        };

        _dbContext.ExternalPatientProfileShares.Add(share);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var shareUrl = BuildShareUrl(publicBaseUrl, rawToken);
        await SendShareCreatedEmailAsync(normalizedEmail, patient.Name, share.DoctorName, shareUrl, share.ExpiresAt, cancellationToken);

        return new CreatedResult(shareUrl, MapShare(share, publicBaseUrl, rawToken));
    }

    public async Task<ActionResult<List<ExternalProfileShareResponse>>> GetMySharesAsync(
        ClaimsPrincipal user,
        string publicBaseUrl,
        CancellationToken cancellationToken = default)
    {
        var patientId = GetCurrentUserId(user);
        if (patientId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var shares = await _dbContext.ExternalPatientProfileShares
            .AsNoTracking()
            .Where(x => x.PatientId == patientId.Value)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ExternalProfileShareResponse
            {
                ShareId = x.ExternalPatientProfileShareId,
                DoctorEmail = x.DoctorEmail,
                DoctorName = x.DoctorName,
                ShareUrl = BuildShareUrl(publicBaseUrl, x.ShareToken),
                CreatedAt = x.CreatedAt,
                ExpiresAt = x.ExpiresAt,
                IsActive = x.IsActive && x.ExpiresAt > DateTime.UtcNow
            })
            .ToListAsync(cancellationToken);

        return new OkObjectResult(shares);
    }

    public async Task<IActionResult> DeactivateShareAsync(
        Guid shareId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var patientId = GetCurrentUserId(user);
        if (patientId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var share = await _dbContext.ExternalPatientProfileShares
            .FirstOrDefaultAsync(
                x => x.ExternalPatientProfileShareId == shareId && x.PatientId == patientId.Value,
                cancellationToken);
        if (share is null)
        {
            return new NotFoundObjectResult("Share not found.");
        }

        share.IsActive = false;
        share.AccessSessionTokenHash = null;
        share.AccessSessionExpiresAt = null;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new NoContentResult();
    }

    public async Task<ActionResult<ExternalShareStatusResponse>> GetStatusAsync(
        string shareToken,
        CancellationToken cancellationToken = default)
    {
        var share = await GetShareByTokenAsync(shareToken, tracking: false, cancellationToken);
        if (share is null || !IsShareOpen(share))
        {
            return new OkObjectResult(new ExternalShareStatusResponse
            {
                IsValid = false,
                Message = "This patient profile link is invalid or expired."
            });
        }

        return new OkObjectResult(new ExternalShareStatusResponse
        {
            IsValid = true,
            Message = "Email verification is required before viewing this profile.",
            ExpiresAt = share.ExpiresAt,
            DoctorEmailHint = MaskEmail(share.DoctorEmail)
        });
    }

    public async Task<IActionResult> SendVerificationCodeAsync(
        string shareToken,
        ExternalShareEmailRequest request,
        string publicBaseUrl,
        CancellationToken cancellationToken = default)
    {
        var share = await GetShareByTokenAsync(shareToken, tracking: true, cancellationToken);
        if (share is null || !IsShareOpen(share))
        {
            return new NotFoundObjectResult("This patient profile link is invalid or expired.");
        }

        if (!TryNormalizeEmail(request.Email, out var normalizedEmail, out var emailError))
        {
            return new BadRequestObjectResult(emailError);
        }

        if (!share.DoctorEmail.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            return new ObjectResult("This email is not approved for this patient profile share.")
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }

        var code = GenerateNumericCode();
        share.VerificationCodeHash = Hash($"{normalizedEmail}:{code}");
        share.VerificationCodeExpiresAt = DateTime.UtcNow.Add(VerificationCodeLifetime);
        share.LastCodeSentAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await SendVerificationEmailAsync(normalizedEmail, code, BuildShareUrl(publicBaseUrl, shareToken), share.ExpiresAt, cancellationToken);
        return new OkObjectResult("Verification code sent.");
    }

    public async Task<ActionResult<ExternalShareVerifyResponse>> VerifyEmailAsync(
        string shareToken,
        ExternalShareVerifyRequest request,
        CancellationToken cancellationToken = default)
    {
        var share = await GetShareByTokenAsync(shareToken, tracking: true, cancellationToken);
        if (share is null || !IsShareOpen(share))
        {
            return new NotFoundObjectResult("This patient profile link is invalid or expired.");
        }

        if (!TryNormalizeEmail(request.Email, out var normalizedEmail, out var emailError))
        {
            return new BadRequestObjectResult(emailError);
        }

        if (!share.DoctorEmail.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            return new ObjectResult("This email is not approved for this patient profile share.")
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }

        if (string.IsNullOrWhiteSpace(request.Code)
            || share.VerificationCodeHash is null
            || !share.VerificationCodeExpiresAt.HasValue
            || share.VerificationCodeExpiresAt.Value < DateTime.UtcNow
            || !FixedTimeEquals(share.VerificationCodeHash, Hash($"{normalizedEmail}:{request.Code.Trim()}")))
        {
            return new BadRequestObjectResult("Invalid or expired verification code.");
        }

        var sessionToken = GenerateToken();
        var sessionExpiresAt = Min(DateTime.UtcNow.Add(AccessSessionLifetime), share.ExpiresAt);
        share.AccessSessionTokenHash = Hash(sessionToken);
        share.AccessSessionExpiresAt = sessionExpiresAt;
        share.VerificationCodeHash = null;
        share.VerificationCodeExpiresAt = null;
        share.LastAccessedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new OkObjectResult(new ExternalShareVerifyResponse
        {
            AccessToken = sessionToken,
            ExpiresAt = sessionExpiresAt
        });
    }

    public async Task<ActionResult<ExternalSharedPatientProfileResponse>> GetSharedProfileAsync(
        string shareToken,
        string? accessToken,
        CancellationToken cancellationToken = default)
    {
        var share = await ValidateAccessAsync(shareToken, accessToken, tracking: true, cancellationToken);
        if (share.Result is not null)
        {
            return share.Result;
        }

        share.Value!.LastAccessedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var profile = await BuildSharedProfileAsync(share.Value, shareToken, accessToken!, cancellationToken);
        return new OkObjectResult(profile);
    }

    public async Task<ExternalSharedFileView> GetFileForViewingAsync(
        string shareToken,
        Guid medicalFileId,
        string? accessToken,
        CancellationToken cancellationToken = default)
    {
        var share = await ValidateAccessAsync(shareToken, accessToken, tracking: false, cancellationToken);
        if (share.Result is not null)
        {
            throw new UnauthorizedAccessException("Share access is not verified or has expired.");
        }

        var file = await _dbContext.MedicalFiles
            .AsNoTracking()
            .Where(x => x.MedicalFileId == medicalFileId && x.Appointment.PatientId == share.Value!.PatientId)
            .Select(x => new
            {
                x.FilePath,
                x.FileType
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (file is null)
        {
            throw new KeyNotFoundException("Medical file not found.");
        }

        var physicalPath = ResolvePhysicalFilePath(file.FilePath);
        if (!File.Exists(physicalPath))
        {
            throw new KeyNotFoundException("Medical file is not available on this server.");
        }

        if (!_contentTypeProvider.TryGetContentType(physicalPath, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        return new ExternalSharedFileView(physicalPath, contentType, Path.GetFileName(physicalPath));
    }

    private async Task<ExternalSharedPatientProfileResponse> BuildSharedProfileAsync(
        ExternalPatientProfileShare share,
        string shareToken,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var patient = await _dbContext.Patients
            .AsNoTracking()
            .Where(x => x.PatientId == share.PatientId)
            .Select(x => new ExternalSharedPatientSummary
            {
                PatientUserId = x.UserID,
                Name = x.User.Name,
                DateOfBirth = x.User.DateOfBirth,
                Gender = x.Gender.ToString(),
                BloodType = x.BloodType.HasValue ? x.BloodType.Value.ToString() : null,
                ClinicName = x.User.Clinic != null ? x.User.Clinic.Name : null
            })
            .FirstAsync(cancellationToken);

        var medicalFiles = await _dbContext.MedicalFiles
            .AsNoTracking()
            .Where(file => file.Appointment.PatientId == share.PatientId)
            .OrderByDescending(file => file.UploadedAt)
            .Select(file => new MedicalFileProjection
            {
                MedicalFileId = file.MedicalFileId,
                FilePath = file.FilePath,
                FileType = file.FileType.ToString(),
                FileSizeInBytes = file.FileSizeInBytes,
                UploadedAt = file.UploadedAt,
                DoctorName = file.UploadedByDoctor.User.Name,
                AppointmentNotes = file.Appointment.Notes,
                DiagnosisCode = file.DiagnosisCode,
                DiagnosisName = file.DiagnosisName,
                ActivityCode = file.ActivityCode,
                ActivityName = file.ActivityName,
                LabTestName = file.LabTestRequestsAsResult.OrderBy(x => x.TestName).Select(x => x.TestName).FirstOrDefault(),
                ImagingTestName = file.ImagingTestRequestsAsResult.OrderBy(x => x.TestName).Select(x => x.TestName).FirstOrDefault(),
                HasLabResult = file.LabTestRequestsAsResult.Any(),
                HasImagingResult = file.ImagingTestRequestsAsResult.Any()
            })
            .ToListAsync(cancellationToken);

        var labTests = await _dbContext.LabTestRequests
            .AsNoTracking()
            .Where(x => x.PatientId == share.PatientId
                || (x.ResultMedicalFile != null && x.ResultMedicalFile.Appointment.PatientId == share.PatientId))
            .OrderByDescending(x => x.RequestedAt)
            .Select(x => new ExternalSharedTestItem
            {
                RequestId = x.LabTestRequestId,
                TestType = "Lab",
                TestName = x.TestName,
                Status = x.ResultMedicalFileId.HasValue || x.StructuredResult != null ? "Completed" : "Pending",
                RequestedAt = x.RequestedAt,
                ResultAt = x.StructuredResult != null
                    ? x.StructuredResult.CompletedAt
                    : x.ResultMedicalFile != null
                        ? x.ResultMedicalFile.UploadedAt
                        : null,
                ResultSummary = x.StructuredResult != null
                    ? x.StructuredResult.Comments
                    : x.ClinicalNotes,
                DoctorName = x.RequestedByDoctor != null ? x.RequestedByDoctor.User.Name : null
            })
            .ToListAsync(cancellationToken);

        var imagingTests = await _dbContext.ImagingTestRequests
            .AsNoTracking()
            .Where(x => x.PatientId == share.PatientId
                || (x.ResultMedicalFile != null && x.ResultMedicalFile.Appointment.PatientId == share.PatientId))
            .OrderByDescending(x => x.RequestedAt)
            .Select(x => new ExternalSharedTestItem
            {
                RequestId = x.ImagingTestRequestId,
                TestType = "Imaging",
                TestName = x.TestName,
                Status = x.ResultMedicalFileId.HasValue ? "Completed" : "Pending",
                RequestedAt = x.RequestedAt,
                ResultAt = x.ResultMedicalFile != null ? x.ResultMedicalFile.UploadedAt : null,
                ResultSummary = x.ResultSummary ?? x.ClinicalNotes,
                DoctorName = x.RequestedByDoctor != null ? x.RequestedByDoctor.User.Name : null
            })
            .ToListAsync(cancellationToken);

        var appointments = await _dbContext.Appointments
            .AsNoTracking()
            .Where(x => x.PatientId == share.PatientId)
            .OrderByDescending(x => x.AppointmentDate)
            .ThenByDescending(x => x.AppointmentTime)
            .Select(x => new ExternalSharedTimelineItem
            {
                OccurredAt = x.AppointmentDate.ToDateTime(x.AppointmentTime),
                Type = "Appointment",
                Title = x.TreatmentName ?? x.Doctor.Specialty.ToString(),
                Description = x.Notes,
                DoctorName = x.Doctor.User.Name
            })
            .ToListAsync(cancellationToken);

        var files = medicalFiles.Select(file => new ExternalSharedMedicalFileItem
        {
            MedicalFileId = file.MedicalFileId,
            Title = GetMedicalFileTitle(file),
            RecordType = GetMedicalFileRecordType(file),
            FileType = file.FileType,
            FileSizeInBytes = file.FileSizeInBytes,
            UploadedAt = file.UploadedAt,
            DoctorName = file.DoctorName,
            ViewerUrl = $"/shared-profiles/{Uri.EscapeDataString(shareToken)}/files/{file.MedicalFileId}/viewer?accessToken={Uri.EscapeDataString(accessToken)}"
        }).ToList();

        var timeline = appointments
            .Concat(medicalFiles.Select(file => new ExternalSharedTimelineItem
            {
                OccurredAt = file.UploadedAt,
                Type = GetMedicalFileRecordType(file),
                Title = GetMedicalFileTitle(file),
                Description = file.AppointmentNotes,
                DoctorName = file.DoctorName
            }))
            .Concat(labTests.Select(test => new ExternalSharedTimelineItem
            {
                OccurredAt = test.ResultAt ?? test.RequestedAt,
                Type = "Lab",
                Title = test.TestName,
                Description = test.ResultSummary,
                DoctorName = test.DoctorName
            }))
            .Concat(imagingTests.Select(test => new ExternalSharedTimelineItem
            {
                OccurredAt = test.ResultAt ?? test.RequestedAt,
                Type = "Imaging",
                Title = test.TestName,
                Description = test.ResultSummary,
                DoctorName = test.DoctorName
            }))
            .OrderByDescending(x => x.OccurredAt)
            .ToList();

        return new ExternalSharedPatientProfileResponse
        {
            Patient = patient,
            ShareExpiresAt = share.ExpiresAt,
            Timeline = timeline,
            Diagnoses = medicalFiles
                .Where(x => !string.IsNullOrWhiteSpace(x.DiagnosisName) || !string.IsNullOrWhiteSpace(x.DiagnosisCode))
                .Select(x => new ExternalSharedDiagnosisItem
                {
                    DiagnosisCode = x.DiagnosisCode,
                    DiagnosisName = x.DiagnosisName ?? x.DiagnosisCode ?? "Diagnosis",
                    RecordedAt = x.UploadedAt,
                    DoctorName = x.DoctorName,
                    SourceFileTitle = GetMedicalFileTitle(x)
                })
                .OrderByDescending(x => x.RecordedAt)
                .ToList(),
            Medications = medicalFiles
                .Where(x => !string.IsNullOrWhiteSpace(x.ActivityName) || !string.IsNullOrWhiteSpace(x.ActivityCode))
                .Select(x => new ExternalSharedMedicationItem
                {
                    ActivityCode = x.ActivityCode,
                    ActivityName = x.ActivityName ?? x.ActivityCode ?? "Medication",
                    RecordedAt = x.UploadedAt,
                    DoctorName = x.DoctorName,
                    DiagnosisName = x.DiagnosisName
                })
                .OrderByDescending(x => x.RecordedAt)
                .ToList(),
            Tests = labTests.Concat(imagingTests)
                .OrderByDescending(x => x.ResultAt ?? x.RequestedAt)
                .ToList(),
            Files = files
        };
    }

    private async Task<ExternalPatientProfileShare?> GetShareByTokenAsync(
        string shareToken,
        bool tracking,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(shareToken))
        {
            return null;
        }

        var tokenHash = Hash(shareToken.Trim());
        var query = _dbContext.ExternalPatientProfileShares.AsQueryable();
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(x => x.ShareTokenHash == tokenHash, cancellationToken);
    }

    private async Task<(ExternalPatientProfileShare? Value, ActionResult? Result)> ValidateAccessAsync(
        string shareToken,
        string? accessToken,
        bool tracking,
        CancellationToken cancellationToken)
    {
        var share = await GetShareByTokenAsync(shareToken, tracking, cancellationToken);
        if (share is null || !IsShareOpen(share))
        {
            return (null, new NotFoundObjectResult("This patient profile link is invalid or expired."));
        }

        if (string.IsNullOrWhiteSpace(accessToken)
            || share.AccessSessionTokenHash is null
            || !share.AccessSessionExpiresAt.HasValue
            || share.AccessSessionExpiresAt.Value < DateTime.UtcNow
            || !FixedTimeEquals(share.AccessSessionTokenHash, Hash(accessToken.Trim())))
        {
            return (null, new ObjectResult("Email verification is required.")
            {
                StatusCode = StatusCodes.Status401Unauthorized
            });
        }

        return (share, null);
    }

    private static ExternalProfileShareResponse MapShare(
        ExternalPatientProfileShare share,
        string publicBaseUrl,
        string rawToken)
    {
        return new ExternalProfileShareResponse
        {
            ShareId = share.ExternalPatientProfileShareId,
            DoctorEmail = share.DoctorEmail,
            DoctorName = share.DoctorName,
            ShareUrl = BuildShareUrl(publicBaseUrl, rawToken),
            CreatedAt = share.CreatedAt,
            ExpiresAt = share.ExpiresAt,
            IsActive = share.IsActive && share.ExpiresAt > DateTime.UtcNow
        };
    }

    private static bool IsShareOpen(ExternalPatientProfileShare share) =>
        share.IsActive && share.ExpiresAt > DateTime.UtcNow;

    private static string BuildShareUrl(string publicBaseUrl, string shareToken) =>
        $"{publicBaseUrl.TrimEnd('/')}/shared-profiles/{Uri.EscapeDataString(shareToken)}";

    private static string GetMedicalFileTitle(MedicalFileProjection file) =>
        file.LabTestName
        ?? file.ImagingTestName
        ?? file.DiagnosisName
        ?? file.ActivityName
        ?? Path.GetFileNameWithoutExtension(file.FilePath)
        ?? "Medical file";

    private static string GetMedicalFileRecordType(MedicalFileProjection file)
    {
        if (file.HasLabResult)
        {
            return "LabResult";
        }

        if (file.HasImagingResult)
        {
            return "ImagingResult";
        }

        if (!string.IsNullOrWhiteSpace(file.ActivityName) || !string.IsNullOrWhiteSpace(file.ActivityCode))
        {
            return "Prescription";
        }

        return "MedicalFile";
    }

    private string ResolvePhysicalFilePath(string filePath)
    {
        if (Path.IsPathRooted(filePath))
        {
            return filePath;
        }

        var relativePath = filePath.TrimStart(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar,
            '/',
            '\\');

        return Path.GetFullPath(Path.Combine(_contentRootPath, relativePath));
    }

    private async Task SendShareCreatedEmailAsync(
        string email,
        string patientName,
        string? doctorName,
        string shareUrl,
        DateTime expiresAt,
        CancellationToken cancellationToken)
    {
        var greeting = string.IsNullOrWhiteSpace(doctorName) ? "Hello" : $"Hello {doctorName}";
        var body = $"""
            <p>{Html(greeting)},</p>
            <p>{Html(patientName)} shared a read-only medical profile with you.</p>
            <p>This access is temporary and expires at <strong>{expiresAt:yyyy-MM-dd HH:mm} UTC</strong>.</p>
            <p><a href="{Html(shareUrl)}">Open shared patient profile</a></p>
            <p>You will need to verify this approved email address before viewing the profile.</p>
            """;

        await _emailService.SendEmailAsync(email, "Shared patient profile access", body, cancellationToken);
    }

    private async Task SendVerificationEmailAsync(
        string email,
        string code,
        string shareUrl,
        DateTime expiresAt,
        CancellationToken cancellationToken)
    {
        var body = $"""
            <p>Your verification code is:</p>
            <h2 style="letter-spacing:3px">{Html(code)}</h2>
            <p>This code expires in 15 minutes. The patient profile share expires at <strong>{expiresAt:yyyy-MM-dd HH:mm} UTC</strong>.</p>
            <p><a href="{Html(shareUrl)}">Return to shared profile</a></p>
            """;

        await _emailService.SendEmailAsync(email, "Patient profile verification code", body, cancellationToken);
    }

    private static bool TryNormalizeEmail(string? email, out string normalizedEmail, out string error)
    {
        normalizedEmail = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(email))
        {
            error = "Email is required.";
            return false;
        }

        try
        {
            var address = new MailAddress(email.Trim());
            normalizedEmail = address.Address.ToLowerInvariant();
            return true;
        }
        catch
        {
            error = "A valid email address is required.";
            return false;
        }
    }

    private static Guid? GetCurrentUserId(ClaimsPrincipal user)
    {
        var claim = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var userId) ? userId : null;
    }

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string GenerateNumericCode()
    {
        var value = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return value.ToString("D6");
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length
            && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static DateTime Min(DateTime first, DateTime second) => first <= second ? first : second;

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string MaskEmail(string email)
    {
        var parts = email.Split('@', 2);
        if (parts.Length != 2)
        {
            return "***";
        }

        var local = parts[0];
        var maskedLocal = local.Length <= 2
            ? $"{local[0]}***"
            : $"{local[0]}***{local[^1]}";
        return $"{maskedLocal}@{parts[1]}";
    }

    private static string Html(string? value) => System.Net.WebUtility.HtmlEncode(value ?? string.Empty);

    private sealed class MedicalFileProjection
    {
        public Guid MedicalFileId { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
        public long FileSizeInBytes { get; set; }
        public DateTime UploadedAt { get; set; }
        public string? DoctorName { get; set; }
        public string? AppointmentNotes { get; set; }
        public string? DiagnosisCode { get; set; }
        public string? DiagnosisName { get; set; }
        public string? ActivityCode { get; set; }
        public string? ActivityName { get; set; }
        public string? LabTestName { get; set; }
        public string? ImagingTestName { get; set; }
        public bool HasLabResult { get; set; }
        public bool HasImagingResult { get; set; }
    }
}
