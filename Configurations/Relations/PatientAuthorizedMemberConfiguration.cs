using HSCSAPI.Models.Relations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HSCSAPI.Configurations.Relations;

public class PatientAuthorizedMemberConfiguration : IEntityTypeConfiguration<PatientAuthorizedMember>
{
    public void Configure(EntityTypeBuilder<PatientAuthorizedMember> builder)
    {
        builder.ToTable("PatientAuthorizedMembers");

        builder.HasKey(x => new { x.PatientId, x.AuthorizedMemberId });

        builder.Property(x => x.RelationshipType)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.AuthorizedAt)
            .IsRequired();

        builder.HasOne(x => x.Patient)
            .WithMany(x => x.AuthorizedMembers)
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.AuthorizedMember)
            .WithMany(x => x.Patients)
            .HasForeignKey(x => x.AuthorizedMemberId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
