using HSCSAPI.Models.Enums;

namespace HSCSAPI.DTOs.Auth;

public class VerifyCodeRequest
{
    public string Email { get; set; } = string.Empty;
    public string VerificationCode { get; set; } = string.Empty;
    public VerificationPurpose Purpose { get; set; } = VerificationPurpose.PasswordReset;
}
