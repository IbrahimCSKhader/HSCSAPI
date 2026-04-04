using HSCSAPI.Models.Secretaries;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HSCSAPI.Configurations.Secretaries;

public class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.ToTable("Reports");

        builder.HasKey(x => x.ReportId);

        builder.Property(x => x.GeneratedAt)
            .IsRequired();

        builder.HasOne(x => x.Secretary)
            .WithMany(x => x.Reports)
            .HasForeignKey(x => x.SecretaryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.ReportInformations)
            .WithOne(x => x.Report)
            .HasForeignKey(x => x.ReportId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
