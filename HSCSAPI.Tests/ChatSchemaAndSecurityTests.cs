using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using HSCSAPI.Data;
using HSCSAPI.Hub;
using HSCSAPI.Models.Chats;
using HSCSAPI.Models.Enums;
using HSCSAPI.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace HSCSAPI.Tests;

public class ChatSchemaAndSecurityTests
{
    [Fact]
    public void ChatSchema_HasUniqueParticipantPairAndDifferentUsersConstraint()
    {
        using var context = CreateDbContext();
        var entity = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Chat))!;

        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique
            && index.Properties.Select(x => x.Name).SequenceEqual([nameof(Chat.UserOneId), nameof(Chat.UserTwoId)]));
        Assert.Contains(entity.GetCheckConstraints(), constraint =>
            constraint.Name == "CK_Chats_DifferentUsers");
    }

    [Fact]
    public void MessageSchema_HasContentConstraintLengthsAndStringEnumStorage()
    {
        using var context = CreateDbContext();
        var entity = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(ChatMessage))!;

        Assert.Contains(entity.GetCheckConstraints(), constraint =>
            constraint.Name == "CK_ChatMessages_Content");
        Assert.Equal(4000, entity.FindProperty(nameof(ChatMessage.Text))!.GetMaxLength());
        Assert.Equal(500, entity.FindProperty(nameof(ChatMessage.FilePath))!.GetMaxLength());
        Assert.Equal(255, entity.FindProperty(nameof(ChatMessage.OriginalFileName))!.GetMaxLength());
        Assert.Equal(typeof(string), entity.FindProperty(nameof(ChatMessage.MessageType))!.GetProviderClrType());
    }

    [Fact]
    public void ChatSchema_UsesCascadeForMessagesAndRestrictForUsers()
    {
        using var context = CreateDbContext();
        var chatMessageEntity = context.Model.FindEntityType(typeof(ChatMessage))!;
        var chatEntity = context.Model.FindEntityType(typeof(Chat))!;

        Assert.Contains(chatMessageEntity.GetForeignKeys(), key =>
            key.PrincipalEntityType.ClrType == typeof(Chat) && key.DeleteBehavior == DeleteBehavior.Cascade);
        Assert.All(
            chatEntity.GetForeignKeys().Where(key => key.PrincipalEntityType.ClrType.Name == "User"),
            key => Assert.Equal(DeleteBehavior.Restrict, key.DeleteBehavior));
    }

    [Theory]
    [InlineData(typeof(ChatHub))]
    [InlineData(typeof(NotificationHub))]
    public void SignalRHubs_RequireAuthorization(Type hubType)
    {
        Assert.NotNull(hubType.GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public void JwtToken_ContainsNameIdentifierUsedBySignalR()
    {
        const string secret = "test-secret-key-that-is-long-enough-for-hmac-sha256-validation";
        var settings = new Dictionary<string, string?>
        {
            ["JwtSettings:SecretKey"] = secret,
            ["JwtSettings:Issuer"] = "HSCSAPI.Tests",
            ["JwtSettings:Audience"] = "HSCSAPI.Tests.Client",
            ["JwtSettings:TokenExpirationHours"] = "1"
        };
        var service = new TokenService(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        var userId = Guid.NewGuid();
        var token = service.GenerateToken(userId, "user@test.local", "Patient");
        var handler = new JwtSecurityTokenHandler();

        var principal = handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "HSCSAPI.Tests",
            ValidateAudience = true,
            ValidAudience = "HSCSAPI.Tests.Client",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(secret))
        }, out _);

        Assert.Equal(userId.ToString(), principal.FindFirstValue(ClaimTypes.NameIdentifier));
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new AppDbContext(options);
    }
}
