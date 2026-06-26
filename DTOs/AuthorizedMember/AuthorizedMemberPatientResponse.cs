namespace HSCSAPI.DTOs.AuthorizedMember;

public class AuthorizedMemberPatientResponse
{
    public Guid PatientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string? BloodType { get; set; }
    public string PatientUserId { get; set; } = string.Empty;
    public Guid? ClinicId { get; set; }
    public string? ClinicName { get; set; }
    public string RelationshipType { get; set; } = string.Empty;
    public DateTime AuthorizedAt { get; set; }
    public int MedicalRecordsCount { get; set; }
    public int UpcomingAppointmentsCount { get; set; }
    public bool CanViewRecords { get; set; }
    public bool CanViewAppointments { get; set; }
}
