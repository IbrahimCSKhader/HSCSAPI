namespace HSCSAPI.DTOs.PatientProfile;

public class CreateExternalProfileShareRequest
{
    public string DoctorEmail { get; set; } = string.Empty;
    public string? DoctorName { get; set; }
}

public class ExternalProfileShareResponse
{
    public Guid ShareId { get; set; }
    public string DoctorEmail { get; set; } = string.Empty;
    public string? DoctorName { get; set; }
    public string ShareUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsActive { get; set; }
}

public class ExternalShareEmailRequest
{
    public string Email { get; set; } = string.Empty;
}

public class ExternalShareVerifyRequest
{
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class ExternalShareStatusResponse
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
    public string? DoctorEmailHint { get; set; }
}

public class ExternalShareVerifyResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public class ExternalSharedPatientProfileResponse
{
    public ExternalSharedPatientSummary Patient { get; set; } = new();
    public DateTime ShareExpiresAt { get; set; }
    public List<ExternalSharedTimelineItem> Timeline { get; set; } = [];
    public List<ExternalSharedDiagnosisItem> Diagnoses { get; set; } = [];
    public List<ExternalSharedMedicationItem> Medications { get; set; } = [];
    public List<ExternalSharedTestItem> Tests { get; set; } = [];
    public List<ExternalSharedMedicalFileItem> Files { get; set; } = [];
}

public class ExternalSharedPatientSummary
{
    public string PatientUserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string? BloodType { get; set; }
    public string? ClinicName { get; set; }
}

public class ExternalSharedTimelineItem
{
    public DateTime OccurredAt { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DoctorName { get; set; }
}

public class ExternalSharedDiagnosisItem
{
    public string? DiagnosisCode { get; set; }
    public string DiagnosisName { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; }
    public string? DoctorName { get; set; }
    public string? SourceFileTitle { get; set; }
}

public class ExternalSharedMedicationItem
{
    public string? ActivityCode { get; set; }
    public string ActivityName { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; }
    public string? DoctorName { get; set; }
    public string? DiagnosisName { get; set; }
}

public class ExternalSharedTestItem
{
    public Guid RequestId { get; set; }
    public string TestType { get; set; } = string.Empty;
    public string TestName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime? ResultAt { get; set; }
    public string? ResultSummary { get; set; }
    public string? DoctorName { get; set; }
}

public class ExternalSharedMedicalFileItem
{
    public Guid MedicalFileId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string RecordType { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long FileSizeInBytes { get; set; }
    public DateTime UploadedAt { get; set; }
    public string? DoctorName { get; set; }
    public string ViewerUrl { get; set; } = string.Empty;
}
