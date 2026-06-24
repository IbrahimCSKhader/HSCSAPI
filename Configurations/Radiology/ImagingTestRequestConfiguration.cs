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

        builder.Property(x => x.ImagingCode)
            .HasMaxLength(50);

        builder.Property(x => x.BodyRegion)
            .HasMaxLength(200);

        builder.Property(x => x.Priority)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("Routine");

        builder.Property(x => x.ClinicalNotes)
            .HasMaxLength(1000);

        builder.Property(x => x.RequestedAt)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(x => x.ImagingCode);
        builder.HasIndex(x => x.PatientId);
        builder.HasIndex(x => x.RequestedByDoctorId);
        builder.HasIndex(x => x.RadiologyClinicId);
        builder.HasIndex(x => x.RequestedAt);

        builder.HasOne(x => x.Patient)
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RequestedByDoctor)
            .WithMany()
            .HasForeignKey(x => x.RequestedByDoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RadiologyClinic)
            .WithMany()
            .HasForeignKey(x => x.RadiologyClinicId)
            .OnDelete(DeleteBehavior.SetNull);

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
