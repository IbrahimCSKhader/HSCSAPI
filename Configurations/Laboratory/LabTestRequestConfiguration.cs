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

        builder.Property(x => x.LoincCode)
            .HasMaxLength(20);

        builder.Property(x => x.Priority)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("Routine");

        builder.Property(x => x.ClinicalNotes)
            .HasMaxLength(1000);

        builder.Property(x => x.RequestedAt)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(x => x.LoincCode);
        builder.HasIndex(x => x.PatientId);
        builder.HasIndex(x => x.RequestedByDoctorId);
        builder.HasIndex(x => x.TestingClinicId);
        builder.HasIndex(x => x.RequestedAt);

        builder.HasOne(x => x.Patient)
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RequestedByDoctor)
            .WithMany()
            .HasForeignKey(x => x.RequestedByDoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.TestingClinic)
            .WithMany()
            .HasForeignKey(x => x.TestingClinicId)
            .OnDelete(DeleteBehavior.SetNull);

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
