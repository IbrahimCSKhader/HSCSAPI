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
