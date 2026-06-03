using HSCSAPI.Models.Enums;

namespace HSCSAPI.SeedData;

public static partial class ApplicationSeedData
{
    public static readonly SeedReport PrimaryReport = new(
        SecretaryKey: AdminSecretaryKey,
        GeneratedDaysOffset: -2);

    public static readonly SeedReportInformation PrimaryReportInformation = new(
        FileFormat: ReportFileFormat.Pdf,
        FilePath: "/seed/reports/weekly-clinic-report.pdf",
        FileSizeInBytes: 2048);
}
