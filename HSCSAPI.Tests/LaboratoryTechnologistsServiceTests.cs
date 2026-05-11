using HSCSAPI.Data;
using HSCSAPI.DTOs.Auth;
using HSCSAPI.DTOs.LaboratoryTechnologist;
using HSCSAPI.Models.Enums;
using HSCSAPI.Services.Auth;
using HSCSAPI.Services.LaboratoryTechnologists;
using HSCSAPI.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Tests;

public class LaboratoryTechnologistsServiceTests
{
    [Fact]
    public async Task LaboratoryTechnologist_CanReadAndUpdateOwnProfile()
    {
        await using var app = await TestApplicationContext.CreateAsync();
        using var scope = app.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<ILaboratoryTechnologistsService>();

        var technologistId = await dbContext.Users
            .Where(user => user.Email == "labtech@seed.local")
            .Select(user => user.Id)
            .FirstAsync();

        var principal = TestApplicationContext.CreatePrincipal(technologistId, UserSystemRole.LaboratoryTechnologist);

        var profileResult = await service.GetMyProfileAsync(principal);
        var profileOk = Assert.IsType<OkObjectResult>(profileResult.Result);
        var profile = Assert.IsType<LaboratoryTechnologistResponse>(profileOk.Value);

        Assert.Equal("Rana Lab", profile.Name);
        Assert.Equal("LAB-10001", profile.ProfessionalLicenseNumber);

        var updateResult = await service.UpdateMyProfileAsync(new UpdateMyLaboratoryTechnologistProfileRequest
        {
            Name = "Rana Updated",
            PhoneNumber = "0599888881",
            Address = "Jerusalem",
            DateOfBirth = new DateOnly(1992, 10, 1)
        }, principal);

        var updateOk = Assert.IsType<OkObjectResult>(updateResult.Result);
        var updated = Assert.IsType<LaboratoryTechnologistResponse>(updateOk.Value);

        Assert.Equal("Rana Updated", updated.Name);
        Assert.Equal("Jerusalem", updated.Address);

        var persistedTechnologist = await dbContext.LaboratoryTechnologists
            .Include(technologist => technologist.User)
            .FirstAsync(technologist => technologist.LaboratoryTechnologistId == technologistId);

        Assert.Equal("Rana Updated", persistedTechnologist.User.Name);
        Assert.Equal("0599888881", persistedTechnologist.User.PhoneNumber);
    }

    [Fact]
    public async Task Secretary_CanListOwnClinicLaboratoryTechnologists_ButCannotAccessOtherClinicLaboratoryTechnologists()
    {
        await using var app = await TestApplicationContext.CreateAsync();
        using var scope = app.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var service = scope.ServiceProvider.GetRequiredService<ILaboratoryTechnologistsService>();

        var northClinicId = await dbContext.Clinics
            .Where(clinic => clinic.Name == "North Family Clinic")
            .Select(clinic => clinic.ClinicId)
            .FirstAsync();

        var registerResult = await authService.RegisterLaboratoryTechnologistAsync(new RegisterLaboratoryTechnologistRequest
        {
            Name = "North Lab Tech",
            Email = "north.labtech@test.local",
            Password = "LabTech123",
            ClinicId = northClinicId,
            ProfessionalLicenseNumber = "LAB-NORTH-001"
        });

        Assert.True(registerResult.Success);

        var northTechnologistId = await dbContext.Users
            .Where(user => user.Email == "north.labtech@test.local")
            .Select(user => user.Id)
            .FirstAsync();

        var secretaryId = await dbContext.Users
            .Where(user => user.Email == "secretary.admin@seed.local")
            .Select(user => user.Id)
            .FirstAsync();

        var principal = TestApplicationContext.CreatePrincipal(secretaryId, UserSystemRole.Secretary);

        var listResult = await service.GetAllAsync(null, principal);
        var listOk = Assert.IsType<OkObjectResult>(listResult.Result);
        var technologists = Assert.IsAssignableFrom<List<LaboratoryTechnologistResponse>>(listOk.Value);

        var technologist = Assert.Single(technologists);
        Assert.Equal("labtech@seed.local", technologist.Email);

        var byClinicResult = await service.GetByClinicAsync(northClinicId, principal);
        var forbiddenClinic = Assert.IsType<ObjectResult>(byClinicResult.Result);
        Assert.Equal(403, forbiddenClinic.StatusCode);

        var byIdResult = await service.GetByIdAsync(northTechnologistId, principal);
        var forbiddenTechnologist = Assert.IsType<ObjectResult>(byIdResult.Result);
        Assert.Equal(403, forbiddenTechnologist.StatusCode);
    }

    [Fact]
    public async Task SuperAdmin_CanUpdateLaboratoryTechnologist_AndMoveThemToAnotherClinic()
    {
        await using var app = await TestApplicationContext.CreateAsync();
        using var scope = app.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<ILaboratoryTechnologistsService>();

        var technologist = await dbContext.LaboratoryTechnologists
            .Include(profile => profile.User)
            .FirstAsync(profile => profile.User.Email == "labtech@seed.local");

        var northClinicId = await dbContext.Clinics
            .Where(clinic => clinic.Name == "North Family Clinic")
            .Select(clinic => clinic.ClinicId)
            .FirstAsync();

        var superAdminId = await dbContext.Users
            .Where(user => user.Email == "superadmin@test.local")
            .Select(user => user.Id)
            .FirstAsync();

        var principal = TestApplicationContext.CreatePrincipal(superAdminId, UserSystemRole.SuperAdmin);

        var updateResult = await service.UpdateAsync(technologist.LaboratoryTechnologistId, new UpdateLaboratoryTechnologistRequest
        {
            Name = "Transferred Lab Tech",
            Email = "transferred.labtech@test.local",
            PhoneNumber = "0599888882",
            Address = "Nablus",
            DateOfBirth = new DateOnly(1990, 4, 3),
            ClinicId = northClinicId,
            ProfessionalLicenseNumber = "LAB-20002"
        }, principal);

        var okResult = Assert.IsType<OkObjectResult>(updateResult.Result);
        var response = Assert.IsType<LaboratoryTechnologistResponse>(okResult.Value);

        Assert.Equal("Transferred Lab Tech", response.Name);
        Assert.Equal("transferred.labtech@test.local", response.Email);
        Assert.Equal("North Family Clinic", response.ClinicName);
        Assert.Equal("LAB-20002", response.ProfessionalLicenseNumber);
    }

    [Fact]
    public async Task DeleteLaboratoryTechnologist_Succeeds_AndUnassignsLinkedLabRequests()
    {
        await using var app = await TestApplicationContext.CreateAsync();
        using var scope = app.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<ILaboratoryTechnologistsService>();

        var technologistId = await dbContext.Users
            .Where(user => user.Email == "labtech@seed.local")
            .Select(user => user.Id)
            .FirstAsync();

        var linkedRequestId = await dbContext.LabTestRequests
            .Where(request => request.LaboratoryTechnologistId == technologistId)
            .Select(request => request.LabTestRequestId)
            .FirstAsync();

        var superAdminId = await dbContext.Users
            .Where(user => user.Email == "superadmin@test.local")
            .Select(user => user.Id)
            .FirstAsync();

        var principal = TestApplicationContext.CreatePrincipal(superAdminId, UserSystemRole.SuperAdmin);
        var deleteResult = await service.DeleteAsync(technologistId, principal);

        Assert.IsType<NoContentResult>(deleteResult);
        Assert.False(await dbContext.Users.AnyAsync(user => user.Id == technologistId));

        var requestAfterDelete = await dbContext.LabTestRequests
            .AsNoTracking()
            .FirstAsync(request => request.LabTestRequestId == linkedRequestId);

        Assert.Null(requestAfterDelete.LaboratoryTechnologistId);
    }
}
