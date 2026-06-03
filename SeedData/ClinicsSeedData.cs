namespace HSCSAPI.SeedData;

public static partial class ApplicationSeedData
{
    public static readonly IReadOnlyList<SeedClinic> Clinics =
    [
        new SeedClinic(
            Key: CentralClinicKey,
            Name: "Central Care Clinic",
            Address: "Hebron - Main Street"),
        new SeedClinic(
            Key: NorthClinicKey,
            Name: "North Family Clinic",
            Address: "Nablus - City Center")
    ];
}
