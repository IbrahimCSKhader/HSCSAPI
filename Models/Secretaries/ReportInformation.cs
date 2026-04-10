using HSCSAPI.Models.Enums;

namespace HSCSAPI.Models.Secretaries;

public class ReportInformation
{
    public Guid ReportInformationId { get; set; } = Guid.NewGuid();
    public Guid ReportId { get; set; }
    public ReportFileFormat FileFormat { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public long FileSizeInBytes { get; set; }

    public Report Report { get; set; } = null!;
}
