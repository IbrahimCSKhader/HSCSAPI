using System.ComponentModel.DataAnnotations;

namespace HSCSAPI.DTOs.Appointment;

public class AvailabilitySlotResponse
{
    public Guid AvailabilitySlotId { get; set; }
    public Guid DoctorId { get; set; }
    public DateOnly SlotDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "available";
    public string? PatientName { get; set; }
}

public class CreateAvailabilitySlotRequest
{
    [Required]
    public DateOnly SlotDate { get; set; }

    [Required]
    public TimeOnly StartTime { get; set; }

    [Required]
    public TimeOnly EndTime { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
