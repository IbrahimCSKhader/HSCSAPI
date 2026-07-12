using System.Security.Claims;
using HSCSAPI.Data;
using HSCSAPI.DTOs.Common;
using HSCSAPI.DTOs.Secretary;
using HSCSAPI.Models.Clinics;
using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Identity;
using HSCSAPI.Models.Profiles;
using HSCSAPI.Services.Auth;
using HSCSAPI.Services.Secretaries;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HSCSAPI.Tests;

public class SecretarySelfServiceTests
{
    [Fact]
    public async Task GetMyProfile_ReturnsCurrentSecretaryProfile()
    {
        using var context = new SecretarySelfTestContext();
        var clinic = context.AddClinic("Central Clinic");
        var secretary = await context.AddSecretaryAsync(clinic.ClinicId);

        var response = await context.Service.GetMyProfileAsync(
            SecretarySelfTestContext.Principal(secretary.Id),
            CancellationToken.None);

        var profile = OkValue(response);
        Assert.Equal(secretary.Id, profile.SecretaryId);
        Assert.Equal("Mira Secretary", profile.Name);
        Assert.Equal("secretary@test.local", profile.Email);
        Assert.Equal(clinic.ClinicId, profile.ClinicId);
        Assert.Equal("Central Clinic", profile.ClinicName);
        Assert.True(profile.IsActive);
    }

    [Fact]
    public async Task UpdateMyProfile_UpdatesOnlyTheCurrentSecretaryAccount()
    {
        using var context = new SecretarySelfTestContext();
        var clinic = context.AddClinic("Central Clinic");
        var secretary = await context.AddSecretaryAsync(clinic.ClinicId, email: "old.secretary@test.local");
        var otherSecretary = await context.AddSecretaryAsync(clinic.ClinicId, email: "other.secretary@test.local");

        var response = await context.Service.UpdateMyProfileAsync(
            new UpdateSecretaryRequest
            {
                Name = "Updated Secretary",
                Email = "new.secretary@test.local",
                PhoneNumber = " 0599000000 ",
                Address = " Ramallah ",
                DateOfBirth = new DateOnly(1995, 4, 12)
            },
            SecretarySelfTestContext.Principal(secretary.Id),
            CancellationToken.None);

        var profile = OkValue(response);
        Assert.Equal("Updated Secretary", profile.Name);
        Assert.Equal("new.secretary@test.local", profile.Email);
        Assert.Equal("0599000000", profile.PhoneNumber);
        Assert.Equal("Ramallah", profile.Address);
        Assert.Equal(new DateOnly(1995, 4, 12), profile.DateOfBirth);

        var refreshed = await context.UserManager.FindByIdAsync(secretary.Id.ToString());
        Assert.Equal("new.secretary@test.local", refreshed!.Email);
        Assert.Equal("new.secretary@test.local", refreshed.UserName);
        Assert.Equal("other.secretary@test.local", (await context.UserManager.FindByIdAsync(otherSecretary.Id.ToString()))!.Email);
    }

    [Fact]
    public async Task ChangeMyPassword_UpdatesPasswordAndTimestamp()
    {
        using var context = new SecretarySelfTestContext();
        var clinic = context.AddClinic("Central Clinic");
        var secretary = await context.AddSecretaryAsync(clinic.ClinicId, password: "OldPass123");

        var response = await context.Service.ChangeMyPasswordAsync(
            new ChangePasswordRequest
            {
                CurrentPassword = "OldPass123",
                NewPassword = "NewPass123",
                ConfirmNewPassword = "NewPass123"
            },
            SecretarySelfTestContext.Principal(secretary.Id),
            CancellationToken.None);

        var result = OkValue(response);
        Assert.True(result.Success);
        Assert.NotNull(result.PasswordLastUpdatedIso);
        Assert.True(await context.UserManager.CheckPasswordAsync(secretary, "NewPass123"));
        Assert.False(await context.UserManager.CheckPasswordAsync(secretary, "OldPass123"));

        var refreshed = await context.UserManager.FindByIdAsync(secretary.Id.ToString());
        Assert.NotNull(refreshed!.PasswordLastUpdatedAt);
    }

    [Fact]
    public async Task ChangeMyPassword_RejectsWrongCurrentPassword()
    {
        using var context = new SecretarySelfTestContext();
        var clinic = context.AddClinic("Central Clinic");
        var secretary = await context.AddSecretaryAsync(clinic.ClinicId, password: "OldPass123");

        var response = await context.Service.ChangeMyPasswordAsync(
            new ChangePasswordRequest
            {
                CurrentPassword = "WrongPass123",
                NewPassword = "NewPass123",
                ConfirmNewPassword = "NewPass123"
            },
            SecretarySelfTestContext.Principal(secretary.Id),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(response.Result);
        var result = Assert.IsType<ChangePasswordResponse>(badRequest.Value);
        Assert.False(result.Success);
        Assert.True(await context.UserManager.CheckPasswordAsync(secretary, "OldPass123"));
    }

    [Fact]
    public async Task UpdateMyProfile_RejectsDuplicateEmail()
    {
        using var context = new SecretarySelfTestContext();
        var clinic = context.AddClinic("Central Clinic");
        var secretary = await context.AddSecretaryAsync(clinic.ClinicId, email: "secretary@test.local");
        await context.AddSecretaryAsync(clinic.ClinicId, email: "taken@test.local");

        var response = await context.Service.UpdateMyProfileAsync(
            new UpdateSecretaryRequest
            {
                Name = "Mira Secretary",
                Email = "taken@test.local"
            },
            SecretarySelfTestContext.Principal(secretary.Id),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Equal("Email already registered.", badRequest.Value);
    }

    private static T OkValue<T>(ActionResult<T> response)
    {
        var ok = Assert.IsType<OkObjectResult>(response.Result);
        return Assert.IsType<T>(ok.Value);
    }
}

internal sealed class SecretarySelfTestContext : IDisposable
{
    private readonly ServiceProvider _serviceProvider;

    public SecretarySelfTestContext()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));
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
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();
        services.AddScoped<IPasswordHasher<User>, LegacyCompatiblePasswordHasher>();

        _serviceProvider = services.BuildServiceProvider();
        DbContext = _serviceProvider.GetRequiredService<AppDbContext>();
        UserManager = _serviceProvider.GetRequiredService<UserManager<User>>();
        Service = new SecretariesService(DbContext, UserManager);
    }

    public AppDbContext DbContext { get; }
    public UserManager<User> UserManager { get; }
    public SecretariesService Service { get; }

    public Clinic AddClinic(string name)
    {
        var clinic = new Clinic
        {
            ClinicId = Guid.NewGuid(),
            Name = name,
            CreatedBySuperAdminUserId = Guid.NewGuid(),
            IsActive = true
        };

        DbContext.Clinics.Add(clinic);
        DbContext.SaveChanges();
        return clinic;
    }

    public async Task<User> AddSecretaryAsync(
        Guid clinicId,
        string email = "secretary@test.local",
        string password = "SecretaryPass123")
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Mira Secretary",
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            RegisteredAt = DateTime.UtcNow,
            ClinicId = clinicId,
            IsActive = true
        };

        var result = await UserManager.CreateAsync(user, password);
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Description)));

        DbContext.Secretaries.Add(new Secretary
        {
            SecretaryId = user.Id,
            User = user
        });
        await DbContext.SaveChangesAsync();
        return user;
    }

    public static ClaimsPrincipal Principal(Guid userId)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, nameof(UserSystemRole.Secretary))
            ],
            "Test"));
    }

    public void Dispose()
    {
        DbContext.Dispose();
        _serviceProvider.Dispose();
    }
}
