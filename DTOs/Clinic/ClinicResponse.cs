namespace HSCSAPI.DTOs.Clinic;

public class ClinicResponse
{
    public Guid ClinicId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public Guid CreatedBySuperAdminUserId { get; set; }
    public Guid? AdminSecretaryId { get; set; }
}
