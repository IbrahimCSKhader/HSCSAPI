using HSCSAPI.Data;
using HSCSAPI.DTOs.Clinic;
using HSCSAPI.DTOs.Secretary;
using HSCSAPI.Models.Enums;
using HSCSAPI.Services.Clinics;
using HSCSAPI.Services.Secretaries;
using HSCSAPI.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Tests;

public class ClinicAndSecretaryServiceTests
{
    [Fact]
    public async Task AssignedSecretary_CannotBeMovedToAnotherClinic()
    {
        await using var app = await TestApplicationContext.CreateAsync();
        using var scope = app.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clinicsService = scope.ServiceProvider.GetRequiredService<IClinicsService>();
        var secretariesService = scope.ServiceProvider.GetRequiredService<ISecretariesService>();

        var superAdminId = await dbContext.Users
            .Where(u => u.Email == "superadmin@test.local")
            .Select(u => u.Id)
            .FirstAsync();

        var principal = TestApplicationContext.CreatePrincipal(superAdminId, UserSystemRole.SuperAdmin);

        var createClinicResult = await clinicsService.CreateAsync(new CreateClinicRequest
        {
            Name = "Second Clinic",
            Address = "Ramallah"
        }, principal);

        var createdClinic = Assert.IsType<OkObjectResult>(createClinicResult.Result).Value as ClinicResponse;
        Assert.NotNull(createdClinic);

        var secretaryId = await dbContext.Users
            .Where(u => u.Email == "secretary.staff@seed.local")
            .Select(u => u.Id)
            .FirstAsync();

        var assignResult = await secretariesService.AssignToClinicAsync(
            secretaryId,
            new AssignSecretaryToClinicRequest { ClinicId = createdClinic!.ClinicId },
            principal);

        var badRequest = Assert.IsType<BadRequestObjectResult>(assignResult.Result);
        Assert.Equal("Secretary is already assigned to another clinic.", badRequest.Value);
    }

    [Fact]
    public async Task ClinicDelete_IsBlockedWhenUsersAreAssigned()
    {
        await using var app = await TestApplicationContext.CreateAsync();
        using var scope = app.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clinicsService = scope.ServiceProvider.GetRequiredService<IClinicsService>();

        var clinicId = await dbContext.Clinics
            .Where(c => c.Name == "Central Care Clinic")
            .Select(c => c.ClinicId)
            .FirstAsync();

        var deleteResult = await clinicsService.DeleteAsync(clinicId);

        var badRequest = Assert.IsType<BadRequestObjectResult>(deleteResult);
        Assert.Equal("Cannot delete a clinic while users are still assigned to it.", badRequest.Value);
    }

    [Fact]
    public async Task Secretary_CanReadOwnClinicTeam()
    {
        await using var app = await TestApplicationContext.CreateAsync();
        using var scope = app.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var secretariesService = scope.ServiceProvider.GetRequiredService<ISecretariesService>();

        var secretaryId = await dbContext.Users
            .Where(u => u.Email == "secretary.admin@seed.local")
            .Select(u => u.Id)
            .FirstAsync();

        var principal = TestApplicationContext.CreatePrincipal(secretaryId, UserSystemRole.Secretary);
        var result = await secretariesService.GetMyClinicSecretariesAsync(principal);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var secretaries = Assert.IsAssignableFrom<List<SecretaryResponse>>(okResult.Value);

        Assert.Equal(2, secretaries.Count);
        Assert.All(secretaries, secretary => Assert.Equal("Central Care Clinic", secretary.ClinicName));
    }
}
