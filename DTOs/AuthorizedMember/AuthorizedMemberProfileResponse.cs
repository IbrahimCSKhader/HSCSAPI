namespace HSCSAPI.DTOs.AuthorizedMember;

public class AuthorizedMemberProfileResponse
{
    public Guid AuthorizedMemberId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public DateOnly? DateOfBirth { get; set; }
}
