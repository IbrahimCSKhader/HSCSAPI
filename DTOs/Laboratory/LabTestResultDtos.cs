namespace HSCSAPI.DTOs.Laboratory;

public class LabTestTemplateResponse
{
    public Guid LabTestTemplateId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ShortLabel { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LoincCode { get; set; }
    public string SpecimenType { get; set; } = string.Empty;
    public string? PreparationInstructions { get; set; }
    public string SourceUrl { get; set; } = string.Empty;
    public int Version { get; set; }
    public bool IsActive { get; set; }
    public List<LabTestFieldResponse> Fields { get; set; } = [];
}

public class LabTestFieldResponse
{
    public Guid LabTestFieldDefinitionId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? LoincCode { get; set; }
    public string ValueType { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public bool IsRequired { get; set; }
    public int? DecimalPlaces { get; set; }
    public string? ReferenceRange { get; set; }
    public List<string> AllowedValues { get; set; } = [];
    public int DisplayOrder { get; set; }
}

public class LabWorkItemsResponse
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public List<LabWorkItemResponse> Items { get; set; } = [];
}

public class LabWorkItemResponse
{
    public Guid LabTestRequestId { get; set; }
    public string TestName { get; set; } = string.Empty;
    public string? LoincCode { get; set; }
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public Guid? PatientId { get; set; }
    public string? PatientUserId { get; set; }
    public string? PatientName { get; set; }
    public string? DoctorName { get; set; }
    public string? TestingClinicName { get; set; }
    public string? SuggestedTemplateCode { get; set; }
    public Guid? LabTestResultId { get; set; }
    public string? ClinicalNotes { get; set; }
    public string? RequestingDoctorId { get; set; }
    public DateTime? CompletedAtIso { get; set; }
    public string? ResultSummary { get; set; }
    public bool PdfAvailable { get; set; }
    public string? PdfFileName { get; set; }
    public DateOnly? PatientDateOfBirth { get; set; }
    public string? PatientGender { get; set; }
}

public class CreateLabTestResultRequest
{
    public string TemplateCode { get; set; } = string.Empty;
    public string AccessionNumber { get; set; } = string.Empty;
    public DateTime CollectedAt { get; set; }
    public DateTime ReceivedAt { get; set; }
    public string SpecimenCondition { get; set; } = "Accepted";
    public string? SpecimenNotes { get; set; }
    public string? Comments { get; set; }
    public List<CreateLabTestResultValueRequest> Values { get; set; } = [];
}

public class CreateLabTestResultValueRequest
{
    public string FieldCode { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? ReferenceRange { get; set; }
    public string? Flag { get; set; }
}

public class LabTestResultResponse
{
    public Guid LabTestResultId { get; set; }
    public Guid LabTestRequestId { get; set; }
    public string TemplateCode { get; set; } = string.Empty;
    public string TestName { get; set; } = string.Empty;
    public string? LoincCode { get; set; }
    public int TemplateVersion { get; set; }
    public string AccessionNumber { get; set; } = string.Empty;
    public DateTime CollectedAt { get; set; }
    public DateTime ReceivedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public string SpecimenCondition { get; set; } = string.Empty;
    public string? SpecimenNotes { get; set; }
    public string? Comments { get; set; }
    public Guid? PatientId { get; set; }
    public string? PatientUserId { get; set; }
    public string? PatientName { get; set; }
    public string? DoctorName { get; set; }
    public string? LaboratoryTechnologistName { get; set; }
    public string? TestingClinicName { get; set; }
    public List<LabTestResultValueResponse> Values { get; set; } = [];
    public LabResultPdfResponse? Pdf { get; set; }
}

public class LabTestResultValueResponse
{
    public string FieldCode { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string ValueType { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public string? ReferenceRange { get; set; }
    public string? Flag { get; set; }
    public int DisplayOrder { get; set; }
}

public class LabResultPdfResponse
{
    public string FileName { get; set; } = string.Empty;
    public long FileSizeInBytes { get; set; }
    public string Sha256Checksum { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
}
