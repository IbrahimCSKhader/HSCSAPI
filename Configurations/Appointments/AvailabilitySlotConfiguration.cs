using HSCSAPI.Models.Appointments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HSCSAPI.Configurations.Appointments;

public class AvailabilitySlotConfiguration : IEntityTypeConfiguration<AvailabilitySlot>
{
    public void Configure(EntityTypeBuilder<AvailabilitySlot> builder)
    {
        builder.ToTable("AvailabilitySlots");

        builder.HasKey(x => x.AvailabilitySlotId);

        builder.Property(x => x.DayOfWeek)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.SlotDate)
            .IsRequired();

        builder.Property(x => x.StartTime)
            .IsRequired();

        builder.Property(x => x.EndTime)
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasMaxLength(500);

        builder.Property(x => x.IsAvailable)
            .HasDefaultValue(true)
            .IsRequired();

        builder.HasIndex(x => new { x.DoctorId, x.SlotDate, x.StartTime, x.EndTime })
            .IsUnique();

        builder.HasOne(x => x.Doctor)
            .WithMany(x => x.AvailabilitySlots)
            .HasForeignKey(x => x.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
