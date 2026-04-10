using HSCSAPI.Models.Profiles;

namespace HSCSAPI.Models.Secretaries;

public class Report
{
    public Guid ReportId { get; set; } = Guid.NewGuid();
    public Guid SecretaryId { get; set; }
    public DateTime GeneratedAt { get; set; }

    public Secretary Secretary { get; set; } = null!;
    public ICollection<ReportInformation> ReportInformations { get; set; } = new HashSet<ReportInformation>();
}
