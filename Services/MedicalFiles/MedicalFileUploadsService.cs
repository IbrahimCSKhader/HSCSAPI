using System.Security.Claims;
using System.Security.Cryptography;
using HSCSAPI.Data;
using HSCSAPI.DTOs.MedicalFiles;
using HSCSAPI.DTOs.Standards;
using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Laboratory;
using HSCSAPI.Models.MedicalFiles;
using HSCSAPI.Models.Profiles;
using HSCSAPI.Models.Radiology;
using HSCSAPI.Services.Standards;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Services.MedicalFiles;

public class MedicalFileUploadsService : IMedicalFileUploadsService
{
    private const long MaxFileSize = 25 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, UploadFileRule> FileRulesByContentType =
        new Dictionary<string, UploadFileRule>(StringComparer.OrdinalIgnoreCase)
        {
            ["application/pdf"] = new(MedicalFileType.Pdf, ".pdf", IsPdf),
            ["image/jpeg"] = new(MedicalFileType.Jpeg, ".jpg", IsJpeg),
            ["image/jpg"] = new(MedicalFileType.Jpeg, ".jpg", IsJpeg),
            ["image/png"] = new(MedicalFileType.Png, ".png", IsPng)
        };

    private static readonly IReadOnlyDictionary<string, UploadFileRule> FileRulesByExtension =
        new Dictionary<string, UploadFileRule>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = new(MedicalFileType.Pdf, ".pdf", IsPdf),
            [".jpg"] = new(MedicalFileType.Jpeg, ".jpg", IsJpeg),
            [".jpeg"] = new(MedicalFileType.Jpeg, ".jpg", IsJpeg),
            [".png"] = new(MedicalFileType.Png, ".png", IsPng)
        };

    private readonly AppDbContext _dbContext;
    private readonly IStandardsService _standardsService;
    private readonly string _contentRootPath;
    private readonly string _medicalFilesRootPath;

    public MedicalFileUploadsService(
        AppDbContext dbContext,
        IStandardsService standardsService,
        IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _standardsService = standardsService;
        _contentRootPath = Path.GetFullPath(environment.ContentRootPath);
        _medicalFilesRootPath = Path.Combine(_contentRootPath, "wwwroot", "medical-files");
    }

    public Task<ActionResult<List<MedicalFileUploadCategoryResponse>>> GetUploadCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        List<MedicalFileUploadCategoryResponse> categories =
        [
            new()
            {
                Category = "LabTest",
                DisplayName = "Lab Test",
                RequiresStandardCode = true,
                StandardSource = "LOINC",
                StandardsEndpoint = "/api/Standards/lab-tests"
            },
            new()
            {
                Category = "ImagingTest",
                DisplayName = "Imaging Test",
                RequiresStandardCode = true,
                StandardSource = "Radiology Playbook",
                StandardsEndpoint = "/api/Standards/imaging-types"
            },
            new()
            {
                Category = "Prescription",
                DisplayName = "Prescription",
                RequiresStandardCode = false,
                StandardSource = "RxNorm",
                StandardsEndpoint = "/api/Standards/rxnorm/drugs"
            },
            new()
            {
                Category = "Other",
                DisplayName = "Other",
                RequiresStandardCode = false
            }
        ];

        return Task.FromResult<ActionResult<List<MedicalFileUploadCategoryResponse>>>(new OkObjectResult(categories));
    }

    public async Task<ActionResult<MedicalFileUploadResponse>> UploadMyMedicalFileAsync(
        CreateMedicalFileUploadRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var currentDoctorId = GetCurrentUserId(user);
        if (currentDoctorId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var doctor = await _dbContext.Doctors
            .AsNoTracking()
            .Include(item => item.User)
            .FirstOrDefaultAsync(item => item.DoctorId == currentDoctorId.Value, cancellationToken);
        if (doctor is null)
        {
            return new NotFoundObjectResult("Doctor profile not found.");
        }

        var patient = await FindPatientAsync(request.PatientId, cancellationToken);
        if (patient is null)
        {
            return new BadRequestObjectResult("Patient not found.");
        }

        var category = NormalizeCategory(request.Category);
        if (category is null)
        {
            return new BadRequestObjectResult("Invalid category. Use LabTest, ImagingTest, Prescription, or Other.");
        }

        var appointment = await ResolveAppointmentAsync(
            doctor.DoctorId,
            patient.PatientId,
            request.AppointmentId,
            cancellationToken);
        if (appointment is null)
        {
            return new BadRequestObjectResult(
                request.AppointmentId.HasValue
                    ? "Appointment not found for this doctor and patient."
                    : "No appointment was found for this doctor and patient. Provide appointmentId or create an appointment first.");
        }

        var file = request.File;
        if (file is null)
        {
            return new BadRequestObjectResult("Medical file is required.");
        }

        var fileRule = await ValidateFileAsync(file, cancellationToken);
        if (fileRule.Error is not null)
        {
            return new BadRequestObjectResult(fileRule.Error);
        }

        var resolvedStandard = await ResolveStandardAsync(category.Value, request, cancellationToken);
        if (resolvedStandard.Error is not null)
        {
            return new BadRequestObjectResult(resolvedStandard.Error);
        }

        var medicalFileId = Guid.NewGuid();
        var uploadedAt = DateTime.UtcNow;
        var savedFile = await SaveFileAsync(
            file,
            medicalFileId,
            fileRule.Rule!,
            resolvedStandard.Display,
            cancellationToken);

        var medicalFile = new MedicalFile
        {
            MedicalFileId = medicalFileId,
            AppointmentId = appointment.AppointmentId,
            UploadedByDoctorId = doctor.DoctorId,
            FileType = fileRule.Rule!.FileType,
            FilePath = savedFile.RelativePath,
            EncryptedChecksum = savedFile.Checksum,
            FileSizeInBytes = file.Length,
            SeverityLevel = SeverityLevel.Low,
            UploadedAt = uploadedAt
        };

        Guid? labTestRequestId = null;
        Guid? imagingTestRequestId = null;
        var notes = Clean(request.Notes);

        try
        {
            _dbContext.MedicalFiles.Add(medicalFile);

            if (category == UploadCategory.LabTest)
            {
                var labRequest = new LabTestRequest
                {
                    LabTestRequestId = Guid.NewGuid(),
                    TestName = resolvedStandard.Display!,
                    PatientId = patient.PatientId,
                    RequestedByDoctorId = doctor.DoctorId,
                    TestingClinicId = appointment.ClinicId,
                    LoincCode = resolvedStandard.Code,
                    Priority = "Routine",
                    ClinicalNotes = notes,
                    RequestedAt = uploadedAt,
                    ResultMedicalFileId = medicalFile.MedicalFileId
                };
                labTestRequestId = labRequest.LabTestRequestId;
                _dbContext.LabTestRequests.Add(labRequest);
            }
            else if (category == UploadCategory.ImagingTest)
            {
                var imagingRequest = new ImagingTestRequest
                {
                    ImagingTestRequestId = Guid.NewGuid(),
                    TestName = resolvedStandard.Display!,
                    PatientId = patient.PatientId,
                    RequestedByDoctorId = doctor.DoctorId,
                    RadiologyClinicId = appointment.ClinicId,
                    ImagingCode = resolvedStandard.Code,
                    BodyRegion = resolvedStandard.BodyRegion,
                    Priority = "Routine",
                    ClinicalNotes = notes,
                    RequestedAt = uploadedAt,
                    ResultMedicalFileId = medicalFile.MedicalFileId
                };
                imagingTestRequestId = imagingRequest.ImagingTestRequestId;
                _dbContext.ImagingTestRequests.Add(imagingRequest);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            DeleteSavedFile(savedFile.PhysicalPath);
            throw;
        }

        return new OkObjectResult(new MedicalFileUploadResponse
        {
            MedicalFileId = medicalFile.MedicalFileId,
            AppointmentId = medicalFile.AppointmentId,
            Category = ToCategoryDisplay(category.Value),
            StandardCode = resolvedStandard.Code,
            StandardDisplay = resolvedStandard.Display,
            Notes = notes,
            FileName = Path.GetFileName(medicalFile.FilePath),
            FileType = medicalFile.FileType.ToString(),
            FileSizeInBytes = medicalFile.FileSizeInBytes,
            UploadedAt = medicalFile.UploadedAt,
            PatientId = patient.PatientId,
            PatientUserId = patient.UserID,
            PatientName = patient.User.Name,
            UploadedByDoctorId = doctor.DoctorId,
            UploadedByDoctorName = doctor.User.Name,
            LabTestRequestId = labTestRequestId,
            ImagingTestRequestId = imagingTestRequestId,
            FileUrl = $"/api/Doctors/me/medical-file-uploads/{medicalFile.MedicalFileId}/download"
        });
    }

    private async Task<Patient?> FindPatientAsync(string patientId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(patientId))
        {
            return null;
        }

        var trimmedPatientId = patientId.Trim();
        var query = _dbContext.Patients
            .Include(patient => patient.User)
            .AsQueryable();

        return Guid.TryParse(trimmedPatientId, out var patientGuid)
            ? await query.FirstOrDefaultAsync(patient => patient.PatientId == patientGuid, cancellationToken)
            : await query.FirstOrDefaultAsync(patient => patient.UserID == trimmedPatientId, cancellationToken);
    }

    private async Task<AppointmentUploadInfo?> ResolveAppointmentAsync(
        Guid doctorId,
        Guid patientId,
        Guid? appointmentId,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Appointments
            .AsNoTracking()
            .Where(appointment => appointment.DoctorId == doctorId
                && appointment.PatientId == patientId);

        if (appointmentId.HasValue)
        {
            query = query.Where(appointment => appointment.AppointmentId == appointmentId.Value);
        }

        return await query
            .OrderByDescending(appointment => appointment.AppointmentDate)
            .ThenByDescending(appointment => appointment.AppointmentTime)
            .Select(appointment => new AppointmentUploadInfo(
                appointment.AppointmentId,
                appointment.Doctor.User.ClinicId))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<ResolvedUploadStandard> ResolveStandardAsync(
        UploadCategory category,
        CreateMedicalFileUploadRequest request,
        CancellationToken cancellationToken)
    {
        return category switch
        {
            UploadCategory.LabTest => await ResolveLabStandardAsync(request.StandardCode, cancellationToken),
            UploadCategory.ImagingTest => await ResolveImagingStandardAsync(request.StandardCode, cancellationToken),
            UploadCategory.Prescription => ResolveOptionalStandard(request.StandardCode, request.DisplayName, "Prescription"),
            _ => ResolveOptionalStandard(request.StandardCode, request.DisplayName, "Medical file")
        };
    }

    private async Task<ResolvedUploadStandard> ResolveLabStandardAsync(
        string? standardCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(standardCode))
        {
            return ResolvedUploadStandard.Fail("LOINC code is required for Lab Test uploads.");
        }

        var loinc = await _standardsService.GetLoincByCodeAsync(standardCode, cancellationToken);
        return loinc is null
            ? ResolvedUploadStandard.Fail("LOINC code not found.")
            : ResolvedUploadStandard.Success(loinc.Code, loinc.Display);
    }

    private async Task<ResolvedUploadStandard> ResolveImagingStandardAsync(
        string? standardCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(standardCode))
        {
            return ResolvedUploadStandard.Fail("Imaging code is required for Imaging Test uploads.");
        }

        var trimmedCode = standardCode.Trim();
        var playbook = await _standardsService.GetRadiologyPlaybookByRpidAsync(trimmedCode, cancellationToken);
        if (playbook is not null)
        {
            return ResolvedUploadStandard.Success(playbook.Rpid, playbook.Display, playbook.BodyRegion);
        }

        var imagingTypes = await _standardsService.GetImagingTypesAsync(trimmedCode, cancellationToken);
        var imagingType = imagingTypes.FirstOrDefault(item =>
            item.Code.Equals(trimmedCode, StringComparison.OrdinalIgnoreCase)
            || item.Modality.Equals(trimmedCode, StringComparison.OrdinalIgnoreCase)
            || item.Display.Equals(trimmedCode, StringComparison.OrdinalIgnoreCase));

        return imagingType is null
            ? ResolvedUploadStandard.Fail("Imaging type not found.")
            : ResolvedUploadStandard.Success(imagingType.Code, imagingType.Display);
    }

    private static ResolvedUploadStandard ResolveOptionalStandard(
        string? standardCode,
        string? displayName,
        string fallbackDisplay)
    {
        var code = Clean(standardCode);
        var display = Clean(displayName) ?? code ?? fallbackDisplay;
        return ResolvedUploadStandard.Success(code, display);
    }

    private async Task<UploadFileValidationResult> ValidateFileAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length <= 0)
        {
            return UploadFileValidationResult.Fail("The uploaded file is empty.");
        }

        if (file.Length > MaxFileSize)
        {
            return UploadFileValidationResult.Fail("Medical files cannot exceed 25 MB.");
        }

        var rule = ResolveFileRule(file);
        if (rule is null)
        {
            return UploadFileValidationResult.Fail("Unsupported file type. Use PDF, JPEG, or PNG.");
        }

        var header = new byte[16];
        await using var stream = file.OpenReadStream();
        var bytesRead = await stream.ReadAsync(header, cancellationToken);
        if (!rule.SignatureValidator(header.AsSpan(0, bytesRead)))
        {
            return UploadFileValidationResult.Fail("The file content does not match its declared type.");
        }

        return UploadFileValidationResult.Success(rule);
    }

    private static UploadFileRule? ResolveFileRule(IFormFile file)
    {
        var contentType = file.ContentType?.Split(';', 2)[0].Trim();
        if (!string.IsNullOrWhiteSpace(contentType)
            && FileRulesByContentType.TryGetValue(contentType, out var contentTypeRule))
        {
            return contentTypeRule;
        }

        var extension = Path.GetExtension(file.FileName);
        return string.IsNullOrWhiteSpace(extension)
            ? null
            : FileRulesByExtension.GetValueOrDefault(extension);
    }

    private async Task<SavedMedicalUploadFile> SaveFileAsync(
        IFormFile file,
        Guid medicalFileId,
        UploadFileRule fileRule,
        string? displayName,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_medicalFilesRootPath);

        var uploadDirectory = Path.Combine(_medicalFilesRootPath, medicalFileId.ToString("N"));
        Directory.CreateDirectory(uploadDirectory);

        var fileName = BuildStoredFileName(displayName, file.FileName, fileRule.Extension);
        var physicalPath = Path.Combine(uploadDirectory, fileName);

        try
        {
            await using (var destination = new FileStream(
                physicalPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await file.CopyToAsync(destination, cancellationToken);
            }

            await using var source = new FileStream(
                physicalPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var sha256 = SHA256.Create();
            var checksum = Convert.ToHexString(await sha256.ComputeHashAsync(source, cancellationToken));

            var relativePath = Path.Combine(
                    "wwwroot",
                    "medical-files",
                    medicalFileId.ToString("N"),
                    fileName)
                .Replace('\\', '/');

            return new SavedMedicalUploadFile(relativePath, physicalPath, checksum);
        }
        catch
        {
            DeleteSavedFile(physicalPath);
            throw;
        }
    }

    private static string BuildStoredFileName(string? displayName, string originalFileName, string extension)
    {
        var originalBaseName = Path.GetFileNameWithoutExtension(originalFileName);
        var baseName = MakeSafeFileName(Clean(displayName) ?? originalBaseName);
        return $"{baseName}{extension}";
    }

    private static string MakeSafeFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "medical-file";
        }

        var invalidCharacters = Path.GetInvalidFileNameChars();
        var cleaned = new string(value
            .Select(character => invalidCharacters.Contains(character) ? ' ' : character)
            .ToArray());
        cleaned = string.Join(" ", cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return "medical-file";
        }

        return cleaned.Length > 120 ? cleaned[..120].TrimEnd() : cleaned;
    }

    private void DeleteSavedFile(string physicalPath)
    {
        if (File.Exists(physicalPath))
        {
            File.Delete(physicalPath);
        }

        var directory = Path.GetDirectoryName(physicalPath);
        if (!string.IsNullOrWhiteSpace(directory)
            && Directory.Exists(directory)
            && !Directory.EnumerateFileSystemEntries(directory).Any())
        {
            Directory.Delete(directory);
        }
    }

    private static UploadCategory? NormalizeCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return null;
        }

        var normalized = category
            .Trim()
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

        return normalized switch
        {
            "lab" or "labtest" => UploadCategory.LabTest,
            "imaging" or "imagingtest" or "radiology" => UploadCategory.ImagingTest,
            "prescription" or "rx" => UploadCategory.Prescription,
            "other" => UploadCategory.Other,
            _ => null
        };
    }

    private static string ToCategoryDisplay(UploadCategory category)
    {
        return category switch
        {
            UploadCategory.LabTest => "Lab Test",
            UploadCategory.ImagingTest => "Imaging Test",
            UploadCategory.Prescription => "Prescription",
            _ => "Other"
        };
    }

    private static Guid? GetCurrentUserId(ClaimsPrincipal user)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userId, out var parsedUserId) ? parsedUserId : null;
    }

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static bool IsPdf(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 4 && bytes[..4].SequenceEqual("%PDF"u8);

    private static bool IsJpeg(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;

    private static bool IsPng(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

    private enum UploadCategory
    {
        LabTest,
        ImagingTest,
        Prescription,
        Other
    }

    private sealed record AppointmentUploadInfo(Guid AppointmentId, Guid? ClinicId);

    private sealed record UploadFileRule(
        MedicalFileType FileType,
        string Extension,
        FileSignatureValidator SignatureValidator);

    private sealed record UploadFileValidationResult(UploadFileRule? Rule, string? Error)
    {
        public static UploadFileValidationResult Success(UploadFileRule rule) => new(rule, null);
        public static UploadFileValidationResult Fail(string error) => new(null, error);
    }

    private sealed record ResolvedUploadStandard(
        string? Code,
        string? Display,
        string? BodyRegion,
        string? Error)
    {
        public static ResolvedUploadStandard Success(string? code, string? display, string? bodyRegion = null) =>
            new(code, display, bodyRegion, null);

        public static ResolvedUploadStandard Fail(string error) =>
            new(null, null, null, error);
    }

    private sealed record SavedMedicalUploadFile(
        string RelativePath,
        string PhysicalPath,
        string Checksum);

    private delegate bool FileSignatureValidator(ReadOnlySpan<byte> bytes);
}
