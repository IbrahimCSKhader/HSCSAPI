using HSCSAPI.Models.Profiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HSCSAPI.Configurations.Profiles;

public class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.ToTable("Patients");

        builder.HasKey(x => x.PatientId);
            builder.Property(x => x.PatientId).ValueGeneratedOnAdd();

        builder.Property(x => x.UserID)
            .IsRequired()
            .HasMaxLength(20);

        builder.HasIndex(x => x.UserID)
            .IsUnique();

        builder.Property(x => x.Gender)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.BloodType)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasOne(x => x.User)
            .WithOne(x => x.PatientProfile)
            .HasForeignKey<Patient>(x => x.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Appointments)
            .WithOne(x => x.Patient)
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Reminders)
            .WithOne(x => x.Patient)
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.AuthorizedMembers)
            .WithOne(x => x.Patient)
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.SentInvites)
            .WithOne(x => x.Patient)
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.FileDownloadRequests)
            .WithOne(x => x.Patient)
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
