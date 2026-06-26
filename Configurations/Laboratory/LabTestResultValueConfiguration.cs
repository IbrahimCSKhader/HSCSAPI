using HSCSAPI.Models.Laboratory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HSCSAPI.Configurations.Laboratory;

public class LabTestResultValueConfiguration : IEntityTypeConfiguration<LabTestResultValue>
{
    public void Configure(EntityTypeBuilder<LabTestResultValue> builder)
    {
        builder.ToTable("LabTestResultValues");
        builder.HasKey(x => x.LabTestResultValueId);

        builder.Property(x => x.FieldCode).IsRequired().HasMaxLength(50);
        builder.Property(x => x.FieldLabel).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ValueType).HasConversion<string>().IsRequired().HasMaxLength(20);
        builder.Property(x => x.NumericValue).HasPrecision(18, 6);
        builder.Property(x => x.TextValue).HasMaxLength(500);
        builder.Property(x => x.Unit).HasMaxLength(50);
        builder.Property(x => x.ReferenceRange).HasMaxLength(200);
        builder.Property(x => x.Flag).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(x => new { x.LabTestResultId, x.LabTestFieldDefinitionId }).IsUnique();

        builder.HasOne(x => x.LabTestResult)
            .WithMany(x => x.Values)
            .HasForeignKey(x => x.LabTestResultId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.FieldDefinition)
            .WithMany(x => x.ResultValues)
            .HasForeignKey(x => x.LabTestFieldDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
