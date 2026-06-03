namespace HSCSAPI.SeedData;

public static partial class ApplicationSeedData
{
    public static readonly SeedAvailabilitySlot PrimaryAvailabilitySlot = new(
        DoctorKey: DoctorKey,
        DayOfWeek: DayOfWeek.Monday,
        StartTime: new TimeOnly(9, 0),
        EndTime: new TimeOnly(9, 30),
        IsAvailable: false);

    public static readonly SeedAppointment PrimaryAppointment = new(
        DoctorKey: DoctorKey,
        PatientKey: PatientKey,
        DayOfWeek: DayOfWeek.Monday,
        AppointmentTime: new TimeOnly(9, 0),
        Notes: "Initial seeded appointment.");
}
