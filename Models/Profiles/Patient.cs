using HSCSAPI.Models.Appointments;
using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Identity;
using HSCSAPI.Models.MedicalFiles;
using HSCSAPI.Models.Relations;
using HSCSAPI.Models.Reminders;

namespace HSCSAPI.Models.Profiles;

public class Patient
{
    public Guid PatientId { get; set; } = Guid.NewGuid();
    public string UserID { get; set; } = string.Empty;
    public Gender Gender { get; set; }
    public BloodType? BloodType { get; set; }

    public User User { get; set; } = null!;

    public ICollection<Appointment> Appointments { get; set; } = new HashSet<Appointment>();
    public ICollection<Reminder> Reminders { get; set; } = new HashSet<Reminder>();
    public ICollection<PatientAuthorizedMember> AuthorizedMembers { get; set; } = new HashSet<PatientAuthorizedMember>();
    public ICollection<Invite> SentInvites { get; set; } = new HashSet<Invite>();
    public ICollection<FileDownloadRequest> FileDownloadRequests { get; set; } = new HashSet<FileDownloadRequest>();
}
