using HSCSAPI.Models.Relations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HSCSAPI.Configurations.Relations;

public class InviteConfiguration : IEntityTypeConfiguration<Invite>
{
    public void Configure(EntityTypeBuilder<Invite> builder)
    {
        builder.ToTable("Invites");

        builder.HasKey(x => x.InviteId);

        builder.Property(x => x.RelationshipType)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.SentAt)
            .IsRequired();

        builder.HasOne(x => x.Patient)
            .WithMany(x => x.SentInvites)
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AuthorizedMember)
            .WithMany(x => x.ReceivedInvites)
            .HasForeignKey(x => x.AuthorizedMemberId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
