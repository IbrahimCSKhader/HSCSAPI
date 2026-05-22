namespace HSCSAPI.DTOs.Secretary;

public class SecretaryDashboardAppointmentResponse
{
    public Guid AppointmentId { get; set; }
    public Guid DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientUserId { get; set; } = string.Empty;
    public DateOnly AppointmentDate { get; set; }
    public TimeOnly AppointmentTime { get; set; }
    public string? Notes { get; set; }
    public bool HasReached { get; set; }
}
