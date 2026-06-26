using HSCSAPI.Models.Laboratory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HSCSAPI.Configurations.Laboratory;

public class LabTestResultConfiguration : IEntityTypeConfiguration<LabTestResult>
{
    public void Configure(EntityTypeBuilder<LabTestResult> builder)
    {
        builder.ToTable("LabTestResults");
        builder.HasKey(x => x.LabTestResultId);

        builder.Property(x => x.AccessionNumber).IsRequired().HasMaxLength(80);
        builder.Property(x => x.SpecimenCondition).HasConversion<string>().IsRequired().HasMaxLength(30);
        builder.Property(x => x.SpecimenNotes).HasMaxLength(1000);
        builder.Property(x => x.Comments).HasMaxLength(2000);
        builder.Property(x => x.PdfFilePath).HasMaxLength(500);
        builder.Property(x => x.PdfChecksum).HasMaxLength(128);
        builder.Property(x => x.CollectedAt).IsRequired();
        builder.Property(x => x.ReceivedAt).IsRequired();
        builder.Property(x => x.CompletedAt).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(x => x.LabTestRequestId).IsUnique();
        builder.HasIndex(x => x.AccessionNumber).IsUnique();
        builder.HasIndex(x => x.LaboratoryTechnologistId);
        builder.HasIndex(x => x.CompletedAt);

        builder.HasOne(x => x.LabTestRequest)
            .WithOne(x => x.StructuredResult)
            .HasForeignKey<LabTestResult>(x => x.LabTestRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Template)
            .WithMany(x => x.Results)
            .HasForeignKey(x => x.LabTestTemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.LaboratoryTechnologist)
            .WithMany(x => x.LabTestResults)
            .HasForeignKey(x => x.LaboratoryTechnologistId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
