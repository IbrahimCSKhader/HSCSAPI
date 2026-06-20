using HSCSAPI.Models.Chats;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HSCSAPI.Configurations.Chats;

public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("ChatMessages", table =>
            table.HasCheckConstraint(
                "CK_ChatMessages_Content",
                "([MessageType] = 'Text' AND [Text] IS NOT NULL AND [FilePath] IS NULL AND [ContentType] IS NULL AND [FileSizeInBytes] IS NULL) OR " +
                "([MessageType] IN ('Image', 'Audio') AND [Text] IS NULL AND [FilePath] IS NOT NULL AND [ContentType] IS NOT NULL AND [FileSizeInBytes] IS NOT NULL AND [FileSizeInBytes] > 0)"));

        builder.HasKey(x => x.ChatMessageId);

        builder.Property(x => x.MessageType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Text)
            .HasMaxLength(4000);

        builder.Property(x => x.FilePath)
            .HasMaxLength(500);

        builder.Property(x => x.OriginalFileName)
            .HasMaxLength(255);

        builder.Property(x => x.ContentType)
            .HasMaxLength(100);

        builder.Property(x => x.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(x => new { x.ChatId, x.CreatedAt });
        builder.HasIndex(x => new { x.ChatId, x.ReadAt });

        builder.HasOne(x => x.Chat)
            .WithMany(x => x.Messages)
            .HasForeignKey(x => x.ChatId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Sender)
            .WithMany(x => x.SentChatMessages)
            .HasForeignKey(x => x.SenderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
