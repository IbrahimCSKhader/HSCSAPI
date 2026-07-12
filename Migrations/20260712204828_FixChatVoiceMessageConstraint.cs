using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSCSAPI.Migrations
{
    /// <inheritdoc />
    public partial class FixChatVoiceMessageConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ChatMessages_Content",
                table: "ChatMessages");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChatMessages_Content",
                table: "ChatMessages",
                sql: "([MessageType] = 'Text' AND [Text] IS NOT NULL AND [FilePath] IS NULL AND [ContentType] IS NULL AND [FileSizeInBytes] IS NULL) OR ([MessageType] IN ('Image', 'Voice') AND [Text] IS NULL AND [FilePath] IS NOT NULL AND [ContentType] IS NOT NULL AND [FileSizeInBytes] IS NOT NULL AND [FileSizeInBytes] > 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ChatMessages_Content",
                table: "ChatMessages");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChatMessages_Content",
                table: "ChatMessages",
                sql: "([MessageType] = 'Text' AND [Text] IS NOT NULL AND [FilePath] IS NULL AND [ContentType] IS NULL AND [FileSizeInBytes] IS NULL) OR ([MessageType] IN ('Image', 'Audio') AND [Text] IS NULL AND [FilePath] IS NOT NULL AND [ContentType] IS NOT NULL AND [FileSizeInBytes] IS NOT NULL AND [FileSizeInBytes] > 0)");
        }
    }
}
