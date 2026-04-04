using HSCSAPI.Models.Profiles;

namespace HSCSAPI.Models.Appointments;

public class AvailabilitySlot
{
    public int AvailabilitySlotId { get; set; }
    public int DoctorId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsAvailable { get; set; }

    public Doctor Doctor { get; set; } = null!;
    public Appointment? Appointment { get; set; }
}
