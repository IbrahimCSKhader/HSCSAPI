using System.Security.Claims;
using HSCSAPI.Data;
using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Identity;
using HSCSAPI.Services.Auth;
using HSCSAPI.Services.Clinics;
using HSCSAPI.Services.Email;
using HSCSAPI.Services.Identity;
using HSCSAPI.Services.Secretaries;
using HSCSAPI.Services.Testing;
using HSCSAPI.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HSCSAPI.Tests.Infrastructure;

public sealed class TestApplicationContext : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _serviceProvider;

    private TestApplicationContext(SqliteConnection connection, ServiceProvider serviceProvider)
    {
        _connection = connection;
        _serviceProvider = serviceProvider;
    }

    public static async Task<TestApplicationContext> CreateAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:SecretKey"] = "TestSecretKeyThatIsLongEnoughForJwtSigning1234567890",
                ["JwtSettings:Issuer"] = "HSCSAPI.Tests",
                ["JwtSettings:Audience"] = "HSCSAPI.Tests.Client",
                ["JwtSettings:TokenExpirationHours"] = "24"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(builder => builder.AddDebug().SetMinimumLevel(LogLevel.Debug));

        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
        services
            .AddIdentityCore<User>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<Role>()
            .AddEntityFrameworkStores<AppDbContext>();

        services.AddScoped<IPasswordHasher<User>, LegacyCompatiblePasswordHasher>();
        services.AddScoped<IEmailService, FakeEmailService>();
        services.AddScoped<UserIdGeneratorService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IClinicsService, ClinicsService>();
        services.AddScoped<ISecretariesService, SecretariesService>();
        services.AddScoped<IdentitySeedService>();
        services.AddScoped<OneTimeClinicTestSeedService>();

        services.Configure<SuperAdminSeedSettings>(options =>
        {
            options.Email = "superadmin@test.local";
            options.Password = "TestAdmin123";
            options.Name = "Test Super Admin";
        });

        services.Configure<EmailSettings>(_ => { });
        services.Configure<JwtSettings>(configuration.GetSection("JwtSettings"));

        var serviceProvider = services.BuildServiceProvider(validateScopes: true);

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.EnsureCreatedAsync();

            var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeedService>();
            await seeder.SeedAsync();
        }

        return new TestApplicationContext(connection, serviceProvider);
    }

    public async Task<T> GetRequiredServiceAsync<T>() where T : notnull
    {
        await Task.CompletedTask;
        return _serviceProvider.GetRequiredService<T>();
    }

    public IServiceScope CreateScope()
    {
        return _serviceProvider.CreateScope();
    }

    public static ClaimsPrincipal CreatePrincipal(Guid userId, UserSystemRole role)
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role.ToString())
            },
            authenticationType: "TestAuth");

        return new ClaimsPrincipal(identity);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private sealed class FakeEmailService : IEmailService
    {
        public Task SendEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
