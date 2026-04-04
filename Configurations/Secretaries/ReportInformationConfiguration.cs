using HSCSAPI.Models.Secretaries;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HSCSAPI.Configurations.Secretaries;

public class ReportInformationConfiguration : IEntityTypeConfiguration<ReportInformation>
{
    public void Configure(EntityTypeBuilder<ReportInformation> builder)
    {
        builder.ToTable("ReportInformations");

        builder.HasKey(x => x.ReportInformationId);

        builder.Property(x => x.FileFormat)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.FilePath)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.FileSizeInBytes)
            .IsRequired();

        builder.HasOne(x => x.Report)
            .WithMany(x => x.ReportInformations)
            .HasForeignKey(x => x.ReportId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
