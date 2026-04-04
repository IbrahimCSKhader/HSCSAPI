using HSCSAPI.Models.Laboratory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HSCSAPI.Configurations.Laboratory;

public class LabTestRequestConfiguration : IEntityTypeConfiguration<LabTestRequest>
{
    public void Configure(EntityTypeBuilder<LabTestRequest> builder)
    {
        builder.ToTable("LabTestRequests");

        builder.HasKey(x => x.LabTestRequestId);

        builder.Property(x => x.TestName)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasOne(x => x.LaboratoryTechnologist)
            .WithMany(x => x.LabTestRequests)
            .HasForeignKey(x => x.LaboratoryTechnologistId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.ResultMedicalFile)
            .WithMany(x => x.LabTestRequestsAsResult)
            .HasForeignKey(x => x.ResultMedicalFileId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
