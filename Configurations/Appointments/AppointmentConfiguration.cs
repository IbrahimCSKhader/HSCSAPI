using HSCSAPI.Models.Appointments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HSCSAPI.Configurations.Appointments;

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("Appointments");

        builder.HasKey(x => x.AppointmentId);

        builder.Property(x => x.AppointmentDate)
            .IsRequired();

        builder.Property(x => x.AppointmentTime)
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasMaxLength(1000);

        builder.HasIndex(x => x.AvailabilitySlotId)
            .IsUnique();

        builder.HasOne(x => x.Doctor)
            .WithMany(x => x.Appointments)
            .HasForeignKey(x => x.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Patient)
            .WithMany(x => x.Appointments)
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AvailabilitySlot)
            .WithOne(x => x.Appointment)
            .HasForeignKey<Appointment>(x => x.AvailabilitySlotId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
