using HSCSAPI.Models.Identity;
using HSCSAPI.Models.Laboratory;

namespace HSCSAPI.Models.Profiles;

public class LaboratoryTechnologist
{
    public Guid LaboratoryTechnologistId { get; set; } = Guid.NewGuid();
    public string ProfessionalLicenseNumber { get; set; } = string.Empty;

    public User User { get; set; } = null!;

    public ICollection<LabTestRequest> LabTestRequests { get; set; } = new HashSet<LabTestRequest>();
}
