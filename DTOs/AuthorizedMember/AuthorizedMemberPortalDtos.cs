namespace HSCSAPI.DTOs.AuthorizedMember;

public class AuthorizedMemberDashboardResponse
{
    public Guid AuthorizedMemberId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int LinkedPatientsCount { get; set; }
    public int MedicalRecordsCount { get; set; }
    public int UnreadNotificationsCount { get; set; }
    public int UpcomingAppointmentsCount { get; set; }
    public List<AuthorizedMemberLinkedPatientSummaryResponse> LinkedPatients { get; set; } = [];
    public List<AuthorizedMemberAppointmentResponse> UpcomingAppointments { get; set; } = [];
}

public class AuthorizedMemberLinkedPatientSummaryResponse
{
    public Guid PatientId { get; set; }
    public string PatientUserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RelationshipType { get; set; } = string.Empty;
    public int MedicalRecordsCount { get; set; }
    public int UpcomingAppointmentsCount { get; set; }
}

public class AuthorizedMemberAppointmentsResponse
{
    public int TotalCount { get; set; }
    public List<AuthorizedMemberAppointmentResponse> Items { get; set; } = [];
}

public class AuthorizedMemberAppointmentResponse
{
    public Guid AppointmentId { get; set; }
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientUserId { get; set; } = string.Empty;
    public Guid DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public Guid? ClinicId { get; set; }
    public string? ClinicName { get; set; }
    public DateOnly AppointmentDate { get; set; }
    public string DayOfWeek { get; set; } = string.Empty;
    public TimeOnly AppointmentTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class AuthorizedMemberMedicalRecordsResponse
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public AuthorizedMemberMedicalRecordTypeCountsResponse TypeCounts { get; set; } = new();
    public List<AuthorizedMemberMedicalRecordResponse> Items { get; set; } = [];
}

public class AuthorizedMemberMedicalRecordTypeCountsResponse
{
    public int All { get; set; }
    public int LabResults { get; set; }
    public int Prescriptions { get; set; }
    public int Imaging { get; set; }
}

public class AuthorizedMemberMedicalRecordResponse
{
    public Guid MedicalFileId { get; set; }
    public Guid AppointmentId { get; set; }
    public string RecordCode { get; set; } = string.Empty;
    public string RecordType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long FileSizeInBytes { get; set; }
    public string SeverityLevel { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public Guid PatientId { get; set; }
    public string PatientUserId { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public Guid RecordedByDoctorId { get; set; }
    public string RecordedByDoctorName { get; set; } = string.Empty;
    public Guid? ClinicId { get; set; }
    public string? ClinicName { get; set; }
    public DateOnly AppointmentDate { get; set; }
    public TimeOnly AppointmentTime { get; set; }
    public string? LabTestName { get; set; }
    public string? ImagingTestName { get; set; }
    public bool CanDownloadDirectly { get; set; }
    public bool HasPendingDownloadRequest { get; set; }
    public string FileUrl { get; set; } = string.Empty;
}

public class AuthorizedMemberMedicalRecordDetailResponse : AuthorizedMemberMedicalRecordResponse
{
    public string? AppointmentNotes { get; set; }
    public string? Summary { get; set; }
    public string? ClinicalDetails { get; set; }
}
