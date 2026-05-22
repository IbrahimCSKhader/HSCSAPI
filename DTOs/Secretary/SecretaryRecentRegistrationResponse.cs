namespace HSCSAPI.DTOs.Secretary;

public class SecretaryRecentRegistrationResponse
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
}
