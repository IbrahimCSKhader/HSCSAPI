namespace HSCSAPI.DTOs.Doctor;

public class UpdateDoctorRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public Guid ClinicId { get; set; }
    public string ProfessionalLicenseNumber { get; set; } = string.Empty;
}
