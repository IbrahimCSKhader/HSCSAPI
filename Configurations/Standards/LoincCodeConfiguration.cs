using HSCSAPI.Models.Standards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HSCSAPI.Configurations.Standards;

public class LoincCodeConfiguration : IEntityTypeConfiguration<LoincCode>
{
    public void Configure(EntityTypeBuilder<LoincCode> builder)
    {
        builder.ToTable("LOINCCODE", table => table.ExcludeFromMigrations());

        builder.HasKey(x => x.Code);

        builder.Property(x => x.Code)
            .HasColumnName("LOINC_NUM")
            .HasMaxLength(20);

        builder.Property(x => x.Component)
            .HasColumnName("COMPONENT")
            .HasMaxLength(500);

        builder.Property(x => x.Property)
            .HasColumnName("PROPERTY")
            .HasMaxLength(100);

        builder.Property(x => x.TimeAspect)
            .HasColumnName("TIME_ASPCT")
            .HasMaxLength(100);

        builder.Property(x => x.System)
            .HasColumnName("SYSTEM")
            .HasMaxLength(200);

        builder.Property(x => x.ScaleType)
            .HasColumnName("SCALE_TYP")
            .HasMaxLength(100);

        builder.Property(x => x.MethodType)
            .HasColumnName("METHOD_TYP")
            .HasMaxLength(200);

        builder.Property(x => x.Class)
            .HasColumnName("CLASS")
            .HasMaxLength(100);

        builder.Property(x => x.ClassType)
            .HasColumnName("CLASSTYPE")
            .HasMaxLength(20);

        builder.Property(x => x.LongCommonName)
            .HasColumnName("LONG_COMMON_NAME")
            .HasMaxLength(1000);

        builder.Property(x => x.ShortName)
            .HasColumnName("SHORTNAME")
            .HasMaxLength(255);

        builder.Property(x => x.Status)
            .HasColumnName("STATUS")
            .HasMaxLength(50);

        builder.Property(x => x.VersionFirstReleased)
            .HasColumnName("VersionFirstReleased")
            .HasMaxLength(20);

        builder.Property(x => x.VersionLastChanged)
            .HasColumnName("VersionLastChanged")
            .HasMaxLength(20);
    }
}
