using HSCSAPI.Models.Identity;
using HSCSAPI.Models.Clinics;
using HSCSAPI.Models.MedicalFiles;
using HSCSAPI.Models.Secretaries;

namespace HSCSAPI.Models.Profiles;

public class Secretary
{
    public Guid SecretaryId { get; set; } = Guid.NewGuid();
    public Guid? ClinicId { get; set; }

    public User User { get; set; } = null!;
    public Clinic? Clinic { get; set; }
    public Clinic? ManagedClinic { get; set; }

    public ICollection<Report> Reports { get; set; } = new HashSet<Report>();
    public ICollection<FileDownloadRequest> ReviewedFileDownloadRequests { get; set; } = new HashSet<FileDownloadRequest>();
}
