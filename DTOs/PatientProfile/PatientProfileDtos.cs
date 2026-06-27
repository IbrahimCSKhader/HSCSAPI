namespace HSCSAPI.DTOs.PatientProfile;

public class PagedResponse<T>
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public List<T> Items { get; set; } = [];
}

public class PatientProfileDashboardResponse
{
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public int UpcomingAppointmentsCount { get; set; }
    public int UnreadMessagesCount { get; set; }
    public int UnreadNotificationsCount { get; set; }
    public PatientHealthOverviewResponse HealthOverview { get; set; } = new();
    public PatientVisitsByClinicSummaryResponse VisitsByClinic { get; set; } = new();
    public List<PatientProfileAppointmentSummaryResponse> UpcomingAppointments { get; set; } = [];
}

public class PatientHealthOverviewResponse
{
    public int TotalRecordsCount { get; set; }
    public int LabResultsCount { get; set; }
    public int PrescriptionsCount { get; set; }
    public int ImagingResultsCount { get; set; }
}

public class PatientVisitsByClinicSummaryResponse
{
    public int TotalVisits { get; set; }
    public int ClinicsCount { get; set; }
    public List<PatientVisitByClinicResponse> Clinics { get; set; } = [];
}

public class PatientVisitByClinicResponse
{
    public Guid? ClinicId { get; set; }
    public string ClinicName { get; set; } = string.Empty;
    public int VisitsCount { get; set; }
}

public class PatientProfileAppointmentSummaryResponse
{
    public Guid AppointmentId { get; set; }
    public Guid DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public Guid? ClinicId { get; set; }
    public string? ClinicName { get; set; }
    public DateOnly AppointmentDate { get; set; }
    public TimeOnly AppointmentTime { get; set; }
    public string? Notes { get; set; }
}

public class PatientMedicalRecordResponse
{
    public Guid MedicalFileId { get; set; }
    public string RecordCode { get; set; } = string.Empty;
    public string RecordType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string SeverityLevel { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long FileSizeInBytes { get; set; }
    public DateTime UploadedAt { get; set; }
    public Guid DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public Guid? ClinicId { get; set; }
    public string? ClinicName { get; set; }
    public DateOnly AppointmentDate { get; set; }
    public bool CanDownloadDirectly { get; set; }
    public bool HasPendingDownloadRequest { get; set; }
}

public class PatientMedicalRecordDetailResponse : PatientMedicalRecordResponse
{
    public Guid AppointmentId { get; set; }
    public TimeOnly AppointmentTime { get; set; }
    public string? AppointmentNotes { get; set; }
    public string? Summary { get; set; }
    public string? ClinicalDetails { get; set; }
}

public class CreateFileDownloadRequest
{
    public string Reason { get; set; } = string.Empty;
    public string PurposeDescription { get; set; } = string.Empty;
}

public class PatientFileDownloadRequestResponse
{
    public Guid FileDownloadRequestId { get; set; }
    public Guid MedicalFileId { get; set; }
    public string RecordTitle { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string PurposeDescription { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }
    public Guid? ReviewedBySecretaryId { get; set; }
    public string? ReviewedBySecretaryName { get; set; }
}

public class PatientAuthorizedMemberResponse
{
    public Guid AuthorizedMemberId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string RelationshipType { get; set; } = string.Empty;
    public DateTime AuthorizedAt { get; set; }
    public bool IsActive { get; set; }
}

public class CreateAuthorizedMemberInviteRequest
{
    public string? FullName { get; set; }
    public string Email { get; set; } = string.Empty;
    public string RelationshipType { get; set; } = string.Empty;
}

public class PatientAuthorizedMemberInviteResponse
{
    public Guid InviteId { get; set; }
    public Guid AuthorizedMemberId { get; set; }
    public string AuthorizedMemberName { get; set; } = string.Empty;
    public string AuthorizedMemberEmail { get; set; } = string.Empty;
    public string RelationshipType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public bool IsActive { get; set; }
}
