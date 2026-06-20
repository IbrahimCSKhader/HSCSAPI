using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSCSAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddChatMessaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Chats",
                columns: table => new
                {
                    ChatId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserOneId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserTwoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    LastMessageAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chats", x => x.ChatId);
                    table.CheckConstraint("CK_Chats_DifferentUsers", "[UserOneId] <> [UserTwoId]");
                    table.ForeignKey(
                        name: "FK_Chats_Users_UserOneId",
                        column: x => x.UserOneId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Chats_Users_UserTwoId",
                        column: x => x.UserTwoId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChatMessages",
                columns: table => new
                {
                    ChatMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChatId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FileSizeInBytes = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessages", x => x.ChatMessageId);
                    table.CheckConstraint("CK_ChatMessages_Content", "([MessageType] = 'Text' AND [Text] IS NOT NULL AND [FilePath] IS NULL AND [ContentType] IS NULL AND [FileSizeInBytes] IS NULL) OR ([MessageType] IN ('Image', 'Audio') AND [Text] IS NULL AND [FilePath] IS NOT NULL AND [ContentType] IS NOT NULL AND [FileSizeInBytes] IS NOT NULL AND [FileSizeInBytes] > 0)");
                    table.ForeignKey(
                        name: "FK_ChatMessages_Chats_ChatId",
                        column: x => x.ChatId,
                        principalTable: "Chats",
                        principalColumn: "ChatId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChatMessages_Users_SenderId",
                        column: x => x.SenderId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ChatId_CreatedAt",
                table: "ChatMessages",
                columns: new[] { "ChatId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ChatId_ReadAt",
                table: "ChatMessages",
                columns: new[] { "ChatId", "ReadAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_SenderId",
                table: "ChatMessages",
                column: "SenderId");

            migrationBuilder.CreateIndex(
                name: "IX_Chats_LastMessageAt",
                table: "Chats",
                column: "LastMessageAt");

            migrationBuilder.CreateIndex(
                name: "IX_Chats_UserOneId_UserTwoId",
                table: "Chats",
                columns: new[] { "UserOneId", "UserTwoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Chats_UserTwoId",
                table: "Chats",
                column: "UserTwoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatMessages");

            migrationBuilder.DropTable(
                name: "Chats");
        }
    }
}
