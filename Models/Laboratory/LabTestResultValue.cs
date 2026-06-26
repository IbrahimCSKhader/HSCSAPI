using HSCSAPI.Models.Enums;

namespace HSCSAPI.Models.Laboratory;

public class LabTestResultValue
{
    public Guid LabTestResultValueId { get; set; } = Guid.NewGuid();
    public Guid LabTestResultId { get; set; }
    public Guid LabTestFieldDefinitionId { get; set; }
    public string FieldCode { get; set; } = string.Empty;
    public string FieldLabel { get; set; } = string.Empty;
    public LabResultValueType ValueType { get; set; }
    public decimal? NumericValue { get; set; }
    public string? TextValue { get; set; }
    public string? Unit { get; set; }
    public string? ReferenceRange { get; set; }
    public LabResultFlag? Flag { get; set; }
    public int DisplayOrder { get; set; }

    public LabTestResult LabTestResult { get; set; } = null!;
    public LabTestFieldDefinition FieldDefinition { get; set; } = null!;
}
