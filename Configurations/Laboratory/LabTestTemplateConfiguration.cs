using HSCSAPI.Models.Laboratory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HSCSAPI.Configurations.Laboratory;

public class LabTestTemplateConfiguration : IEntityTypeConfiguration<LabTestTemplate>
{
    public void Configure(EntityTypeBuilder<LabTestTemplate> builder)
    {
        builder.ToTable("LabTestTemplates");
        builder.HasKey(x => x.LabTestTemplateId);

        builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.LoincCode).HasMaxLength(20);
        builder.Property(x => x.SpecimenType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.PreparationInstructions).HasMaxLength(1000);
        builder.Property(x => x.SourceUrl).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Version).IsRequired();
        builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);

        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => x.LoincCode);
    }
}
