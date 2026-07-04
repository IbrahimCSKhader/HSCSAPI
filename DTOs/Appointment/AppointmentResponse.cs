namespace HSCSAPI.DTOs.Appointment;

public class AppointmentResponse
{
    public Guid AppointmentId { get; set; }
    public Guid DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public Guid AvailabilitySlotId { get; set; }
    public Guid? ClinicId { get; set; }
    public string? ClinicName { get; set; }
    public DateOnly AppointmentDate { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly AppointmentTime { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
