namespace HSCSAPI.DTOs.Patient;

public class PatientResponse
{
    public Guid PatientId { get; set; }
    public string PatientUserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string? BloodType { get; set; }
    public Guid? ClinicId { get; set; }
    public string? ClinicName { get; set; }
    public bool EmailConfirmed { get; set; }
}
