using HSCSAPI.Models.Standards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HSCSAPI.Configurations.Standards;

public class RadiologyExamCatalogConfiguration : IEntityTypeConfiguration<RadiologyExamCatalog>
{
    public void Configure(EntityTypeBuilder<RadiologyExamCatalog> builder)
    {
        builder.ToTable("RadiologyExamCatalogs", table => table.ExcludeFromMigrations());

        builder.HasKey(x => x.Id);

        builder.Property(x => x.StandardSystem)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.Rpid)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.LetterCode);
        builder.Property(x => x.ShortName);
        builder.Property(x => x.LongName);
        builder.Property(x => x.Modality);
        builder.Property(x => x.PlaybookType);
        builder.Property(x => x.BodyRegion);
        builder.Property(x => x.BodyRegion2);
        builder.Property(x => x.ModalityModifier);
        builder.Property(x => x.ProcedureModifier);
        builder.Property(x => x.AnatomicFocus);
        builder.Property(x => x.Laterality);
        builder.Property(x => x.ReasonForExam);
        builder.Property(x => x.Technique);
        builder.Property(x => x.Pharmaceutical);
        builder.Property(x => x.View);
        builder.Property(x => x.Rids);

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt);

        builder.HasIndex(x => x.Rpid);
        builder.HasIndex(x => x.Modality);
    }
}
