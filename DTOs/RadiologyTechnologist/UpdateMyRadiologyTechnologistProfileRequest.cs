namespace HSCSAPI.DTOs.RadiologyTechnologist;

public class UpdateMyRadiologyTechnologistProfileRequest
{
    public string Name { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public DateOnly? DateOfBirth { get; set; }
}
