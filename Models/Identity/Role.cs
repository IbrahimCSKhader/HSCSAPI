namespace HSCSAPI.Models.Identity;

public class Role
{
    public int RoleId { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<UserRole> UserRoles { get; set; } = new HashSet<UserRole>();
}
