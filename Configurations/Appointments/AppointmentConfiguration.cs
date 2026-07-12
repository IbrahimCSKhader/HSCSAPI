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

        builder.Property(x => x.TreatmentId)
            .HasMaxLength(100);

        builder.Property(x => x.TreatmentName)
            .HasMaxLength(200);

        builder.Property(x => x.Notes)
            .HasMaxLength(1000);

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasIndex(x => new { x.DoctorId, x.AppointmentDate, x.AppointmentTime })
            .IsUnique()
            .HasFilter("[IsActive] = 1");

        builder.HasIndex(x => new { x.PatientId, x.AppointmentDate, x.AppointmentTime })
            .IsUnique()
            .HasFilter("[IsActive] = 1");

        builder.HasOne(x => x.Doctor)
            .WithMany(x => x.Appointments)
            .HasForeignKey(x => x.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Patient)
            .WithMany(x => x.Appointments)
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AvailabilitySlot)
            .WithMany(x => x.Appointments)
            .HasForeignKey(x => x.AvailabilitySlotId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
