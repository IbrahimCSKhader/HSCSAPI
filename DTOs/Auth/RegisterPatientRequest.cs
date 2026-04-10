namespace HSCSAPI.DTOs.Auth;

public class RegisterPatientRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string? BloodType { get; set; }
    public int ClinicId { get; set; }
}
