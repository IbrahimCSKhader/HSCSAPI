using HSCSAPI.Models.Reminders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HSCSAPI.Configurations.Reminders;

public class ReminderPreferenceConfiguration : IEntityTypeConfiguration<ReminderPreference>
{
    public void Configure(EntityTypeBuilder<ReminderPreference> builder)
    {
        builder.ToTable("ReminderPreferences");

        builder.HasKey(x => x.UserId);

        builder.Property(x => x.AppointmentRemindersEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.LabResultRemindersEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.MessageRemindersEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.InAppNotificationsEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.EmailRemindersEnabled)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(x => x.User)
            .WithOne()
            .HasForeignKey<ReminderPreference>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
