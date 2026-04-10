using System.Security.Cryptography;
using System.Text;
using HSCSAPI.Data;
using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Identity;
using HSCSAPI.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HSCSAPI.Services.Identity;

public class IdentitySeedService
{
    private readonly AppDbContext _dbContext;
    private readonly SuperAdminSeedSettings _seedSettings;

    public IdentitySeedService(AppDbContext dbContext, IOptions<SuperAdminSeedSettings> seedOptions)
    {
        _dbContext = dbContext;
        _seedSettings = seedOptions.Value;
    }

    public async Task SeedSuperAdminAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_seedSettings.Email) || string.IsNullOrWhiteSpace(_seedSettings.Password))
        {
            return;
        }

        var superAdminRole = await _dbContext.Roles
            .FirstOrDefaultAsync(r => r.Name == nameof(UserSystemRole.SuperAdmin), cancellationToken);

        if (superAdminRole == null)
        {
            superAdminRole = new Role
            {
                RoleId = (int)UserSystemRole.SuperAdmin,
                Name = nameof(UserSystemRole.SuperAdmin)
            };
            _dbContext.Roles.Add(superAdminRole);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var email = _seedSettings.Email.Trim().ToLowerInvariant();
        var superAdminUser = await _dbContext.Users
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (superAdminUser == null)
        {
            superAdminUser = new User
            {
                Name = _seedSettings.Name,
                Email = email,
                PasswordHash = HashPassword(_seedSettings.Password)
            };
            _dbContext.Users.Add(superAdminUser);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var hasRole = await _dbContext.UserRoles
            .AnyAsync(ur => ur.UserId == superAdminUser.UserId && ur.RoleId == superAdminRole.RoleId, cancellationToken);

        if (!hasRole)
        {
            _dbContext.UserRoles.Add(new UserRole
            {
                UserId = superAdminUser.UserId,
                RoleId = superAdminRole.RoleId
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }
}
