namespace HSCSAPI.DTOs.AuthorizedMember;

public class AuthorizedMemberInviteResponse
{
    public Guid InviteId { get; set; }
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientEmail { get; set; } = string.Empty;
    public string PatientUserId { get; set; } = string.Empty;
    public Guid? ClinicId { get; set; }
    public string? ClinicName { get; set; }
    public string RelationshipType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public bool IsActive { get; set; }
}
