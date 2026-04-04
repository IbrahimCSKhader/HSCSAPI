using HSCSAPI.Models.Identity;
using HSCSAPI.Models.Laboratory;

namespace HSCSAPI.Models.Profiles;

public class LaboratoryTechnologist
{
    public int LaboratoryTechnologistId { get; set; }
    public string ProfessionalLicenseNumber { get; set; } = string.Empty;

    public User User { get; set; } = null!;

    public ICollection<LabTestRequest> LabTestRequests { get; set; } = new HashSet<LabTestRequest>();
}
