namespace HSCSAPI.DTOs.Auth;

public class VerifyRegistrationCodeRequest
{
    public string Email { get; set; } = string.Empty;
    public string VerificationCode { get; set; } = string.Empty;
}
