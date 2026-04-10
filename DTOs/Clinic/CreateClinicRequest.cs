namespace HSCSAPI.DTOs.Clinic;

public class CreateClinicRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public Guid AdminSecretaryId { get; set; }
}
