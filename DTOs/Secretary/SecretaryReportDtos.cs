using System.ComponentModel.DataAnnotations;

namespace HSCSAPI.DTOs.Secretary;

public class GenerateSecretaryReportRequest
{
    [Required]
    public string ReportType { get; set; } = "clinic-summary";

    [Required]
    public string FileFormat { get; set; } = "csv";
}

public class SecretaryReportResponse
{
    public Guid ReportId { get; set; }
    public string ReportType { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public List<SecretaryReportFileResponse> Files { get; set; } = [];
}

public class SecretaryReportFileResponse
{
    public Guid ReportInformationId { get; set; }
    public string FileFormat { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSizeInBytes { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
}
