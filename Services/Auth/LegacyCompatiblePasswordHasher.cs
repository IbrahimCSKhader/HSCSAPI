using HSCSAPI.Models.Identity;
using Microsoft.AspNetCore.Identity;

namespace HSCSAPI.Services.Auth;

public class LegacyCompatiblePasswordHasher : IPasswordHasher<User>
{
    private readonly PasswordHasher<User> _identityPasswordHasher = new();

    public string HashPassword(User user, string password)
    {
        return _identityPasswordHasher.HashPassword(user, password);
    }

    public PasswordVerificationResult VerifyHashedPassword(User user, string hashedPassword, string providedPassword)
    {
        if (string.IsNullOrWhiteSpace(hashedPassword))
        {
            return PasswordVerificationResult.Failed;
        }

        if (IsLegacyBcryptHash(hashedPassword))
        {
            return BCrypt.Net.BCrypt.Verify(providedPassword, hashedPassword)
                ? PasswordVerificationResult.SuccessRehashNeeded
                : PasswordVerificationResult.Failed;
        }

        return _identityPasswordHasher.VerifyHashedPassword(user, hashedPassword, providedPassword);
    }

    private static bool IsLegacyBcryptHash(string hashedPassword)
    {
        return hashedPassword.StartsWith("$2a$", StringComparison.Ordinal)
            || hashedPassword.StartsWith("$2b$", StringComparison.Ordinal)
            || hashedPassword.StartsWith("$2y$", StringComparison.Ordinal);
    }
}
