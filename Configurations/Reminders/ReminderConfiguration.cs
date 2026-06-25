using HSCSAPI.Models.Reminders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HSCSAPI.Configurations.Reminders;

public class ReminderConfiguration : IEntityTypeConfiguration<Reminder>
{
    public void Configure(EntityTypeBuilder<Reminder> builder)
    {
        builder.ToTable("Reminders");

        builder.HasKey(x => x.ReminderId);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200)
            .HasDefaultValue("Reminder");

        builder.Property(x => x.ReminderText)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.Category)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("General");

        builder.Property(x => x.ReminderAt)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(x => new { x.DoctorId, x.DismissedAt, x.ReminderAt });
        builder.HasIndex(x => new { x.PatientId, x.DismissedAt, x.ReminderAt });
        builder.HasIndex(x => new { x.AuthorizedMemberId, x.DismissedAt, x.ReminderAt });

        builder.HasOne(x => x.Patient)
            .WithMany(x => x.Reminders)
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Doctor)
            .WithMany(x => x.Reminders)
            .HasForeignKey(x => x.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AuthorizedMember)
            .WithMany(x => x.Reminders)
            .HasForeignKey(x => x.AuthorizedMemberId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
