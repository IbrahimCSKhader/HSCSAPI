using System.ComponentModel.DataAnnotations;

namespace HSCSAPI.DTOs.Secretary;

public class GenerateSecretaryReportRequest
{
    [Required]
    public string ReportType { get; set; } = "ClinicOverview";

    [Required]
    public string FileFormat { get; set; } = "Pdf";

    public string? Period { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public int? Year { get; set; }
    public int? Month { get; set; }
}

public class SecretaryReportResponse
{
    public Guid ReportId { get; set; }
    public string ReportType { get; set; } = string.Empty;
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
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

public class SecretaryReportOptionsResponse
{
    public List<string> ReportTypes { get; set; } = [];
    public List<string> FileFormats { get; set; } = [];
    public List<string> Periods { get; set; } = [];
}
