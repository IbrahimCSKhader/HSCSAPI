using HSCSAPI.Models.Laboratory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HSCSAPI.Configurations.Laboratory;

public class LabTestFieldDefinitionConfiguration : IEntityTypeConfiguration<LabTestFieldDefinition>
{
    public void Configure(EntityTypeBuilder<LabTestFieldDefinition> builder)
    {
        builder.ToTable("LabTestFieldDefinitions");
        builder.HasKey(x => x.LabTestFieldDefinitionId);

        builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Label).IsRequired().HasMaxLength(200);
        builder.Property(x => x.LoincCode).HasMaxLength(20);
        builder.Property(x => x.ValueType).HasConversion<string>().IsRequired().HasMaxLength(20);
        builder.Property(x => x.Unit).HasMaxLength(50);
        builder.Property(x => x.ReferenceRange).HasMaxLength(200);
        builder.Property(x => x.AllowedValuesJson).HasMaxLength(2000);

        builder.HasIndex(x => new { x.LabTestTemplateId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.LabTestTemplateId, x.DisplayOrder }).IsUnique();

        builder.HasOne(x => x.Template)
            .WithMany(x => x.Fields)
            .HasForeignKey(x => x.LabTestTemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
