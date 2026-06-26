namespace HSCSAPI.Models.Laboratory;

public class LabTestTemplate
{
    public Guid LabTestTemplateId { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? LoincCode { get; set; }
    public string SpecimenType { get; set; } = string.Empty;
    public string? PreparationInstructions { get; set; }
    public string SourceUrl { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;

    public ICollection<LabTestFieldDefinition> Fields { get; set; } = new HashSet<LabTestFieldDefinition>();
    public ICollection<LabTestResult> Results { get; set; } = new HashSet<LabTestResult>();
}
