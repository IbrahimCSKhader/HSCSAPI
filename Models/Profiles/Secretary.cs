using HSCSAPI.Models.Identity;
using HSCSAPI.Models.MedicalFiles;
using HSCSAPI.Models.Secretaries;

namespace HSCSAPI.Models.Profiles;

public class Secretary
{
    public int SecretaryId { get; set; }

    public User User { get; set; } = null!;

    public ICollection<Report> Reports { get; set; } = new HashSet<Report>();
    public ICollection<FileDownloadRequest> ReviewedFileDownloadRequests { get; set; } = new HashSet<FileDownloadRequest>();
}
