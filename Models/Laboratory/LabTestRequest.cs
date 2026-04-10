using HSCSAPI.Models.MedicalFiles;
using HSCSAPI.Models.Profiles;

namespace HSCSAPI.Models.Laboratory;

public class LabTestRequest
{
    public Guid LabTestRequestId { get; set; } = Guid.NewGuid();
    public string TestName { get; set; } = string.Empty;
    public Guid? LaboratoryTechnologistId { get; set; }
    public Guid? ResultMedicalFileId { get; set; }

    public LaboratoryTechnologist? LaboratoryTechnologist { get; set; }
    public MedicalFile? ResultMedicalFile { get; set; }
}
