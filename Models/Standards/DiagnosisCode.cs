namespace HSCSAPI.Models.Standards;

public class DiagnosisCode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CodeSystem { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? DisplayCode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
