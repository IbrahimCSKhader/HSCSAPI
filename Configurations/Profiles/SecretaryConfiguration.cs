using HSCSAPI.Models.Profiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HSCSAPI.Configurations.Profiles;

public class SecretaryConfiguration : IEntityTypeConfiguration<Secretary>
{
    public void Configure(EntityTypeBuilder<Secretary> builder)
    {
        builder.ToTable("Secretaries");

        builder.HasKey(x => x.SecretaryId);
        builder.Property(x => x.SecretaryId).ValueGeneratedNever();

        builder.HasOne(x => x.User)
            .WithOne(x => x.SecretaryProfile)
            .HasForeignKey<Secretary>(x => x.SecretaryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Reports)
            .WithOne(x => x.Secretary)
            .HasForeignKey(x => x.SecretaryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.ReviewedFileDownloadRequests)
            .WithOne(x => x.ReviewedBySecretary)
            .HasForeignKey(x => x.ReviewedBySecretaryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
