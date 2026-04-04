using HSCSAPI.Models.Profiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HSCSAPI.Configurations.Profiles;

public class DoctorConfiguration : IEntityTypeConfiguration<Doctor>
{
    public void Configure(EntityTypeBuilder<Doctor> builder)
    {
        builder.ToTable("Doctors");

        builder.HasKey(x => x.DoctorId);
        builder.Property(x => x.DoctorId).ValueGeneratedNever();

        builder.Property(x => x.ProfessionalLicenseNumber)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(x => x.ProfessionalLicenseNumber)
            .IsUnique();

        builder.HasOne(x => x.User)
            .WithOne(x => x.DoctorProfile)
            .HasForeignKey<Doctor>(x => x.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.AvailabilitySlots)
            .WithOne(x => x.Doctor)
            .HasForeignKey(x => x.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Appointments)
            .WithOne(x => x.Doctor)
            .HasForeignKey(x => x.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Reminders)
            .WithOne(x => x.Doctor)
            .HasForeignKey(x => x.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.UploadedMedicalFiles)
            .WithOne(x => x.UploadedByDoctor)
            .HasForeignKey(x => x.UploadedByDoctorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
