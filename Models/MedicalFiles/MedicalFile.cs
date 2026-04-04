using HSCSAPI.Models.Appointments;
using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Laboratory;
using HSCSAPI.Models.Profiles;
using HSCSAPI.Models.Radiology;

namespace HSCSAPI.Models.MedicalFiles;

public class MedicalFile
{
    public int MedicalFileId { get; set; }
    public int AppointmentId { get; set; }
    public int UploadedByDoctorId { get; set; }
    public MedicalFileType FileType { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string EncryptedChecksum { get; set; } = string.Empty;
    public long FileSizeInBytes { get; set; }
    public SeverityLevel SeverityLevel { get; set; }
    public DateTime UploadedAt { get; set; }

    public Appointment Appointment { get; set; } = null!;
    public Doctor UploadedByDoctor { get; set; } = null!;

    public ICollection<FileDownloadRequest> FileDownloadRequests { get; set; } = new HashSet<FileDownloadRequest>();
    public ICollection<LabTestRequest> LabTestRequestsAsResult { get; set; } = new HashSet<LabTestRequest>();
    public ICollection<ImagingTestRequest> ImagingTestRequestsAsResult { get; set; } = new HashSet<ImagingTestRequest>();
}
