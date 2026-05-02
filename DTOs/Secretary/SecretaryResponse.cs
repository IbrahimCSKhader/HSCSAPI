namespace HSCSAPI.DTOs.Secretary;

public class SecretaryResponse
{
    public Guid SecretaryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public Guid? ClinicId { get; set; }
    public string? ClinicName { get; set; }
    public Guid? ManagedClinicId { get; set; }
    public string? ManagedClinicName { get; set; }
    public bool IsClinicAdmin { get; set; }
}
