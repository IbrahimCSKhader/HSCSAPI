
using System.ComponentModel.DataAnnotations;

namespace HSCSAPI.DTOs.Auth;

public class ResendVerificationCodeRequest
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = string.Empty;
}