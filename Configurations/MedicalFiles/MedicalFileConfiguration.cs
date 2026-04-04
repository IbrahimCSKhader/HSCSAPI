using HSCSAPI.Models.MedicalFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HSCSAPI.Configurations.MedicalFiles;

public class MedicalFileConfiguration : IEntityTypeConfiguration<MedicalFile>
{
    public void Configure(EntityTypeBuilder<MedicalFile> builder)
    {
        builder.ToTable("MedicalFiles");

        builder.HasKey(x => x.MedicalFileId);

        builder.Property(x => x.FileType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.FilePath)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.EncryptedChecksum)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(x => x.FileSizeInBytes)
            .IsRequired();

        builder.Property(x => x.SeverityLevel)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.UploadedAt)
            .IsRequired();

        builder.HasOne(x => x.Appointment)
            .WithMany(x => x.MedicalFiles)
            .HasForeignKey(x => x.AppointmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.UploadedByDoctor)
            .WithMany(x => x.UploadedMedicalFiles)
            .HasForeignKey(x => x.UploadedByDoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.FileDownloadRequests)
            .WithOne(x => x.MedicalFile)
            .HasForeignKey(x => x.MedicalFileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.LabTestRequestsAsResult)
            .WithOne(x => x.ResultMedicalFile)
            .HasForeignKey(x => x.ResultMedicalFileId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.ImagingTestRequestsAsResult)
            .WithOne(x => x.ResultMedicalFile)
            .HasForeignKey(x => x.ResultMedicalFileId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
