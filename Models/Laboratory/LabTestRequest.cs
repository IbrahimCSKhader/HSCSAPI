using HSCSAPI.Models.Clinics;
using HSCSAPI.Models.MedicalFiles;
using HSCSAPI.Models.Profiles;

namespace HSCSAPI.Models.Laboratory;

public class LabTestRequest
{
    public Guid LabTestRequestId { get; set; } = Guid.NewGuid();
    public string TestName { get; set; } = string.Empty;
    public Guid? PatientId { get; set; }
    public Guid? RequestedByDoctorId { get; set; }
    public Guid? TestingClinicId { get; set; }
    public string? LoincCode { get; set; }
    public string Priority { get; set; } = "Routine";
    public string? ClinicalNotes { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public Guid? LaboratoryTechnologistId { get; set; }
    public Guid? ResultMedicalFileId { get; set; }

    public Patient? Patient { get; set; }
    public Doctor? RequestedByDoctor { get; set; }
    public Clinic? TestingClinic { get; set; }
    public LaboratoryTechnologist? LaboratoryTechnologist { get; set; }
    public MedicalFile? ResultMedicalFile { get; set; }
    public LabTestResult? StructuredResult { get; set; }
}
