namespace HSCSAPI.DTOs.Laboratory;

public class CreateLabTestRequestRequest
{
    public string PatientId { get; set; } = string.Empty;
    public Guid TestingClinicId { get; set; }
    public string LoincCode { get; set; } = string.Empty;
    public string Priority { get; set; } = "Routine";
    public string? ClinicalNotes { get; set; }
}

public class LabTestRequestsResponse
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public List<LabTestRequestResponse> Items { get; set; } = [];
}

public class LabTestRequestResponse
{
    public Guid LabTestRequestId { get; set; }
    public string TestName { get; set; } = string.Empty;
    public string? LoincCode { get; set; }
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ClinicalNotes { get; set; }
    public DateTime RequestedAt { get; set; }
    public Guid? PatientId { get; set; }
    public string? PatientUserId { get; set; }
    public string? PatientName { get; set; }
    public Guid? RequestedByDoctorId { get; set; }
    public string? RequestedByDoctorName { get; set; }
    public Guid? TestingClinicId { get; set; }
    public string? TestingClinicName { get; set; }
    public Guid? LaboratoryTechnologistId { get; set; }
    public string? LaboratoryTechnologistName { get; set; }
    public Guid? ResultMedicalFileId { get; set; }
    public string? ResultFileName { get; set; }
    public long? ResultFileSizeInBytes { get; set; }
    public DateTime? ResultUploadedAt { get; set; }
    public string? ResultSummary { get; set; }
    public string? ResultFileUrl { get; set; }
}
