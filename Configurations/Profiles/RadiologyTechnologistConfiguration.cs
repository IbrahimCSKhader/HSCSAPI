using HSCSAPI.Models.Profiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HSCSAPI.Configurations.Profiles;

public class RadiologyTechnologistConfiguration : IEntityTypeConfiguration<RadiologyTechnologist>
{
    public void Configure(EntityTypeBuilder<RadiologyTechnologist> builder)
    {
        builder.ToTable("RadiologyTechnologists");

        builder.HasKey(x => x.RadiologyTechnologistId);
        builder.Property(x => x.RadiologyTechnologistId).ValueGeneratedOnAdd();

        builder.Property(x => x.ProfessionalLicenseNumber)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(x => x.ProfessionalLicenseNumber)
            .IsUnique();

        builder.HasOne(x => x.User)
            .WithOne(x => x.RadiologyTechnologistProfile)
            .HasForeignKey<RadiologyTechnologist>(x => x.RadiologyTechnologistId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.ImagingTestRequests)
            .WithOne(x => x.RadiologyTechnologist)
            .HasForeignKey(x => x.RadiologyTechnologistId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
