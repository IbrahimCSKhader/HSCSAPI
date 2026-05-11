namespace HSCSAPI.DTOs.RadiologyTechnologist;

public class RadiologyTechnologistResponse
{
    public Guid RadiologyTechnologistId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public Guid? ClinicId { get; set; }
    public string? ClinicName { get; set; }
    public string ProfessionalLicenseNumber { get; set; } = string.Empty;
    public bool EmailConfirmed { get; set; }
}
