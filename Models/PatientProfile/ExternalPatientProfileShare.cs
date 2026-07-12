using HSCSAPI.Models.Profiles;

namespace HSCSAPI.Models.PatientProfile;

public class ExternalPatientProfileShare
{
    public Guid ExternalPatientProfileShareId { get; set; } = Guid.NewGuid();
    public Guid PatientId { get; set; }
    public string DoctorEmail { get; set; } = string.Empty;
    public string? DoctorName { get; set; }
    public string ShareToken { get; set; } = string.Empty;
    public string ShareTokenHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastCodeSentAt { get; set; }
    public string? VerificationCodeHash { get; set; }
    public DateTime? VerificationCodeExpiresAt { get; set; }
    public string? AccessSessionTokenHash { get; set; }
    public DateTime? AccessSessionExpiresAt { get; set; }
    public DateTime? LastAccessedAt { get; set; }

    public Patient Patient { get; set; } = null!;
}
