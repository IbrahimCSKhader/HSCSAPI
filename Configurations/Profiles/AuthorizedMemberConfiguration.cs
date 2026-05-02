using HSCSAPI.Models.Profiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HSCSAPI.Configurations.Profiles;

public class AuthorizedMemberConfiguration : IEntityTypeConfiguration<AuthorizedMember>
{
    public void Configure(EntityTypeBuilder<AuthorizedMember> builder)
    {
        builder.ToTable("AuthorizedMembers");

        builder.HasKey(x => x.AuthorizedMemberId);
        builder.Property(x => x.AuthorizedMemberId).ValueGeneratedOnAdd();

        builder.HasOne(x => x.User)
            .WithOne(x => x.AuthorizedMemberProfile)
            .HasForeignKey<AuthorizedMember>(x => x.AuthorizedMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Patients)
            .WithOne(x => x.AuthorizedMember)
            .HasForeignKey(x => x.AuthorizedMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.ReceivedInvites)
            .WithOne(x => x.AuthorizedMember)
            .HasForeignKey(x => x.AuthorizedMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Reminders)
            .WithOne(x => x.AuthorizedMember)
            .HasForeignKey(x => x.AuthorizedMemberId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
