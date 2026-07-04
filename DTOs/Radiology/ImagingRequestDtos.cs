namespace HSCSAPI.DTOs.Radiology;

public class UploadImagingResultRequest
{
    public string StudyCode { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public IFormFile File { get; set; } = null!;
}

public class CreateImagingRequestRequest
{
    public string PatientId { get; set; } = string.Empty;
    public Guid RadiologyClinicId { get; set; }
    public string ImagingCode { get; set; } = string.Empty;
    public string? BodyRegion { get; set; }
    public string Priority { get; set; } = "Routine";
    public string? ClinicalNotes { get; set; }
}

public class ImagingRequestsResponse
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public List<ImagingRequestResponse> Items { get; set; } = [];
}

public class ImagingRequestResponse
{
    public Guid ImagingTestRequestId { get; set; }
    public string TestName { get; set; } = string.Empty;
    public string? ImagingCode { get; set; }
    public string? BodyRegion { get; set; }
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ClinicalNotes { get; set; }
    public DateTime RequestedAt { get; set; }
    public Guid? PatientId { get; set; }
    public string? PatientUserId { get; set; }
    public string? PatientName { get; set; }
    public Guid? RequestedByDoctorId { get; set; }
    public string? RequestedByDoctorName { get; set; }
    public string? RequestingDoctorId { get; set; }
    public Guid? RadiologyClinicId { get; set; }
    public string? RadiologyClinicName { get; set; }
    public Guid? RadiologyTechnologistId { get; set; }
    public string? RadiologyTechnologistName { get; set; }
    public Guid? ResultMedicalFileId { get; set; }
    public string? ResultFileName { get; set; }
    public long? ResultFileSizeInBytes { get; set; }
    public DateTime? ResultUploadedAt { get; set; }
    public string? ResultSummary { get; set; }
    public string? ResultFileUrl { get; set; }
}
