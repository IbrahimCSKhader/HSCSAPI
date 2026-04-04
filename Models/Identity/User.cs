using HSCSAPI.Models.Notifications;
using HSCSAPI.Models.Profiles;

namespace HSCSAPI.Models.Identity;

public class User
{
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public string PasswordHash { get; set; } = string.Empty;

    public Patient? PatientProfile { get; set; }
    public Doctor? DoctorProfile { get; set; }
    public Secretary? SecretaryProfile { get; set; }
    public AuthorizedMember? AuthorizedMemberProfile { get; set; }
    public LaboratoryTechnologist? LaboratoryTechnologistProfile { get; set; }
    public RadiologyTechnologist? RadiologyTechnologistProfile { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new HashSet<UserRole>();
    public ICollection<Notification> Notifications { get; set; } = new HashSet<Notification>();
}
