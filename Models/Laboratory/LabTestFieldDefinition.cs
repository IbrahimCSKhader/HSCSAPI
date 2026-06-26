using HSCSAPI.Models.Enums;

namespace HSCSAPI.Models.Laboratory;

public class LabTestFieldDefinition
{
    public Guid LabTestFieldDefinitionId { get; set; } = Guid.NewGuid();
    public Guid LabTestTemplateId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? LoincCode { get; set; }
    public LabResultValueType ValueType { get; set; }
    public string? Unit { get; set; }
    public bool IsRequired { get; set; } = true;
    public int? DecimalPlaces { get; set; }
    public string? ReferenceRange { get; set; }
    public string? AllowedValuesJson { get; set; }
    public int DisplayOrder { get; set; }

    public LabTestTemplate Template { get; set; } = null!;
    public ICollection<LabTestResultValue> ResultValues { get; set; } = new HashSet<LabTestResultValue>();
}
