namespace HSCSAPI.Models.Standards;

public class RadiologyExamCatalog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string StandardSystem { get; set; } = string.Empty;
    public string Rpid { get; set; } = string.Empty;
    public string? LetterCode { get; set; }
    public string? ShortName { get; set; }
    public string? LongName { get; set; }
    public string? Modality { get; set; }
    public string? PlaybookType { get; set; }
    public string? BodyRegion { get; set; }
    public string? BodyRegion2 { get; set; }
    public string? ModalityModifier { get; set; }
    public string? ProcedureModifier { get; set; }
    public string? AnatomicFocus { get; set; }
    public string? Laterality { get; set; }
    public string? ReasonForExam { get; set; }
    public string? Technique { get; set; }
    public string? Pharmaceutical { get; set; }
    public string? View { get; set; }
    public string? Rids { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
