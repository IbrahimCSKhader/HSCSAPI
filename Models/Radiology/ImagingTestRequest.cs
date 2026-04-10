using HSCSAPI.Models.MedicalFiles;
using HSCSAPI.Models.Profiles;

namespace HSCSAPI.Models.Radiology;

public class ImagingTestRequest
{
    public Guid ImagingTestRequestId { get; set; } = Guid.NewGuid();
    public string TestName { get; set; } = string.Empty;
    public Guid? RadiologyTechnologistId { get; set; }
    public Guid? ResultMedicalFileId { get; set; }

    public RadiologyTechnologist? RadiologyTechnologist { get; set; }
    public MedicalFile? ResultMedicalFile { get; set; }
}
