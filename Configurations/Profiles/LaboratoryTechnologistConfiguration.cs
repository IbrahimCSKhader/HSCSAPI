using HSCSAPI.Models.Profiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HSCSAPI.Configurations.Profiles;

public class LaboratoryTechnologistConfiguration : IEntityTypeConfiguration<LaboratoryTechnologist>
{
    public void Configure(EntityTypeBuilder<LaboratoryTechnologist> builder)
    {
        builder.ToTable("LaboratoryTechnologists");

        builder.HasKey(x => x.LaboratoryTechnologistId);
        builder.Property(x => x.LaboratoryTechnologistId).ValueGeneratedNever();

        builder.Property(x => x.ProfessionalLicenseNumber)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(x => x.ProfessionalLicenseNumber)
            .IsUnique();

        builder.HasOne(x => x.User)
            .WithOne(x => x.LaboratoryTechnologistProfile)
            .HasForeignKey<LaboratoryTechnologist>(x => x.LaboratoryTechnologistId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.LabTestRequests)
            .WithOne(x => x.LaboratoryTechnologist)
            .HasForeignKey(x => x.LaboratoryTechnologistId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
