using HSCSAPI.Models.MedicalFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HSCSAPI.Configurations.MedicalFiles;

public class FileDownloadRequestConfiguration : IEntityTypeConfiguration<FileDownloadRequest>
{
    public void Configure(EntityTypeBuilder<FileDownloadRequest> builder)
    {
        builder.ToTable("FileDownloadRequests");

        builder.HasKey(x => x.FileDownloadRequestId);

        builder.Property(x => x.Reason)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.PurposeDescription)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.SubmittedAt)
            .IsRequired();

        builder.Property(x => x.RejectionReason)
            .HasMaxLength(1000);

        builder.HasOne(x => x.Patient)
            .WithMany(x => x.FileDownloadRequests)
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.MedicalFile)
            .WithMany(x => x.FileDownloadRequests)
            .HasForeignKey(x => x.MedicalFileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ReviewedBySecretary)
            .WithMany(x => x.ReviewedFileDownloadRequests)
            .HasForeignKey(x => x.ReviewedBySecretaryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
