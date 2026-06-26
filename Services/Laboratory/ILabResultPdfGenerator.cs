namespace HSCSAPI.Services.Laboratory;

public interface ILabResultPdfGenerator
{
    byte[] Generate(LabResultPdfDocument document);
}

public sealed class LabResultPdfDocument
{
    public string AccessionNumber { get; init; } = string.Empty;
    public string TestName { get; init; } = string.Empty;
    public string? LoincCode { get; init; }
    public string PatientName { get; init; } = string.Empty;
    public string PatientUserId { get; init; } = string.Empty;
    public string? DateOfBirth { get; init; }
    public string? Gender { get; init; }
    public string DoctorName { get; init; } = string.Empty;
    public string LaboratoryTechnologistName { get; init; } = string.Empty;
    public string ClinicName { get; init; } = string.Empty;
    public string SpecimenType { get; init; } = string.Empty;
    public string SpecimenCondition { get; init; } = string.Empty;
    public string? SpecimenNotes { get; init; }
    public DateTime CollectedAt { get; init; }
    public DateTime ReceivedAt { get; init; }
    public DateTime CompletedAt { get; init; }
    public string? Comments { get; init; }
    public IReadOnlyList<LabResultPdfValue> Values { get; init; } = [];
}

public sealed record LabResultPdfValue(
    string Label,
    string Value,
    string? Flag,
    string? Unit,
    string? ReferenceRange);
