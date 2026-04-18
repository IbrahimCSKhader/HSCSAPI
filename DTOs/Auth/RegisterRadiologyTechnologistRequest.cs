namespace HSCSAPI.DTOs.Auth;

public class RegisterRadiologyTechnologistRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string ProfessionalLicenseNumber { get; set; } = string.Empty;
}