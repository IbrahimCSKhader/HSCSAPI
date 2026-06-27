using System.Security.Claims;
using HSCSAPI.Data;
using HSCSAPI.DTOs.Laboratory;
using HSCSAPI.Models.Laboratory;
using HSCSAPI.Models.MedicalFiles;
using HSCSAPI.Models.Profiles;
using HSCSAPI.Services.Standards;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Services.Laboratory;

public class LabTestRequestsService : ILabTestRequestsService
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly AppDbContext _dbContext;
    private readonly IStandardsService _standardsService;
    private readonly IWebHostEnvironment _environment;
    private readonly FileExtensionContentTypeProvider _contentTypeProvider = new();

    public LabTestRequestsService(
        AppDbContext dbContext,
        IStandardsService standardsService,
        IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _standardsService = standardsService;
        _environment = environment;
    }

    public async Task<ActionResult<LabTestRequestsResponse>> GetMyRequestsAsync(
        string? status,
        string? patientId,
        int page,
        int pageSize,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var currentDoctorId = GetCurrentUserId(user);
        if (currentDoctorId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var (normalizedPage, normalizedPageSize) = NormalizePaging(page, pageSize);
        var query = BuildReadableRequestsQuery(currentDoctorId.Value);

        if (!ApplyStatusFilter(status, ref query, out var statusError))
        {
            return new BadRequestObjectResult(statusError);
        }

        if (!string.IsNullOrWhiteSpace(patientId))
        {
            var trimmedPatientId = patientId.Trim();
            query = query.Where(request =>
                (request.Patient != null && request.Patient.UserID == trimmedPatientId)
                || (request.ResultMedicalFile != null
                    && request.ResultMedicalFile.Appointment.Patient.UserID == trimmedPatientId));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var requests = await query
            .OrderByDescending(request => request.RequestedAt)
            .ThenByDescending(request => request.LabTestRequestId)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        var response = new LabTestRequestsResponse
        {
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount,
            TotalPages = CalculateTotalPages(totalCount, normalizedPageSize),
            Items = requests.Select(MapToResponse).ToList()
        };

        return new OkObjectResult(response);
    }

    public async Task<ActionResult<LabTestRequestResponse>> GetMyRequestAsync(
        Guid labTestRequestId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var currentDoctorId = GetCurrentUserId(user);
        if (currentDoctorId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var request = await BuildReadableRequestsQuery(currentDoctorId.Value)
            .FirstOrDefaultAsync(item => item.LabTestRequestId == labTestRequestId, cancellationToken);

        return request is null
            ? new NotFoundObjectResult("Lab test request not found.")
            : new OkObjectResult(MapToResponse(request));
    }

    public async Task<ActionResult<LabTestRequestResponse>> CreateMyRequestAsync(
        CreateLabTestRequestRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var currentDoctorId = GetCurrentUserId(user);
        if (currentDoctorId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var doctorExists = await _dbContext.Doctors
            .AsNoTracking()
            .AnyAsync(doctor => doctor.DoctorId == currentDoctorId.Value, cancellationToken);
        if (!doctorExists)
        {
            return new NotFoundObjectResult("Doctor profile not found.");
        }

        var patient = await FindPatientAsync(request.PatientId, cancellationToken);
        if (patient is null)
        {
            return new BadRequestObjectResult("Patient not found.");
        }

        if (request.TestingClinicId == Guid.Empty)
        {
            return new BadRequestObjectResult("Testing clinic is required.");
        }

        var testingClinicExists = await _dbContext.Clinics
            .AsNoTracking()
            .AnyAsync(clinic => clinic.ClinicId == request.TestingClinicId && clinic.IsActive, cancellationToken);
        if (!testingClinicExists)
        {
            return new BadRequestObjectResult("Testing clinic not found or inactive.");
        }

        var loinc = await _standardsService.GetLoincByCodeAsync(request.LoincCode, cancellationToken);
        if (loinc is null)
        {
            return new BadRequestObjectResult("LOINC code not found.");
        }

        var priority = NormalizePriority(request.Priority);
        if (priority is null)
        {
            return new BadRequestObjectResult("Invalid priority. Use Routine or Urgent.");
        }

        var laboratoryTechnologistId = await _dbContext.LaboratoryTechnologists
            .AsNoTracking()
            .Where(technologist => technologist.User.ClinicId == request.TestingClinicId && technologist.User.IsActive)
            .Select(technologist => (Guid?)technologist.LaboratoryTechnologistId)
            .FirstOrDefaultAsync(cancellationToken);

        var labTestRequest = new LabTestRequest
        {
            LabTestRequestId = Guid.NewGuid(),
            TestName = loinc.Display,
            PatientId = patient.PatientId,
            RequestedByDoctorId = currentDoctorId.Value,
            TestingClinicId = request.TestingClinicId,
            LoincCode = loinc.Code,
            Priority = priority,
            ClinicalNotes = Clean(request.ClinicalNotes),
            RequestedAt = DateTime.UtcNow,
            LaboratoryTechnologistId = laboratoryTechnologistId,
            ResultMedicalFileId = null
        };

        _dbContext.LabTestRequests.Add(labTestRequest);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var created = await BuildReadableRequestsQuery(currentDoctorId.Value)
            .FirstAsync(item => item.LabTestRequestId == labTestRequest.LabTestRequestId, cancellationToken);

        return new OkObjectResult(MapToResponse(created));
    }

    public async Task<IActionResult> DownloadMyResultFileAsync(
        Guid labTestRequestId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var currentDoctorId = GetCurrentUserId(user);
        if (currentDoctorId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var request = await BuildReadableRequestsQuery(currentDoctorId.Value)
            .FirstOrDefaultAsync(item => item.LabTestRequestId == labTestRequestId, cancellationToken);
        if (request is null)
        {
            return new NotFoundObjectResult("Lab test request not found.");
        }

        if (request.ResultMedicalFile is null)
        {
            if (request.StructuredResult is null)
            {
                return new BadRequestObjectResult("This lab test request has no result file yet.");
            }

            if (string.IsNullOrWhiteSpace(request.StructuredResult.PdfFilePath))
            {
                return new BadRequestObjectResult("The structured lab result PDF has not been generated yet.");
            }

            var generatedPdfPath = ResolvePhysicalPath(request.StructuredResult.PdfFilePath);
            if (!System.IO.File.Exists(generatedPdfPath))
            {
                return new NotFoundObjectResult("Result file was not found on disk.");
            }

            return new PhysicalFileResult(generatedPdfPath, "application/pdf")
            {
                FileDownloadName = Path.GetFileName(generatedPdfPath),
                EnableRangeProcessing = true
            };
        }

        var filePath = ResolvePhysicalPath(request.ResultMedicalFile);
        if (!System.IO.File.Exists(filePath))
        {
            return new NotFoundObjectResult("Result file was not found on disk.");
        }

        if (!_contentTypeProvider.TryGetContentType(filePath, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        return new PhysicalFileResult(filePath, contentType)
        {
            FileDownloadName = Path.GetFileName(filePath),
            EnableRangeProcessing = true
        };
    }

    private IQueryable<LabTestRequest> BuildReadableRequestsQuery(Guid doctorId)
    {
        return _dbContext.LabTestRequests
            .AsNoTracking()
            .Include(request => request.Patient)
                .ThenInclude(patient => patient!.User)
            .Include(request => request.RequestedByDoctor)
                .ThenInclude(doctor => doctor!.User)
            .Include(request => request.TestingClinic)
            .Include(request => request.LaboratoryTechnologist)
                .ThenInclude(technologist => technologist!.User)
            .Include(request => request.ResultMedicalFile)
                .ThenInclude(file => file!.Appointment)
                    .ThenInclude(appointment => appointment.Patient)
                        .ThenInclude(patient => patient.User)
            .Include(request => request.ResultMedicalFile)
                .ThenInclude(file => file!.Appointment)
                    .ThenInclude(appointment => appointment.Doctor)
                        .ThenInclude(doctor => doctor.User)
            .Include(request => request.StructuredResult)
            .Where(request =>
                request.RequestedByDoctorId == doctorId
                || (request.ResultMedicalFile != null
                    && request.ResultMedicalFile.Appointment.DoctorId == doctorId));
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
            .Where(patient => patient.User.IsActive)
            .AsQueryable();

        return Guid.TryParse(trimmedPatientId, out var patientGuid)
            ? await query.FirstOrDefaultAsync(patient => patient.PatientId == patientGuid, cancellationToken)
            : await query.FirstOrDefaultAsync(patient => patient.UserID == trimmedPatientId, cancellationToken);
    }

    private static bool ApplyStatusFilter(
        string? status,
        ref IQueryable<LabTestRequest> query,
        out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(status) || status.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        switch (status.Trim().ToLowerInvariant())
        {
            case "pending":
                query = query.Where(request => request.ResultMedicalFileId == null && request.StructuredResult == null);
                return true;
            case "result":
            case "results":
            case "completed":
            case "results-available":
            case "result-available":
                query = query.Where(request => request.ResultMedicalFileId != null || request.StructuredResult != null);
                return true;
            default:
                error = "Invalid status. Use all, pending, or results-available.";
                return false;
        }
    }

    private LabTestRequestResponse MapToResponse(LabTestRequest request)
    {
        var patient = request.Patient ?? request.ResultMedicalFile?.Appointment.Patient;
        var doctor = request.RequestedByDoctor ?? request.ResultMedicalFile?.Appointment.Doctor;
        var resultFile = request.ResultMedicalFile;
        var structuredResult = request.StructuredResult;

        return new LabTestRequestResponse
        {
            LabTestRequestId = request.LabTestRequestId,
            TestName = request.TestName,
            LoincCode = request.LoincCode,
            Priority = request.Priority,
            Status = resultFile is null && structuredResult is null ? "Pending" : "ResultsAvailable",
            ClinicalNotes = request.ClinicalNotes,
            RequestedAt = request.RequestedAt,
            PatientId = patient?.PatientId,
            PatientUserId = patient?.UserID,
            PatientName = patient?.User.Name,
            RequestedByDoctorId = doctor?.DoctorId,
            RequestedByDoctorName = doctor?.User.Name,
            TestingClinicId = request.TestingClinicId,
            TestingClinicName = request.TestingClinic?.Name,
            LaboratoryTechnologistId = request.LaboratoryTechnologistId,
            LaboratoryTechnologistName = request.LaboratoryTechnologist?.User.Name,
            ResultMedicalFileId = resultFile?.MedicalFileId,
            StructuredResultId = structuredResult?.LabTestResultId,
            ResultFileName = resultFile is not null
                ? Path.GetFileName(resultFile.FilePath)
                : Path.GetFileName(structuredResult?.PdfFilePath),
            ResultFileSizeInBytes = resultFile?.FileSizeInBytes ?? structuredResult?.PdfFileSizeInBytes,
            ResultUploadedAt = resultFile?.UploadedAt ?? structuredResult?.PdfGeneratedAt,
            ResultSummary = structuredResult?.Comments ?? request.ClinicalNotes ?? resultFile?.Appointment.Notes,
            ResultFileUrl = resultFile is null && structuredResult?.PdfFilePath is null
                ? null
                : $"/api/Doctors/me/lab-requests/{request.LabTestRequestId}/result-file"
        };
    }

    private string ResolvePhysicalPath(MedicalFile file)
    {
        if (Path.IsPathRooted(file.FilePath))
        {
            return file.FilePath;
        }

        var relativePath = file.FilePath.TrimStart(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar,
            '/',
            '\\');

        return Path.Combine(_environment.ContentRootPath, relativePath);
    }

    private string ResolvePhysicalPath(string filePath)
    {
        var contentRoot = Path.GetFullPath(_environment.ContentRootPath);
        var resolvedPath = Path.IsPathRooted(filePath)
            ? Path.GetFullPath(filePath)
            : Path.GetFullPath(Path.Combine(
                contentRoot,
                filePath.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '/', '\\')));
        if (!resolvedPath.StartsWith(contentRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Structured lab result path is outside the application content root.");
        }

        return resolvedPath;
    }

    private static Guid? GetCurrentUserId(ClaimsPrincipal user)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userId, out var parsedUserId) ? parsedUserId : null;
    }

    private static string? NormalizePriority(string? priority)
    {
        if (string.IsNullOrWhiteSpace(priority))
        {
            return "Routine";
        }

        return priority.Trim().ToLowerInvariant() switch
        {
            "routine" => "Routine",
            "urgent" => "Urgent",
            _ => null
        };
    }

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static (int Page, int PageSize) NormalizePaging(int page, int pageSize)
    {
        var normalizedPage = page <= 0 ? DefaultPage : page;
        var normalizedPageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
        return (normalizedPage, normalizedPageSize);
    }

    private static int CalculateTotalPages(int totalCount, int pageSize)
    {
        return totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
    }
}
