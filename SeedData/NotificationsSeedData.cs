namespace HSCSAPI.SeedData;

public static partial class ApplicationSeedData
{
    public static readonly IReadOnlyList<SeedNotification> Notifications =
    [
        new SeedNotification(
            UserKey: PatientKey,
            Title: "Your seeded appointment is ready.",
            IsRead: false,
            Message: "Open your notification center to review the upcoming appointment."),
        new SeedNotification(
            UserKey: DoctorKey,
            Title: "You have a seeded appointment on Monday.",
            IsRead: true,
            Message: "The patient appointment is available in your doctor dashboard.")
    ];

    public static readonly SeedReminder PrimaryReminder = new(
        PatientKey: PatientKey,
        DoctorKey: DoctorKey,
        AuthorizedMemberKey: AuthorizedMemberKey,
        ReminderText: "Take your prescribed medication after breakfast.",
        ReminderDaysOffset: 1,
        Title: "Medication reminder",
        Category: "Appointment");
}
