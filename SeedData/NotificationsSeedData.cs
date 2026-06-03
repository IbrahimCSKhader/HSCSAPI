namespace HSCSAPI.SeedData;

public static partial class ApplicationSeedData
{
    public static readonly IReadOnlyList<SeedNotification> Notifications =
    [
        new SeedNotification(
            UserKey: PatientKey,
            Title: "Your seeded appointment is ready.",
            IsRead: false),
        new SeedNotification(
            UserKey: DoctorKey,
            Title: "You have a seeded appointment on Monday.",
            IsRead: true)
    ];

    public static readonly SeedReminder PrimaryReminder = new(
        PatientKey: PatientKey,
        DoctorKey: DoctorKey,
        AuthorizedMemberKey: AuthorizedMemberKey,
        ReminderText: "Take your prescribed medication after breakfast.",
        ReminderDaysOffset: 1);
}
