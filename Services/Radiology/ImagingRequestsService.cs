using System.Security.Claims;
using HSCSAPI.Data;
using HSCSAPI.DTOs.Radiology;
using HSCSAPI.DTOs.Standards;
using HSCSAPI.Models.MedicalFiles;
using HSCSAPI.Models.Profiles;
using HSCSAPI.Models.Radiology;
using HSCSAPI.Services.Standards;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Services.Radiology;

public class ImagingRequestsService : IImagingRequestsService
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly AppDbContext _dbContext;
    private readonly IStandardsService _standardsService;
    private readonly IWebHostEnvironment _environment;
    private readonly FileExtensionContentTypeProvider _contentTypeProvider = new();

    public ImagingRequestsService(
        AppDbContext dbContext,
        IStandardsService standardsService,
        IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _standardsService = standardsService;
        _environment = environment;
    }

    public async Task<ActionResult<ImagingRequestsResponse>> GetMyRequestsAsync(
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
            .ThenByDescending(request => request.ImagingTestRequestId)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        return new OkObjectResult(new ImagingRequestsResponse
        {
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount,
            TotalPages = CalculateTotalPages(totalCount, normalizedPageSize),
            Items = requests.Select(MapToResponse).ToList()
        });
    }

    public async Task<ActionResult<ImagingRequestResponse>> GetMyRequestAsync(
        Guid imagingTestRequestId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var currentDoctorId = GetCurrentUserId(user);
        if (currentDoctorId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var request = await BuildReadableRequestsQuery(currentDoctorId.Value)
            .FirstOrDefaultAsync(item => item.ImagingTestRequestId == imagingTestRequestId, cancellationToken);

        return request is null
            ? new NotFoundObjectResult("Imaging request not found.")
            : new OkObjectResult(MapToResponse(request));
    }

    public async Task<ActionResult<ImagingRequestResponse>> CreateMyRequestAsync(
        CreateImagingRequestRequest request,
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

        if (request.RadiologyClinicId == Guid.Empty)
        {
            return new BadRequestObjectResult("Radiology clinic is required.");
        }

        var radiologyClinicExists = await _dbContext.Clinics
            .AsNoTracking()
            .AnyAsync(clinic => clinic.ClinicId == request.RadiologyClinicId, cancellationToken);
        if (!radiologyClinicExists)
        {
            return new BadRequestObjectResult("Radiology clinic not found.");
        }

        var imagingType = await ResolveImagingTypeAsync(request.ImagingCode, cancellationToken);
        if (imagingType is null)
        {
            return new BadRequestObjectResult("Imaging type not found.");
        }

        var priority = NormalizePriority(request.Priority);
        if (priority is null)
        {
            return new BadRequestObjectResult("Invalid priority. Use Routine or Urgent.");
        }

        var radiologyTechnologistId = await _dbContext.RadiologyTechnologists
            .AsNoTracking()
            .Where(technologist => technologist.User.ClinicId == request.RadiologyClinicId)
            .Select(technologist => (Guid?)technologist.RadiologyTechnologistId)
            .FirstOrDefaultAsync(cancellationToken);

        var imagingRequest = new ImagingTestRequest
        {
            ImagingTestRequestId = Guid.NewGuid(),
            TestName = imagingType.Display,
            PatientId = patient.PatientId,
            RequestedByDoctorId = currentDoctorId.Value,
            RadiologyClinicId = request.RadiologyClinicId,
            ImagingCode = imagingType.Code,
            BodyRegion = Clean(request.BodyRegion) ?? imagingType.BodyRegion,
            Priority = priority,
            ClinicalNotes = Clean(request.ClinicalNotes),
            RequestedAt = DateTime.UtcNow,
            RadiologyTechnologistId = radiologyTechnologistId,
            ResultMedicalFileId = null
        };

        _dbContext.ImagingTestRequests.Add(imagingRequest);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var created = await BuildReadableRequestsQuery(currentDoctorId.Value)
            .FirstAsync(item => item.ImagingTestRequestId == imagingRequest.ImagingTestRequestId, cancellationToken);

        return new OkObjectResult(MapToResponse(created));
    }

    public async Task<IActionResult> DownloadMyResultFileAsync(
        Guid imagingTestRequestId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var currentDoctorId = GetCurrentUserId(user);
        if (currentDoctorId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var request = await BuildReadableRequestsQuery(currentDoctorId.Value)
            .FirstOrDefaultAsync(item => item.ImagingTestRequestId == imagingTestRequestId, cancellationToken);
        if (request is null)
        {
            return new NotFoundObjectResult("Imaging request not found.");
        }

        if (request.ResultMedicalFile is null)
        {
            return new BadRequestObjectResult("This imaging request has no result file yet.");
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

    private IQueryable<ImagingTestRequest> BuildReadableRequestsQuery(Guid doctorId)
    {
        return _dbContext.ImagingTestRequests
            .AsNoTracking()
            .Include(request => request.Patient)
                .ThenInclude(patient => patient!.User)
            .Include(request => request.RequestedByDoctor)
                .ThenInclude(doctor => doctor!.User)
            .Include(request => request.RadiologyClinic)
            .Include(request => request.RadiologyTechnologist)
                .ThenInclude(technologist => technologist!.User)
            .Include(request => request.ResultMedicalFile)
                .ThenInclude(file => file!.Appointment)
                    .ThenInclude(appointment => appointment.Patient)
                        .ThenInclude(patient => patient.User)
            .Include(request => request.ResultMedicalFile)
                .ThenInclude(file => file!.Appointment)
                    .ThenInclude(appointment => appointment.Doctor)
                        .ThenInclude(doctor => doctor.User)
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
            .AsQueryable();

        return Guid.TryParse(trimmedPatientId, out var patientGuid)
            ? await query.FirstOrDefaultAsync(patient => patient.PatientId == patientGuid, cancellationToken)
            : await query.FirstOrDefaultAsync(patient => patient.UserID == trimmedPatientId, cancellationToken);
    }

    private async Task<ResolvedImagingType?> ResolveImagingTypeAsync(
        string imagingCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(imagingCode))
        {
            return null;
        }

        var trimmedCode = imagingCode.Trim();
        var playbook = await _standardsService.GetRadiologyPlaybookByRpidAsync(trimmedCode, cancellationToken);
        if (playbook is not null)
        {
            return new ResolvedImagingType(
                playbook.Rpid,
                playbook.Display,
                playbook.BodyRegion);
        }

        var imagingTypes = await _standardsService.GetImagingTypesAsync(trimmedCode, cancellationToken);
        var imagingType = imagingTypes.FirstOrDefault(item =>
            item.Code.Equals(trimmedCode, StringComparison.OrdinalIgnoreCase)
            || item.Modality.Equals(trimmedCode, StringComparison.OrdinalIgnoreCase)
            || item.Display.Equals(trimmedCode, StringComparison.OrdinalIgnoreCase));

        return imagingType is null
            ? null
            : new ResolvedImagingType(imagingType.Code, imagingType.Display, null);
    }

    private static bool ApplyStatusFilter(
        string? status,
        ref IQueryable<ImagingTestRequest> query,
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
                query = query.Where(request => request.ResultMedicalFileId == null);
                return true;
            case "result":
            case "results":
            case "completed":
            case "results-available":
            case "result-available":
                query = query.Where(request => request.ResultMedicalFileId != null);
                return true;
            default:
                error = "Invalid status. Use all, pending, or results-available.";
                return false;
        }
    }

    private ImagingRequestResponse MapToResponse(ImagingTestRequest request)
    {
        var patient = request.Patient ?? request.ResultMedicalFile?.Appointment.Patient;
        var doctor = request.RequestedByDoctor ?? request.ResultMedicalFile?.Appointment.Doctor;
        var resultFile = request.ResultMedicalFile;

        return new ImagingRequestResponse
        {
            ImagingTestRequestId = request.ImagingTestRequestId,
            TestName = request.TestName,
            ImagingCode = request.ImagingCode,
            BodyRegion = request.BodyRegion,
            Priority = request.Priority,
            Status = resultFile is null ? "Pending" : "ResultsAvailable",
            ClinicalNotes = request.ClinicalNotes,
            RequestedAt = request.RequestedAt,
            PatientId = patient?.PatientId,
            PatientUserId = patient?.UserID,
            PatientName = patient?.User.Name,
            RequestedByDoctorId = doctor?.DoctorId,
            RequestedByDoctorName = doctor?.User.Name,
            RadiologyClinicId = request.RadiologyClinicId,
            RadiologyClinicName = request.RadiologyClinic?.Name,
            RadiologyTechnologistId = request.RadiologyTechnologistId,
            RadiologyTechnologistName = request.RadiologyTechnologist?.User.Name,
            ResultMedicalFileId = resultFile?.MedicalFileId,
            ResultFileName = resultFile is null ? null : Path.GetFileName(resultFile.FilePath),
            ResultFileSizeInBytes = resultFile?.FileSizeInBytes,
            ResultUploadedAt = resultFile?.UploadedAt,
            ResultSummary = resultFile?.Appointment.Notes,
            ResultFileUrl = resultFile is null
                ? null
                : $"/api/Doctors/me/imaging-requests/{request.ImagingTestRequestId}/result-file"
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

    private sealed record ResolvedImagingType(
        string Code,
        string Display,
        string? BodyRegion);
}
