using Microsoft.AspNetCore.Http;

namespace HSCSAPI.DTOs.MedicalFiles;

public class CreateMedicalFileUploadRequest
{
    public string PatientId { get; set; } = string.Empty;
    public Guid? AppointmentId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? StandardCode { get; set; }
    public string? DisplayName { get; set; }
    public string? DiagnosisCode { get; set; }
    public string? DiagnosisName { get; set; }
    public string? ActivityCode { get; set; }
    public string? ActivityName { get; set; }
    public string? Notes { get; set; }
    public IFormFile? File { get; set; }
}

public class MedicalFileUploadCategoryResponse
{
    public string Category { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool RequiresStandardCode { get; set; }
    public string? StandardSource { get; set; }
    public string? StandardsEndpoint { get; set; }
}

public class MedicalFileUploadResponse
{
    public Guid MedicalFileId { get; set; }
    public Guid AppointmentId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? StandardCode { get; set; }
    public string? StandardDisplay { get; set; }
    public string? DiagnosisCode { get; set; }
    public string? DiagnosisName { get; set; }
    public string? ActivityCode { get; set; }
    public string? ActivityName { get; set; }
    public string? Notes { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long FileSizeInBytes { get; set; }
    public DateTime UploadedAt { get; set; }
    public Guid PatientId { get; set; }
    public string PatientUserId { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public Guid UploadedByDoctorId { get; set; }
    public string UploadedByDoctorName { get; set; } = string.Empty;
    public Guid? LabTestRequestId { get; set; }
    public Guid? ImagingTestRequestId { get; set; }
    public string FileUrl { get; set; } = string.Empty;
}
