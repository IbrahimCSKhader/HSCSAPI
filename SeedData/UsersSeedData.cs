using HSCSAPI.Models.Enums;

namespace HSCSAPI.SeedData;

public static partial class ApplicationSeedData
{
    public static readonly IReadOnlyList<SeedUser> Users =
    [
        new SeedUser(
            Key: AdminSecretaryKey,
            Email: "secretary.admin@seed.local",
            Name: "Clinic Admin Secretary",
            Role: UserSystemRole.Secretary,
            ClinicKey: CentralClinicKey,
            PhoneNumber: "0599000001",
            Address: "Hebron",
            DateOfBirth: new DateOnly(1992, 4, 10)),
        new SeedUser(
            Key: StaffSecretaryKey,
            Email: "secretary.staff@seed.local",
            Name: "Clinic Staff Secretary",
            Role: UserSystemRole.Secretary,
            ClinicKey: CentralClinicKey,
            PhoneNumber: "0599000002",
            Address: "Hebron",
            DateOfBirth: new DateOnly(1994, 8, 15)),
        new SeedUser(
            Key: DoctorKey,
            Email: "doctor@seed.local",
            Name: "Dr. Samer Khalil",
            Role: UserSystemRole.Doctor,
            ClinicKey: CentralClinicKey,
            PhoneNumber: "0599000003",
            Address: "Hebron",
            DateOfBirth: new DateOnly(1985, 1, 20),
            ProfessionalLicenseNumber: "DOC-10001"),
        new SeedUser(
            Key: PatientKey,
            Email: "patient@seed.local",
            Name: "Alaa Nassar",
            Role: UserSystemRole.Patient,
            ClinicKey: CentralClinicKey,
            PhoneNumber: "0599000004",
            Address: "Hebron",
            DateOfBirth: new DateOnly(1998, 6, 4),
            Gender: Gender.Male,
            BloodType: BloodType.OPositive),
        new SeedUser(
            Key: AuthorizedMemberKey,
            Email: "member@seed.local",
            Name: "Mona Nassar",
            Role: UserSystemRole.AuthorizedMember,
            ClinicKey: null,
            PhoneNumber: "0599000005",
            Address: "Hebron",
            DateOfBirth: new DateOnly(1990, 11, 2)),
        new SeedUser(
            Key: LaboratoryTechnologistKey,
            Email: "labtech@seed.local",
            Name: "Rana Lab",
            Role: UserSystemRole.LaboratoryTechnologist,
            ClinicKey: CentralClinicKey,
            PhoneNumber: "0599000006",
            Address: "Hebron",
            DateOfBirth: new DateOnly(1991, 9, 12),
            ProfessionalLicenseNumber: "LAB-10001"),
        new SeedUser(
            Key: RadiologyTechnologistKey,
            Email: "radiology@seed.local",
            Name: "Yousef Ray",
            Role: UserSystemRole.RadiologyTechnologist,
            ClinicKey: CentralClinicKey,
            PhoneNumber: "0599000007",
            Address: "Hebron",
            DateOfBirth: new DateOnly(1993, 3, 8),
            ProfessionalLicenseNumber: "RAD-10001")
    ];
}
