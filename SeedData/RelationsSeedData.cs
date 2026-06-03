using HSCSAPI.Models.Enums;

namespace HSCSAPI.SeedData;

public static partial class ApplicationSeedData
{
    public static readonly SeedPatientAuthorization PrimaryPatientAuthorization = new(
        PatientKey: PatientKey,
        AuthorizedMemberKey: AuthorizedMemberKey,
        RelationshipType: RelationshipType.Mother,
        AuthorizedDaysOffset: -10);

    public static readonly SeedInvite PrimaryInvite = new(
        PatientKey: PatientKey,
        AuthorizedMemberKey: AuthorizedMemberKey,
        RelationshipType: RelationshipType.Mother,
        Status: InviteStatus.Accepted,
        SentDaysOffset: -12,
        RespondedDaysOffset: -11);
}
