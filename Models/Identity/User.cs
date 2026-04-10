using HSCSAPI.Models.Notifications;
using HSCSAPI.Models.Profiles;
using HSCSAPI.Models.Clinics;

namespace HSCSAPI.Models.Identity;

public class User
{
    public Guid UserId { get; set; } = Guid.NewGuid();
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
    public ICollection<Clinic> CreatedClinics { get; set; } = new HashSet<Clinic>();
}
