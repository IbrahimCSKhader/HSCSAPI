using HSCSAPI.Models.Clinics;
using HSCSAPI.Models.Chats;
using HSCSAPI.Models.Notifications;
using HSCSAPI.Models.Profiles;
using Microsoft.AspNetCore.Identity;

namespace HSCSAPI.Models.Identity;

public class User : IdentityUser<Guid>
{
    public string Name { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Address { get; set; }
    public Guid? ClinicId { get; set; }

    public Patient? PatientProfile { get; set; }
    public Doctor? DoctorProfile { get; set; }
    public Secretary? SecretaryProfile { get; set; }
    public AuthorizedMember? AuthorizedMemberProfile { get; set; }
    public LaboratoryTechnologist? LaboratoryTechnologistProfile { get; set; }
    public RadiologyTechnologist? RadiologyTechnologistProfile { get; set; }
    public Clinic? Clinic { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new HashSet<UserRole>();
    public ICollection<Notification> Notifications { get; set; } = new HashSet<Notification>();
    public ICollection<Clinic> CreatedClinics { get; set; } = new HashSet<Clinic>();
    public ICollection<UserVerificationCode> VerificationCodes { get; set; } = new HashSet<UserVerificationCode>();
    public ICollection<Chat> ChatsAsUserOne { get; set; } = new HashSet<Chat>();
    public ICollection<Chat> ChatsAsUserTwo { get; set; } = new HashSet<Chat>();
    public ICollection<ChatMessage> SentChatMessages { get; set; } = new HashSet<ChatMessage>();
}
