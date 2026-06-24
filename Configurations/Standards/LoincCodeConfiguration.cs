using HSCSAPI.Models.Standards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HSCSAPI.Configurations.Standards;

public class LoincCodeConfiguration : IEntityTypeConfiguration<LoincCode>
{
    public void Configure(EntityTypeBuilder<LoincCode> builder)
    {
        builder.ToTable("LoincCodes", table => table.ExcludeFromMigrations());

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code)
            .HasColumnName("LoincNum")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.Component)
            .HasColumnName("Component");

        builder.Property(x => x.Property)
            .HasColumnName("Property");

        builder.Property(x => x.TimeAspect)
            .HasColumnName("TimeAspect");

        builder.Property(x => x.System)
            .HasColumnName("System");

        builder.Property(x => x.ScaleType)
            .HasColumnName("ScaleType");

        builder.Property(x => x.MethodType)
            .HasColumnName("MethodType");

        builder.Property(x => x.Class)
            .HasColumnName("Class");

        builder.Property(x => x.ClassType)
            .HasColumnName("ClassType");

        builder.Property(x => x.LongCommonName)
            .HasColumnName("LongCommonName");

        builder.Property(x => x.ShortName)
            .HasColumnName("ShortName");

        builder.Property(x => x.Status)
            .HasColumnName("Status")
            .HasMaxLength(64);

        builder.Property(x => x.VersionFirstReleased)
            .HasColumnName("VersionFirstReleased")
            .HasMaxLength(64);

        builder.Property(x => x.VersionLastChanged)
            .HasColumnName("VersionLastChanged")
            .HasMaxLength(64);

        builder.Property(x => x.IsActive)
            .HasColumnName("IsActive")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("CreatedAt")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("UpdatedAt");

        builder.HasIndex(x => x.Code);
    }
}
