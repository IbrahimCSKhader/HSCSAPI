using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using HSCSAPI.Data;
using HSCSAPI.DTOs.Laboratory;
using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Laboratory;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Services.Laboratory;

public partial class LaboratoryResultsService : ILaboratoryResultsService
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;
    private const int MaxValueLength = 500;

    private readonly AppDbContext _dbContext;
    private readonly ILabResultPdfGenerator _pdfGenerator;
    private readonly IWebHostEnvironment _environment;
    private readonly string _pdfRootPath;

    public LaboratoryResultsService(
        AppDbContext dbContext,
        ILabResultPdfGenerator pdfGenerator,
        IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _pdfGenerator = pdfGenerator;
        _environment = environment;
        _pdfRootPath = Path.Combine(environment.ContentRootPath, "wwwroot", "lab-results");
    }

    public async Task<ActionResult<List<LabTestTemplateResponse>>> GetTemplatesAsync(
        bool activeOnly,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.LabTestTemplates
            .AsNoTracking()
            .Include(x => x.Fields)
            .AsQueryable();

        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        var templates = await query.OrderBy(x => x.Name).ToListAsync(cancellationToken);
        return new OkObjectResult(templates.Select(MapTemplate).ToList());
    }

    public async Task<ActionResult<LabTestTemplateResponse>> GetTemplateAsync(
        string templateCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = Clean(templateCode);
        if (normalizedCode is null)
        {
            return new BadRequestObjectResult("Template code is required.");
        }

        var template = await _dbContext.LabTestTemplates
            .AsNoTracking()
            .Include(x => x.Fields)
            .FirstOrDefaultAsync(x => x.Code.ToUpper() == normalizedCode.ToUpper(), cancellationToken);

        return template is null
            ? new NotFoundObjectResult("Lab test template not found.")
            : new OkObjectResult(MapTemplate(template));
    }

    public async Task<ActionResult<LabWorkItemsResponse>> GetMyWorkItemsAsync(
        string? status,
        int page,
        int pageSize,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var access = await GetTechnologistAccessAsync(user, cancellationToken);
        if (access.Error is not null)
        {
            return access.Error;
        }

        var query = BuildAccessibleRequestsQuery(access.TechnologistId!.Value, access.ClinicId);
        if (!ApplyStatusFilter(status, ref query, out var statusError))
        {
            return new BadRequestObjectResult(statusError);
        }

        var (normalizedPage, normalizedPageSize) = NormalizePaging(page, pageSize);
        var totalCount = await query.CountAsync(cancellationToken);
        var requests = await query
            .OrderByDescending(x => x.Priority == "Urgent")
            .ThenBy(x => x.RequestedAt)
            .ThenBy(x => x.LabTestRequestId)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        var loincCodes = requests
            .Select(x => x.LoincCode)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var templateCodes = await _dbContext.LabTestTemplates
            .AsNoTracking()
            .Where(x => x.LoincCode != null && loincCodes.Contains(x.LoincCode))
            .ToDictionaryAsync(x => x.LoincCode!, x => x.Code, cancellationToken);

        return new OkObjectResult(new LabWorkItemsResponse
        {
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount,
            TotalPages = CalculateTotalPages(totalCount, normalizedPageSize),
            Items = requests.Select(x => MapWorkItem(
                x,
                ResolveSuggestedTemplateCode(x.LoincCode, templateCodes))).ToList()
        });
    }

    public async Task<ActionResult<LabWorkItemResponse>> GetMyWorkItemAsync(
        Guid labTestRequestId, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var access = await GetTechnologistAccessAsync(user, cancellationToken);
        if (access.Error is not null)
            return access.Error;

        var request = await BuildAccessibleRequestsQuery(access.TechnologistId!.Value, access.ClinicId)
            .FirstOrDefaultAsync(x => x.LabTestRequestId == labTestRequestId, cancellationToken);
        if (request is null)
            return new NotFoundObjectResult("Lab test request not found.");

        var templateCode = request.LoincCode is null ? null : await _dbContext.LabTestTemplates.AsNoTracking()
            .Where(x => x.LoincCode == request.LoincCode).Select(x => x.Code).FirstOrDefaultAsync(cancellationToken);
        templateCode ??= ResolveKnownPanelTemplateCode(request.LoincCode);
        return new OkObjectResult(MapWorkItem(request, templateCode));
    }

    public async Task<ActionResult<LabTestResultResponse>> CreateResultAsync(
        Guid labTestRequestId,
        CreateLabTestResultRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var access = await GetTechnologistAccessAsync(user, cancellationToken);
        if (access.Error is not null)
        {
            return access.Error;
        }

        var labRequest = await BuildRequestForResultQuery()
            .FirstOrDefaultAsync(x => x.LabTestRequestId == labTestRequestId, cancellationToken);
        if (labRequest is null || !CanAccess(labRequest, access.TechnologistId!.Value, access.ClinicId))
        {
            return new NotFoundObjectResult("Lab test request not found.");
        }

        if (labRequest.PatientId is null)
        {
            return new BadRequestObjectResult("The lab test request must be linked to a patient.");
        }

        if (labRequest.StructuredResult is not null)
        {
            return new ConflictObjectResult("A structured result already exists for this lab test request.");
        }

        var templateCode = Clean(request.TemplateCode);
        var template = templateCode is null
            ? null
            : await _dbContext.LabTestTemplates
                .Include(x => x.Fields)
                .FirstOrDefaultAsync(
                    x => x.IsActive && x.Code.ToUpper() == templateCode.ToUpper(),
                    cancellationToken);
        if (template is null)
        {
            return new BadRequestObjectResult("An active lab test template is required.");
        }

        if (!string.IsNullOrWhiteSpace(labRequest.LoincCode)
            && !string.IsNullOrWhiteSpace(template.LoincCode)
            && !labRequest.LoincCode.Equals(template.LoincCode, StringComparison.OrdinalIgnoreCase)
            && !IsAllowedPanelTemplateMapping(labRequest.LoincCode, template.Code))
        {
            return new BadRequestObjectResult(
                $"Template LOINC code {template.LoincCode} does not match request LOINC code {labRequest.LoincCode}.");
        }

        var accessionNumber = Clean(request.AccessionNumber) ?? GenerateAccessionNumber();
        if (!AccessionNumberRegex().IsMatch(accessionNumber))
        {
            return new BadRequestObjectResult(
                "Accession number must be 3-80 characters and contain only letters, numbers, dot, underscore, slash, or hyphen.");
        }

        if (await _dbContext.LabTestResults.AnyAsync(
                x => x.AccessionNumber.ToUpper() == accessionNumber.ToUpper(),
                cancellationToken))
        {
            return new ConflictObjectResult("Accession number already exists.");
        }

        if (!Enum.TryParse<SpecimenCondition>(request.SpecimenCondition, true, out var specimenCondition))
        {
            return new BadRequestObjectResult(
                $"Invalid specimen condition. Use: {string.Join(", ", Enum.GetNames<SpecimenCondition>())}.");
        }

        var collectedAt = NormalizeUtc(request.CollectedAt);
        var receivedAt = NormalizeUtc(request.ReceivedAt);
        var now = DateTime.UtcNow;
        if (collectedAt < new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            || receivedAt < collectedAt
            || collectedAt > now.AddMinutes(5)
            || receivedAt > now.AddMinutes(5))
        {
            return new BadRequestObjectResult(
                "Collection and receipt times are invalid. Receipt must be on or after collection and neither may be in the future.");
        }

        var valueValidation = ValidateValues(template.Fields, request.Values);
        if (valueValidation.Errors.Count > 0)
        {
            return new BadRequestObjectResult(new
            {
                Message = "Lab result values are invalid.",
                Errors = valueValidation.Errors
            });
        }

        var result = new LabTestResult
        {
            LabTestResultId = Guid.NewGuid(),
            LabTestRequestId = labRequest.LabTestRequestId,
            LabTestTemplateId = template.LabTestTemplateId,
            LaboratoryTechnologistId = access.TechnologistId.Value,
            TemplateVersion = template.Version,
            AccessionNumber = accessionNumber,
            CollectedAt = collectedAt,
            ReceivedAt = receivedAt,
            CompletedAt = now,
            SpecimenCondition = specimenCondition,
            SpecimenNotes = Clean(request.SpecimenNotes, 1000),
            Comments = Clean(request.Comments, 2000),
            CreatedAt = now,
            Values = valueValidation.Values
        };

        if (labRequest.LaboratoryTechnologistId is null)
        {
            labRequest.LaboratoryTechnologistId = access.TechnologistId.Value;
        }

        _dbContext.LabTestResults.Add(result);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var created = await BuildResultQuery()
            .FirstAsync(x => x.LabTestResultId == result.LabTestResultId, cancellationToken);
        return new OkObjectResult(MapResult(created));
    }

    public async Task<ActionResult<LabTestResultResponse>> GetResultAsync(
        Guid labTestResultId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var access = await GetTechnologistAccessAsync(user, cancellationToken);
        if (access.Error is not null)
        {
            return access.Error;
        }

        var result = await BuildResultQuery()
            .FirstOrDefaultAsync(x => x.LabTestResultId == labTestResultId, cancellationToken);
        if (result is null || !CanAccess(result.LabTestRequest, access.TechnologistId!.Value, access.ClinicId))
        {
            return new NotFoundObjectResult("Lab test result not found.");
        }

        return new OkObjectResult(MapResult(result));
    }

    public async Task<ActionResult<LabResultPdfResponse>> GeneratePdfAsync(
        Guid labTestResultId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var access = await GetTechnologistAccessAsync(user, cancellationToken);
        if (access.Error is not null)
        {
            return access.Error;
        }

        var result = await BuildResultQuery(tracking: true)
            .FirstOrDefaultAsync(x => x.LabTestResultId == labTestResultId, cancellationToken);
        if (result is null || !CanAccess(result.LabTestRequest, access.TechnologistId!.Value, access.ClinicId))
        {
            return new NotFoundObjectResult("Lab test result not found.");
        }

        var bytes = _pdfGenerator.Generate(MapPdfDocument(result));
        var directory = Path.Combine(_pdfRootPath, result.LabTestResultId.ToString("N"));
        Directory.CreateDirectory(directory);
        var fileName = $"lab-result-{result.LabTestResultId:N}.pdf";
        var physicalPath = Path.Combine(directory, fileName);
        var temporaryPath = $"{physicalPath}.{Guid.NewGuid():N}.tmp";

        try
        {
            await File.WriteAllBytesAsync(temporaryPath, bytes, cancellationToken);
            File.Move(temporaryPath, physicalPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }

        result.PdfFilePath = Path.GetRelativePath(_environment.ContentRootPath, physicalPath).Replace('\\', '/');
        result.PdfChecksum = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        result.PdfFileSizeInBytes = bytes.LongLength;
        result.PdfGeneratedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new OkObjectResult(MapPdf(result));
    }

    public async Task<IActionResult> DownloadPdfAsync(
        Guid labTestResultId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var access = await GetTechnologistAccessAsync(user, cancellationToken);
        if (access.Error is not null)
        {
            return access.Error;
        }

        var result = await BuildResultQuery()
            .FirstOrDefaultAsync(x => x.LabTestResultId == labTestResultId, cancellationToken);
        if (result is null || !CanAccess(result.LabTestRequest, access.TechnologistId!.Value, access.ClinicId))
        {
            return new NotFoundObjectResult("Lab test result not found.");
        }

        if (string.IsNullOrWhiteSpace(result.PdfFilePath))
        {
            return new BadRequestObjectResult("Generate the PDF before downloading it.");
        }

        var physicalPath = ResolveGeneratedFilePath(result.PdfFilePath);
        if (!File.Exists(physicalPath))
        {
            return new NotFoundObjectResult("Generated PDF was not found on disk.");
        }

        return new PhysicalFileResult(physicalPath, "application/pdf")
        {
            FileDownloadName = Path.GetFileName(physicalPath),
            EnableRangeProcessing = true
        };
    }

    private async Task<TechnologistAccess> GetTechnologistAccessAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId(user);
        if (userId is null)
        {
            return new(null, null, new UnauthorizedObjectResult("Invalid token."));
        }

        var access = await _dbContext.LaboratoryTechnologists
            .AsNoTracking()
            .Where(x => x.LaboratoryTechnologistId == userId.Value)
            .Select(x => new { x.LaboratoryTechnologistId, x.User.ClinicId })
            .FirstOrDefaultAsync(cancellationToken);

        return access is null
            ? new(null, null, new NotFoundObjectResult("Laboratory technologist profile not found."))
            : new(access.LaboratoryTechnologistId, access.ClinicId, null);
    }

    private IQueryable<LabTestRequest> BuildAccessibleRequestsQuery(Guid technologistId, Guid? clinicId)
    {
        return BuildRequestForResultQuery()
            .AsNoTracking()
            .Where(x => x.LaboratoryTechnologistId == technologistId
                || (x.LaboratoryTechnologistId == null
                    && clinicId != null
                    && x.TestingClinicId == clinicId));
    }

    private static LabWorkItemResponse MapWorkItem(LabTestRequest request, string? suggestedTemplateCode)
    {
        var doctorCode = request.RequestedByDoctorId.HasValue
            ? $"DOC-{request.RequestedByDoctorId.Value.ToString("N")[..8].ToUpperInvariant()}"
            : null;
        return new LabWorkItemResponse
        {
            LabTestRequestId = request.LabTestRequestId,
            TestName = request.TestName,
            LoincCode = request.LoincCode,
            Priority = request.Priority,
            Status = request.StructuredResult is null ? "Pending" : "Completed",
            RequestedAt = request.RequestedAt,
            PatientId = request.PatientId,
            PatientUserId = request.Patient?.UserID,
            PatientName = request.Patient?.User.Name,
            PatientDateOfBirth = request.Patient?.User.DateOfBirth,
            PatientGender = request.Patient?.Gender.ToString(),
            DoctorName = request.RequestedByDoctor?.User.Name,
            RequestingDoctorId = doctorCode,
            TestingClinicName = request.TestingClinic?.Name,
            SuggestedTemplateCode = suggestedTemplateCode,
            LabTestResultId = request.StructuredResult?.LabTestResultId,
            ClinicalNotes = request.ClinicalNotes,
            CompletedAtIso = request.StructuredResult?.CompletedAt,
            ResultSummary = request.StructuredResult?.Comments,
            PdfAvailable = request.StructuredResult?.PdfFilePath is not null,
            PdfFileName = request.StructuredResult?.PdfFilePath is null ? null : Path.GetFileName(request.StructuredResult.PdfFilePath)
        };
    }

    private IQueryable<LabTestRequest> BuildRequestForResultQuery()
    {
        return _dbContext.LabTestRequests
            .Include(x => x.Patient)
                .ThenInclude(x => x!.User)
            .Include(x => x.RequestedByDoctor)
                .ThenInclude(x => x!.User)
            .Include(x => x.TestingClinic)
            .Include(x => x.StructuredResult);
    }

    private IQueryable<LabTestResult> BuildResultQuery(bool tracking = false)
    {
        var query = _dbContext.LabTestResults
            .Include(x => x.Template)
            .Include(x => x.Values)
            .Include(x => x.LaboratoryTechnologist)
                .ThenInclude(x => x.User)
            .Include(x => x.LabTestRequest)
                .ThenInclude(x => x.Patient)
                    .ThenInclude(x => x!.User)
            .Include(x => x.LabTestRequest)
                .ThenInclude(x => x.RequestedByDoctor)
                    .ThenInclude(x => x!.User)
            .Include(x => x.LabTestRequest)
                .ThenInclude(x => x.TestingClinic)
            .AsQueryable();

        return tracking ? query : query.AsNoTracking();
    }

    private static bool CanAccess(LabTestRequest request, Guid technologistId, Guid? clinicId) =>
        request.LaboratoryTechnologistId == technologistId
        || (request.LaboratoryTechnologistId is null
            && clinicId is not null
            && request.TestingClinicId == clinicId);

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
                query = query.Where(x => x.StructuredResult == null);
                return true;
            case "completed":
            case "results-available":
                query = query.Where(x => x.StructuredResult != null);
                return true;
            default:
                error = "Invalid status. Use all, pending, or completed.";
                return false;
        }
    }

    private static ValueValidationResult ValidateValues(
        IEnumerable<LabTestFieldDefinition> fields,
        IReadOnlyCollection<CreateLabTestResultValueRequest>? submittedValues)
    {
        var errors = new List<string>();
        var values = new List<LabTestResultValue>();
        var fieldList = fields.OrderBy(x => x.DisplayOrder).ToList();
        var submitted = submittedValues?.ToList() ?? [];

        var duplicates = submitted
            .Where(x => !string.IsNullOrWhiteSpace(x.FieldCode))
            .GroupBy(x => x.FieldCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToList();
        if (duplicates.Count > 0)
        {
            errors.Add($"Duplicate fields: {string.Join(", ", duplicates)}.");
        }

        var fieldByCode = fieldList.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
        var submittedByCode = submitted
            .Where(x => !string.IsNullOrWhiteSpace(x.FieldCode))
            .GroupBy(x => x.FieldCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        var unknown = submittedByCode.Keys.Where(x => !fieldByCode.ContainsKey(x)).ToList();
        if (unknown.Count > 0)
        {
            errors.Add($"Unknown fields: {string.Join(", ", unknown)}.");
        }

        var missing = fieldList
            .Where(x => x.IsRequired && !submittedByCode.ContainsKey(x.Code))
            .Select(x => x.Code)
            .ToList();
        if (missing.Count > 0)
        {
            errors.Add($"Missing required fields: {string.Join(", ", missing)}.");
        }

        foreach (var field in fieldList.Where(x => submittedByCode.ContainsKey(x.Code)))
        {
            var submittedValue = submittedByCode[field.Code];
            var rawValue = submittedValue.Value?.Trim();
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                errors.Add($"Field '{field.Code}' requires a value.");
                continue;
            }

            if (rawValue.Length > MaxValueLength)
            {
                errors.Add($"Field '{field.Code}' cannot exceed {MaxValueLength} characters.");
                continue;
            }

            decimal? numericValue = null;
            string? textValue = null;
            switch (field.ValueType)
            {
                case LabResultValueType.Numeric:
                    if (!decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
                    {
                        errors.Add($"Field '{field.Code}' must be a decimal number using '.' as the decimal separator.");
                        continue;
                    }

                    if (field.DecimalPlaces is int places && decimal.Round(parsed, places) != parsed)
                    {
                        errors.Add($"Field '{field.Code}' allows at most {places} decimal places.");
                        continue;
                    }

                    numericValue = parsed;
                    break;
                case LabResultValueType.Choice:
                    var choices = DeserializeChoices(field.AllowedValuesJson);
                    textValue = choices.FirstOrDefault(x => x.Equals(rawValue, StringComparison.OrdinalIgnoreCase));
                    if (textValue is null)
                    {
                        errors.Add($"Field '{field.Code}' must be one of: {string.Join(", ", choices)}.");
                        continue;
                    }
                    break;
                case LabResultValueType.Text:
                    textValue = rawValue;
                    break;
                default:
                    errors.Add($"Field '{field.Code}' has an unsupported value type.");
                    continue;
            }

            LabResultFlag? flag = null;
            if (!string.IsNullOrWhiteSpace(submittedValue.Flag))
            {
                if (!Enum.TryParse<LabResultFlag>(submittedValue.Flag, true, out var parsedFlag))
                {
                    errors.Add($"Field '{field.Code}' has an invalid flag.");
                    continue;
                }

                flag = parsedFlag;
            }

            values.Add(new LabTestResultValue
            {
                LabTestResultValueId = Guid.NewGuid(),
                LabTestFieldDefinitionId = field.LabTestFieldDefinitionId,
                FieldCode = field.Code,
                FieldLabel = field.Label,
                ValueType = field.ValueType,
                NumericValue = numericValue,
                TextValue = textValue,
                Unit = field.Unit,
                ReferenceRange = Clean(submittedValue.ReferenceRange, 200) ?? field.ReferenceRange,
                Flag = flag,
                DisplayOrder = field.DisplayOrder
            });
        }

        return new(errors, values);
    }

    private static LabTestTemplateResponse MapTemplate(LabTestTemplate template) => new()
    {
        LabTestTemplateId = template.LabTestTemplateId,
        Code = template.Code,
        Name = template.Name,
        ShortLabel = template.Code,
        DisplayName = template.Name,
        Description = template.PreparationInstructions,
        LoincCode = template.LoincCode,
        SpecimenType = template.SpecimenType,
        PreparationInstructions = template.PreparationInstructions,
        SourceUrl = template.SourceUrl,
        Version = template.Version,
        IsActive = template.IsActive,
        Fields = template.Fields.OrderBy(x => x.DisplayOrder).Select(x => new LabTestFieldResponse
        {
            LabTestFieldDefinitionId = x.LabTestFieldDefinitionId,
            Code = x.Code,
            Label = x.Label,
            LoincCode = x.LoincCode,
            ValueType = x.ValueType.ToString(),
            Unit = x.Unit,
            IsRequired = x.IsRequired,
            DecimalPlaces = x.DecimalPlaces,
            ReferenceRange = x.ReferenceRange,
            AllowedValues = DeserializeChoices(x.AllowedValuesJson),
            DisplayOrder = x.DisplayOrder
        }).ToList()
    };

    private static string? ResolveSuggestedTemplateCode(string? loincCode, IReadOnlyDictionary<string, string> templateCodes)
    {
        if (string.IsNullOrWhiteSpace(loincCode))
        {
            return null;
        }

        return templateCodes.TryGetValue(loincCode, out var code)
            ? code
            : ResolveKnownPanelTemplateCode(loincCode);
    }

    private static string? ResolveKnownPanelTemplateCode(string? loincCode)
    {
        return loincCode?.Equals("58410-2", StringComparison.OrdinalIgnoreCase) == true
            ? "CBC-DIFF"
            : null;
    }

    private static bool IsAllowedPanelTemplateMapping(string requestLoincCode, string templateCode)
    {
        return requestLoincCode.Equals("58410-2", StringComparison.OrdinalIgnoreCase)
            && templateCode.Equals("CBC-DIFF", StringComparison.OrdinalIgnoreCase);
    }

    private static string GenerateAccessionNumber()
    {
        return $"LAB-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
    }

    private static LabTestResultResponse MapResult(LabTestResult result) => new()
    {
        LabTestResultId = result.LabTestResultId,
        LabTestRequestId = result.LabTestRequestId,
        TemplateCode = result.Template.Code,
        TestName = result.Template.Name,
        LoincCode = result.Template.LoincCode,
        TemplateVersion = result.TemplateVersion,
        AccessionNumber = result.AccessionNumber,
        CollectedAt = result.CollectedAt,
        ReceivedAt = result.ReceivedAt,
        CompletedAt = result.CompletedAt,
        SpecimenCondition = result.SpecimenCondition.ToString(),
        SpecimenNotes = result.SpecimenNotes,
        Comments = result.Comments,
        PatientId = result.LabTestRequest.PatientId,
        PatientUserId = result.LabTestRequest.Patient?.UserID,
        PatientName = result.LabTestRequest.Patient?.User.Name,
        DoctorName = result.LabTestRequest.RequestedByDoctor?.User.Name,
        LaboratoryTechnologistName = result.LaboratoryTechnologist.User.Name,
        TestingClinicName = result.LabTestRequest.TestingClinic?.Name,
        Values = result.Values.OrderBy(x => x.DisplayOrder).Select(x => new LabTestResultValueResponse
        {
            FieldCode = x.FieldCode,
            Label = x.FieldLabel,
            ValueType = x.ValueType.ToString(),
            Value = FormatValue(x),
            Unit = x.Unit,
            ReferenceRange = x.ReferenceRange,
            Flag = x.Flag?.ToString(),
            DisplayOrder = x.DisplayOrder
        }).ToList(),
        Pdf = result.PdfGeneratedAt is null ? null : MapPdf(result)
    };

    private static LabResultPdfDocument MapPdfDocument(LabTestResult result)
    {
        var patient = result.LabTestRequest.Patient;
        return new LabResultPdfDocument
        {
            AccessionNumber = result.AccessionNumber,
            TestName = result.Template.Name,
            LoincCode = result.Template.LoincCode,
            PatientName = patient?.User.Name ?? "Not recorded",
            PatientUserId = patient?.UserID ?? "Not recorded",
            DateOfBirth = patient?.User.DateOfBirth?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Gender = patient?.Gender.ToString(),
            DoctorName = result.LabTestRequest.RequestedByDoctor?.User.Name ?? "Not recorded",
            LaboratoryTechnologistName = result.LaboratoryTechnologist.User.Name,
            ClinicName = result.LabTestRequest.TestingClinic?.Name ?? "Not recorded",
            SpecimenType = result.Template.SpecimenType,
            SpecimenCondition = result.SpecimenCondition.ToString(),
            SpecimenNotes = result.SpecimenNotes,
            CollectedAt = result.CollectedAt,
            ReceivedAt = result.ReceivedAt,
            CompletedAt = result.CompletedAt,
            Comments = result.Comments,
            Values = result.Values.OrderBy(x => x.DisplayOrder).Select(x => new LabResultPdfValue(
                x.FieldLabel,
                FormatValue(x),
                x.Flag?.ToString(),
                x.Unit,
                x.ReferenceRange)).ToList()
        };
    }

    private static LabResultPdfResponse MapPdf(LabTestResult result) => new()
    {
        FileName = Path.GetFileName(result.PdfFilePath) ?? string.Empty,
        FileSizeInBytes = result.PdfFileSizeInBytes ?? 0,
        Sha256Checksum = result.PdfChecksum ?? string.Empty,
        GeneratedAt = result.PdfGeneratedAt ?? default,
        DownloadUrl = $"/api/LaboratoryTests/results/{result.LabTestResultId}/pdf"
    };

    private string ResolveGeneratedFilePath(string relativePath)
    {
        var contentRoot = Path.GetFullPath(_environment.ContentRootPath);
        var path = Path.GetFullPath(Path.Combine(
            contentRoot,
            relativePath.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(contentRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Generated PDF path is outside the application content root.");
        }

        return path;
    }

    private static string FormatValue(LabTestResultValue value) =>
        value.NumericValue?.ToString(CultureInfo.InvariantCulture) ?? value.TextValue ?? string.Empty;

    private static List<string> DeserializeChoices(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static Guid? GetCurrentUserId(ClaimsPrincipal user)
    {
        var rawId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawId, out var id) ? id : null;
    }

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static string? Clean(string? value, int maxLength = 500)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = value.Trim();
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }

    private static (int Page, int PageSize) NormalizePaging(int page, int pageSize) =>
        (page <= 0 ? DefaultPage : page, pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize));

    private static int CalculateTotalPages(int totalCount, int pageSize) =>
        totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._/-]{2,79}$", RegexOptions.CultureInvariant)]
    private static partial Regex AccessionNumberRegex();

    private sealed record TechnologistAccess(Guid? TechnologistId, Guid? ClinicId, ActionResult? Error);
    private sealed record ValueValidationResult(List<string> Errors, List<LabTestResultValue> Values);
}
