using HSCSAPI.Models.Chats;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HSCSAPI.Configurations.Chats;

public class ChatConfiguration : IEntityTypeConfiguration<Chat>
{
    public void Configure(EntityTypeBuilder<Chat> builder)
    {
        builder.ToTable("Chats", table =>
            table.HasCheckConstraint("CK_Chats_DifferentUsers", "[UserOneId] <> [UserTwoId]"));

        builder.HasKey(x => x.ChatId);

        builder.Property(x => x.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(x => new { x.UserOneId, x.UserTwoId })
            .IsUnique();

        builder.HasIndex(x => x.LastMessageAt);

        builder.HasOne(x => x.UserOne)
            .WithMany(x => x.ChatsAsUserOne)
            .HasForeignKey(x => x.UserOneId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.UserTwo)
            .WithMany(x => x.ChatsAsUserTwo)
            .HasForeignKey(x => x.UserTwoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
