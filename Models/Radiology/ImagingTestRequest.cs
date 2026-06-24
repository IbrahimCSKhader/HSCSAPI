using HSCSAPI.Models.Clinics;
using HSCSAPI.Models.MedicalFiles;
using HSCSAPI.Models.Profiles;

namespace HSCSAPI.Models.Radiology;

public class ImagingTestRequest
{
    public Guid ImagingTestRequestId { get; set; } = Guid.NewGuid();
    public string TestName { get; set; } = string.Empty;
    public Guid? PatientId { get; set; }
    public Guid? RequestedByDoctorId { get; set; }
    public Guid? RadiologyClinicId { get; set; }
    public string? ImagingCode { get; set; }
    public string? BodyRegion { get; set; }
    public string Priority { get; set; } = "Routine";
    public string? ClinicalNotes { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public Guid? RadiologyTechnologistId { get; set; }
    public Guid? ResultMedicalFileId { get; set; }

    public Patient? Patient { get; set; }
    public Doctor? RequestedByDoctor { get; set; }
    public Clinic? RadiologyClinic { get; set; }
    public RadiologyTechnologist? RadiologyTechnologist { get; set; }
    public MedicalFile? ResultMedicalFile { get; set; }
}
