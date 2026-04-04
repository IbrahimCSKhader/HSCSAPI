using HSCSAPI.Models.Radiology;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HSCSAPI.Configurations.Radiology;

public class ImagingTestRequestConfiguration : IEntityTypeConfiguration<ImagingTestRequest>
{
    public void Configure(EntityTypeBuilder<ImagingTestRequest> builder)
    {
        builder.ToTable("ImagingTestRequests");

        builder.HasKey(x => x.ImagingTestRequestId);

        builder.Property(x => x.TestName)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasOne(x => x.RadiologyTechnologist)
            .WithMany(x => x.ImagingTestRequests)
            .HasForeignKey(x => x.RadiologyTechnologistId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.ResultMedicalFile)
            .WithMany(x => x.ImagingTestRequestsAsResult)
            .HasForeignKey(x => x.ResultMedicalFileId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
