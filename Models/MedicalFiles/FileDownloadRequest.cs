using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Profiles;

namespace HSCSAPI.Models.MedicalFiles;

public class FileDownloadRequest
{
    public Guid FileDownloadRequestId { get; set; } = Guid.NewGuid();
    public Guid PatientId { get; set; }
    public Guid MedicalFileId { get; set; }
    public Guid? ReviewedBySecretaryId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string PurposeDescription { get; set; } = string.Empty;
    public FileDownloadRequestStatus Status { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }

    public Patient Patient { get; set; } = null!;
    public MedicalFile MedicalFile { get; set; } = null!;
    public Secretary? ReviewedBySecretary { get; set; }
}
