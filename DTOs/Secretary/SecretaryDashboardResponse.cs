namespace HSCSAPI.DTOs.Secretary;

public class SecretaryDashboardResponse
{
    public Guid ClinicId { get; set; }
    public string ClinicName { get; set; } = string.Empty;
    public int PendingRequestsCount { get; set; }
    public int PatientRegistrationsCount { get; set; }
    public int ReportsGeneratedCount { get; set; }
    public int TodayAvailabilitySlotsCount { get; set; }
    public int TodayAppointmentsCount { get; set; }
    public int TodayAppointmentsReachedCount { get; set; }
    public List<SecretaryDashboardAppointmentResponse> TodayAppointments { get; set; } = [];
    public List<SecretaryRecentRegistrationResponse> RecentRegistrations { get; set; } = [];
}
