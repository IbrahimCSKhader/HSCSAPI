using HSCSAPI.Models.PatientProfile;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HSCSAPI.Configurations.PatientProfile;

public class ExternalPatientProfileShareConfiguration : IEntityTypeConfiguration<ExternalPatientProfileShare>
{
    public void Configure(EntityTypeBuilder<ExternalPatientProfileShare> builder)
    {
        builder.ToTable("ExternalPatientProfileShares");

        builder.HasKey(x => x.ExternalPatientProfileShareId);

        builder.Property(x => x.DoctorEmail)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.DoctorName)
            .HasMaxLength(200);

        builder.Property(x => x.ShareToken)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(x => x.ShareTokenHash)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.VerificationCodeHash)
            .HasMaxLength(128);

        builder.Property(x => x.AccessSessionTokenHash)
            .HasMaxLength(128);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.ExpiresAt)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasIndex(x => x.ShareTokenHash)
            .IsUnique();

        builder.HasIndex(x => new { x.PatientId, x.DoctorEmail, x.IsActive });

        builder.HasOne(x => x.Patient)
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
