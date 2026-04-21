using HSCSAPI.Models.Enums;

namespace HSCSAPI.Models.Identity;

public class UserVerificationCode
{
    public Guid UserVerificationCodeId { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public VerificationPurpose Purpose { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
}
