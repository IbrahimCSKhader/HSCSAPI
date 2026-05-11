using HSCSAPI.Data;
using HSCSAPI.DTOs.Auth;
using HSCSAPI.DTOs.RadiologyTechnologist;
using HSCSAPI.Models.Enums;
using HSCSAPI.Services.Auth;
using HSCSAPI.Services.RadiologyTechnologists;
using HSCSAPI.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Tests;

public class RadiologyTechnologistsServiceTests
{
    [Fact]
    public async Task RadiologyTechnologist_CanReadAndUpdateOwnProfile()
    {
        await using var app = await TestApplicationContext.CreateAsync();
        using var scope = app.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IRadiologyTechnologistsService>();

        var technologistId = await dbContext.Users
            .Where(user => user.Email == "radiology@seed.local")
            .Select(user => user.Id)
            .FirstAsync();

        var principal = TestApplicationContext.CreatePrincipal(technologistId, UserSystemRole.RadiologyTechnologist);

        var profileResult = await service.GetMyProfileAsync(principal);
        var profileOk = Assert.IsType<OkObjectResult>(profileResult.Result);
        var profile = Assert.IsType<RadiologyTechnologistResponse>(profileOk.Value);

        Assert.Equal("Yousef Ray", profile.Name);
        Assert.Equal("RAD-10001", profile.ProfessionalLicenseNumber);

        var updateResult = await service.UpdateMyProfileAsync(new UpdateMyRadiologyTechnologistProfileRequest
        {
            Name = "Yousef Updated",
            PhoneNumber = "0599888883",
            Address = "Jerusalem",
            DateOfBirth = new DateOnly(1994, 5, 2)
        }, principal);

        var updateOk = Assert.IsType<OkObjectResult>(updateResult.Result);
        var updated = Assert.IsType<RadiologyTechnologistResponse>(updateOk.Value);

        Assert.Equal("Yousef Updated", updated.Name);
        Assert.Equal("Jerusalem", updated.Address);

        var persistedTechnologist = await dbContext.RadiologyTechnologists
            .Include(technologist => technologist.User)
            .FirstAsync(technologist => technologist.RadiologyTechnologistId == technologistId);

        Assert.Equal("Yousef Updated", persistedTechnologist.User.Name);
        Assert.Equal("0599888883", persistedTechnologist.User.PhoneNumber);
    }

    [Fact]
    public async Task Secretary_CanListOwnClinicRadiologyTechnologists_ButCannotAccessOtherClinicRadiologyTechnologists()
    {
        await using var app = await TestApplicationContext.CreateAsync();
        using var scope = app.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var service = scope.ServiceProvider.GetRequiredService<IRadiologyTechnologistsService>();

        var northClinicId = await dbContext.Clinics
            .Where(clinic => clinic.Name == "North Family Clinic")
            .Select(clinic => clinic.ClinicId)
            .FirstAsync();

        var registerResult = await authService.RegisterRadiologyTechnologistAsync(new RegisterRadiologyTechnologistRequest
        {
            Name = "North Ray Tech",
            Email = "north.radiology@test.local",
            Password = "RadTech123",
            ClinicId = northClinicId,
            ProfessionalLicenseNumber = "RAD-NORTH-001"
        });

        Assert.True(registerResult.Success);

        var northTechnologistId = await dbContext.Users
            .Where(user => user.Email == "north.radiology@test.local")
            .Select(user => user.Id)
            .FirstAsync();

        var secretaryId = await dbContext.Users
            .Where(user => user.Email == "secretary.admin@seed.local")
            .Select(user => user.Id)
            .FirstAsync();

        var principal = TestApplicationContext.CreatePrincipal(secretaryId, UserSystemRole.Secretary);

        var listResult = await service.GetAllAsync(null, principal);
        var listOk = Assert.IsType<OkObjectResult>(listResult.Result);
        var technologists = Assert.IsAssignableFrom<List<RadiologyTechnologistResponse>>(listOk.Value);

        var technologist = Assert.Single(technologists);
        Assert.Equal("radiology@seed.local", technologist.Email);

        var byClinicResult = await service.GetByClinicAsync(northClinicId, principal);
        var forbiddenClinic = Assert.IsType<ObjectResult>(byClinicResult.Result);
        Assert.Equal(403, forbiddenClinic.StatusCode);

        var byIdResult = await service.GetByIdAsync(northTechnologistId, principal);
        var forbiddenTechnologist = Assert.IsType<ObjectResult>(byIdResult.Result);
        Assert.Equal(403, forbiddenTechnologist.StatusCode);
    }

    [Fact]
    public async Task SuperAdmin_CanUpdateRadiologyTechnologist_AndMoveThemToAnotherClinic()
    {
        await using var app = await TestApplicationContext.CreateAsync();
        using var scope = app.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IRadiologyTechnologistsService>();

        var technologist = await dbContext.RadiologyTechnologists
            .Include(profile => profile.User)
            .FirstAsync(profile => profile.User.Email == "radiology@seed.local");

        var northClinicId = await dbContext.Clinics
            .Where(clinic => clinic.Name == "North Family Clinic")
            .Select(clinic => clinic.ClinicId)
            .FirstAsync();

        var superAdminId = await dbContext.Users
            .Where(user => user.Email == "superadmin@test.local")
            .Select(user => user.Id)
            .FirstAsync();

        var principal = TestApplicationContext.CreatePrincipal(superAdminId, UserSystemRole.SuperAdmin);

        var updateResult = await service.UpdateAsync(technologist.RadiologyTechnologistId, new UpdateRadiologyTechnologistRequest
        {
            Name = "Transferred Ray Tech",
            Email = "transferred.radiology@test.local",
            PhoneNumber = "0599888884",
            Address = "Nablus",
            DateOfBirth = new DateOnly(1991, 7, 4),
            ClinicId = northClinicId,
            ProfessionalLicenseNumber = "RAD-20002"
        }, principal);

        var okResult = Assert.IsType<OkObjectResult>(updateResult.Result);
        var response = Assert.IsType<RadiologyTechnologistResponse>(okResult.Value);

        Assert.Equal("Transferred Ray Tech", response.Name);
        Assert.Equal("transferred.radiology@test.local", response.Email);
        Assert.Equal("North Family Clinic", response.ClinicName);
        Assert.Equal("RAD-20002", response.ProfessionalLicenseNumber);
    }

    [Fact]
    public async Task DeleteRadiologyTechnologist_Succeeds_AndUnassignsLinkedImagingRequests()
    {
        await using var app = await TestApplicationContext.CreateAsync();
        using var scope = app.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IRadiologyTechnologistsService>();

        var technologistId = await dbContext.Users
            .Where(user => user.Email == "radiology@seed.local")
            .Select(user => user.Id)
            .FirstAsync();

        var linkedRequestId = await dbContext.ImagingTestRequests
            .Where(request => request.RadiologyTechnologistId == technologistId)
            .Select(request => request.ImagingTestRequestId)
            .FirstAsync();

        var superAdminId = await dbContext.Users
            .Where(user => user.Email == "superadmin@test.local")
            .Select(user => user.Id)
            .FirstAsync();

        var principal = TestApplicationContext.CreatePrincipal(superAdminId, UserSystemRole.SuperAdmin);
        var deleteResult = await service.DeleteAsync(technologistId, principal);

        Assert.IsType<NoContentResult>(deleteResult);
        Assert.False(await dbContext.Users.AnyAsync(user => user.Id == technologistId));

        var requestAfterDelete = await dbContext.ImagingTestRequests
            .AsNoTracking()
            .FirstAsync(request => request.ImagingTestRequestId == linkedRequestId);

        Assert.Null(requestAfterDelete.RadiologyTechnologistId);
    }
}
