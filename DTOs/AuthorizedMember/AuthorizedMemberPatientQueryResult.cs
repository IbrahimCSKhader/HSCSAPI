using HSCSAPI.Models.Enums;

namespace HSCSAPI.DTOs.AuthorizedMember;

internal sealed class AuthorizedMemberPatientQueryResult
{
    public Guid PatientId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
    public string? Address { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public Gender Gender { get; init; }
    public BloodType? BloodType { get; init; }
    public string PatientUserId { get; init; } = string.Empty;
    public Guid? ClinicId { get; init; }
    public string? ClinicName { get; init; }
    public RelationshipType RelationshipType { get; init; }
    public DateTime AuthorizedAt { get; init; }
    public int MedicalRecordsCount { get; init; }
    public int UpcomingAppointmentsCount { get; init; }
}
