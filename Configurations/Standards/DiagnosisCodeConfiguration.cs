using HSCSAPI.Models.Standards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HSCSAPI.Configurations.Standards;

public class DiagnosisCodeConfiguration : IEntityTypeConfiguration<DiagnosisCode>
{
    public void Configure(EntityTypeBuilder<DiagnosisCode> builder)
    {
        builder.ToTable("DiagnosisCodes", table => table.ExcludeFromMigrations());

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CodeSystem)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Code)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.DisplayCode)
            .HasMaxLength(64);

        builder.Property(x => x.Name)
            .IsRequired();

        builder.Property(x => x.Description);

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt);

        builder.HasIndex(x => new { x.CodeSystem, x.Code });
        builder.HasIndex(x => x.DisplayCode);
    }
}
