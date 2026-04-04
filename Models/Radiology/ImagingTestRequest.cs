using HSCSAPI.Models.MedicalFiles;
using HSCSAPI.Models.Profiles;

namespace HSCSAPI.Models.Radiology;

public class ImagingTestRequest
{
    public int ImagingTestRequestId { get; set; }
    public string TestName { get; set; } = string.Empty;
    public int? RadiologyTechnologistId { get; set; }
    public int? ResultMedicalFileId { get; set; }

    public RadiologyTechnologist? RadiologyTechnologist { get; set; }
    public MedicalFile? ResultMedicalFile { get; set; }
}
