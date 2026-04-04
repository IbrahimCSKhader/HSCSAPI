using HSCSAPI.Models.MedicalFiles;
using HSCSAPI.Models.Profiles;

namespace HSCSAPI.Models.Laboratory;

public class LabTestRequest
{
    public int LabTestRequestId { get; set; }
    public string TestName { get; set; } = string.Empty;
    public int? LaboratoryTechnologistId { get; set; }
    public int? ResultMedicalFileId { get; set; }

    public LaboratoryTechnologist? LaboratoryTechnologist { get; set; }
    public MedicalFile? ResultMedicalFile { get; set; }
}
