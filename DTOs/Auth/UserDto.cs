namespace HSCSAPI.DTOs.Auth;

public class UserDto
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public string? UserID { get; set; }
    public List<string> Roles { get; set; } = new();
}
