using HSCSAPI.Models.Appointments;
using HSCSAPI.Models.Identity;
using HSCSAPI.Models.MedicalFiles;
using HSCSAPI.Models.Reminders;

namespace HSCSAPI.Models.Profiles;

public class Doctor
{
    public Guid DoctorId { get; set; } = Guid.NewGuid();
    public string ProfessionalLicenseNumber { get; set; } = string.Empty;

    public User User { get; set; } = null!;

    public ICollection<AvailabilitySlot> AvailabilitySlots { get; set; } = new HashSet<AvailabilitySlot>();
    public ICollection<Appointment> Appointments { get; set; } = new HashSet<Appointment>();
    public ICollection<Reminder> Reminders { get; set; } = new HashSet<Reminder>();
    public ICollection<MedicalFile> UploadedMedicalFiles { get; set; } = new HashSet<MedicalFile>();
}
