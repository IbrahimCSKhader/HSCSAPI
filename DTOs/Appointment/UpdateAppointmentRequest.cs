using System.ComponentModel.DataAnnotations;

namespace HSCSAPI.DTOs.Appointment;

public class UpdateAppointmentRequest
{
    [Required]
    public Guid DoctorId { get; set; }

    [Required]
    public Guid PatientId { get; set; }

    [Required]
    public DateOnly AppointmentDate { get; set; }

    [Required]
    public TimeOnly AppointmentTime { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}
