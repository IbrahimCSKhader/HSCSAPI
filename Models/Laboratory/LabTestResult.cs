using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Profiles;

namespace HSCSAPI.Models.Laboratory;

public class LabTestResult
{
    public Guid LabTestResultId { get; set; } = Guid.NewGuid();
    public Guid LabTestRequestId { get; set; }
    public Guid LabTestTemplateId { get; set; }
    public Guid LaboratoryTechnologistId { get; set; }
    public int TemplateVersion { get; set; }
    public string AccessionNumber { get; set; } = string.Empty;
    public DateTime CollectedAt { get; set; }
    public DateTime ReceivedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public SpecimenCondition SpecimenCondition { get; set; }
    public string? SpecimenNotes { get; set; }
    public string? Comments { get; set; }
    public string? PdfFilePath { get; set; }
    public string? PdfChecksum { get; set; }
    public long? PdfFileSizeInBytes { get; set; }
    public DateTime? PdfGeneratedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public LabTestRequest LabTestRequest { get; set; } = null!;
    public LabTestTemplate Template { get; set; } = null!;
    public LaboratoryTechnologist LaboratoryTechnologist { get; set; } = null!;
    public ICollection<LabTestResultValue> Values { get; set; } = new HashSet<LabTestResultValue>();
}
