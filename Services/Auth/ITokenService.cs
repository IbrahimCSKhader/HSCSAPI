namespace HSCSAPI.Services.Auth;

public interface ITokenService
{
    string GenerateToken(Guid userId, string email, List<string> roles);
}
