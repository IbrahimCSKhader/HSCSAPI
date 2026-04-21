using HSCSAPI.Models.Identity;
using HSCSAPI.Models.Profiles;

namespace HSCSAPI.Models.Clinics;

public class Clinic
{
        public Guid ClinicId { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string? Address { get; set; }

        public Guid CreatedBySuperAdminUserId { get; set; }
        public Guid? AdminSecretaryId { get; set; }

    public User CreatedBySuperAdminUser { get; set; } = null!;
    public Secretary? AdminSecretary { get; set; }
    public ICollection<Secretary> Secretaries { get; set; } = new HashSet<Secretary>();
}
