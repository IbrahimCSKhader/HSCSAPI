using Microsoft.AspNetCore.Identity;

namespace HSCSAPI.Models.Identity;

public class Role : IdentityRole<Guid>
{
    public ICollection<UserRole> UserRoles { get; set; } = new HashSet<UserRole>();
}
