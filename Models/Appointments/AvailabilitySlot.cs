using HSCSAPI.Models.Profiles;

namespace HSCSAPI.Models.Appointments;

public class AvailabilitySlot
{
    public Guid AvailabilitySlotId { get; set; } = Guid.NewGuid();
    public Guid DoctorId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsAvailable { get; set; }

    public Doctor Doctor { get; set; } = null!;
    public Appointment? Appointment { get; set; }
}
