using HSCSAPI.DTOs.Common;

namespace HSCSAPI.DTOs.Doctor;

public class DoctorDashboardResponse
{
    public Guid DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public Guid? ClinicId { get; set; }
    public string? ClinicName { get; set; }
    public DateOnly Today { get; set; }
    public DateOnly WeekStartDate { get; set; }
    public DateOnly WeekEndDate { get; set; }
    public int TodayAppointmentsCount { get; set; }
    public int PendingLabRequestsCount { get; set; }
    public int PendingImagingRequestsCount { get; set; }
    public int TotalPendingClinicalRequestsCount { get; set; }
    public int UnreadMessagesCount { get; set; }
    public List<DoctorAppointmentSummaryResponse> UpcomingAppointments { get; set; } = [];
    public List<DoctorWeeklyAppointmentCountResponse> WeeklyAppointments { get; set; } = [];
    public DoctorClinicalRequestsSummaryResponse ClinicalRequests { get; set; } = new();
}

public class DoctorClinicalRequestsSummaryResponse
{
    public int TotalPending { get; set; }
    public int LabRequests { get; set; }
    public int ImagingRequests { get; set; }
}

public class DoctorWeeklyAppointmentCountResponse
{
    public DateOnly Date { get; set; }
    public string DayOfWeek { get; set; } = string.Empty;
    public int AppointmentsCount { get; set; }
}

public class DoctorAppointmentsScheduleResponse
{
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public int TotalAppointmentsCount { get; set; }
    public List<DoctorScheduleDayResponse> Days { get; set; } = [];
}

public class DoctorScheduleDayResponse
{
    public DateOnly Date { get; set; }
    public string DayOfWeek { get; set; } = string.Empty;
    public bool IsToday { get; set; }
    public List<DoctorAppointmentSummaryResponse> Appointments { get; set; } = [];
}

public class DoctorAppointmentSummaryResponse
{
    public Guid AppointmentId { get; set; }
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientUserId { get; set; } = string.Empty;
    public DateOnly AppointmentDate { get; set; }
    public string DayOfWeek { get; set; } = string.Empty;
    public TimeOnly AppointmentTime { get; set; }
    public int DurationMinutes { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ReasonForVisit { get; set; }
}

public class DoctorAppointmentDetailResponse : DoctorAppointmentSummaryResponse
{
    public Guid DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public Guid? ClinicId { get; set; }
    public string? ClinicName { get; set; }
}

public class ChangeDoctorPasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

public class ChangeDoctorPasswordResponse : ApiResponse
{
}

public class DoctorMedicalRecordsResponse
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public DoctorMedicalRecordTypeCountsResponse TypeCounts { get; set; } = new();
    public List<DoctorMedicalRecordResponse> Items { get; set; } = [];
}

public class DoctorMedicalRecordTypeCountsResponse
{
    public int All { get; set; }
    public int LabTest { get; set; }
    public int ImagingTest { get; set; }
    public int Visit { get; set; }
}

public class DoctorMedicalRecordResponse
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
    public Guid? ClinicId { get; set; }
    public string? ClinicName { get; set; }
    public Guid RecordedByDoctorId { get; set; }
    public string RecordedByDoctorName { get; set; } = string.Empty;
    public DateOnly AppointmentDate { get; set; }
    public TimeOnly AppointmentTime { get; set; }
    public string? LabTestName { get; set; }
    public string? ImagingTestName { get; set; }
    public string FileUrl { get; set; } = string.Empty;
}

public class DoctorMedicalRecordDetailResponse : DoctorMedicalRecordResponse
{
    public string? AppointmentNotes { get; set; }
    public string? Summary { get; set; }
    public string? ClinicalDetails { get; set; }
}
