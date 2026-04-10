using HSCSAPI.Models.Identity;
using HSCSAPI.Models.Radiology;

namespace HSCSAPI.Models.Profiles;

public class RadiologyTechnologist
{
    public Guid RadiologyTechnologistId { get; set; } = Guid.NewGuid();
    public string ProfessionalLicenseNumber { get; set; } = string.Empty;

    public User User { get; set; } = null!;

    public ICollection<ImagingTestRequest> ImagingTestRequests { get; set; } = new HashSet<ImagingTestRequest>();
}
