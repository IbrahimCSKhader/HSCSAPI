using HSCSAPI.Models.MedicalFiles;
using HSCSAPI.Models.Profiles;

namespace HSCSAPI.Models.Appointments;

public class Appointment
{
    public Guid AppointmentId { get; set; } = Guid.NewGuid();
    public Guid DoctorId { get; set; }
    public Guid PatientId { get; set; }
    public Guid AvailabilitySlotId { get; set; }
    public DateOnly AppointmentDate { get; set; }
    public TimeOnly AppointmentTime { get; set; }
    public string? Notes { get; set; }

    public Doctor Doctor { get; set; } = null!;
    public Patient Patient { get; set; } = null!;
    public AvailabilitySlot AvailabilitySlot { get; set; } = null!;

    public ICollection<MedicalFile> MedicalFiles { get; set; } = new HashSet<MedicalFile>();
}
