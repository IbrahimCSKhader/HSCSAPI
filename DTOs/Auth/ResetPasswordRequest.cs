namespace HSCSAPI.DTOs.Auth;

public class ResetPasswordRequest
{
    public string Email { get; set; } = string.Empty;
    public string VerificationCode { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
