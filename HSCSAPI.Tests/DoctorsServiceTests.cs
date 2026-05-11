using HSCSAPI.Data;
using HSCSAPI.DTOs.Auth;
using HSCSAPI.DTOs.Doctor;
using HSCSAPI.Models.Enums;
using HSCSAPI.Services.Auth;
using HSCSAPI.Services.Doctors;
using HSCSAPI.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Tests;

public class DoctorsServiceTests
{
    [Fact]
    public async Task Doctor_CanReadAndUpdateOwnProfile()
    {
        await using var app = await TestApplicationContext.CreateAsync();
        using var scope = app.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IDoctorsService>();

        var doctorId = await dbContext.Users
            .Where(user => user.Email == "doctor@seed.local")
            .Select(user => user.Id)
            .FirstAsync();

        var principal = TestApplicationContext.CreatePrincipal(doctorId, UserSystemRole.Doctor);

        var profileResult = await service.GetMyProfileAsync(principal);
        var profileOk = Assert.IsType<OkObjectResult>(profileResult.Result);
        var profile = Assert.IsType<DoctorResponse>(profileOk.Value);

        Assert.Equal("Dr. Samer Khalil", profile.Name);
        Assert.Equal("DOC-10001", profile.ProfessionalLicenseNumber);

        var updateResult = await service.UpdateMyProfileAsync(new UpdateMyDoctorProfileRequest
        {
            Name = "Dr. Samer Updated",
            PhoneNumber = "0599666666",
            Address = "Jerusalem",
            DateOfBirth = new DateOnly(1986, 2, 14)
        }, principal);

        var updateOk = Assert.IsType<OkObjectResult>(updateResult.Result);
        var updated = Assert.IsType<DoctorResponse>(updateOk.Value);

        Assert.Equal("Dr. Samer Updated", updated.Name);
        Assert.Equal("Jerusalem", updated.Address);

        var persistedDoctor = await dbContext.Doctors
            .Include(doctor => doctor.User)
            .FirstAsync(doctor => doctor.DoctorId == doctorId);

        Assert.Equal("Dr. Samer Updated", persistedDoctor.User.Name);
        Assert.Equal("0599666666", persistedDoctor.User.PhoneNumber);
    }

    [Fact]
    public async Task Secretary_CanListOwnClinicDoctors_ButCannotAccessOtherClinicDoctors()
    {
        await using var app = await TestApplicationContext.CreateAsync();
        using var scope = app.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var service = scope.ServiceProvider.GetRequiredService<IDoctorsService>();

        var northClinicId = await dbContext.Clinics
            .Where(clinic => clinic.Name == "North Family Clinic")
            .Select(clinic => clinic.ClinicId)
            .FirstAsync();

        var registerResult = await authService.RegisterDoctorAsync(new RegisterDoctorRequest
        {
            Name = "North Clinic Doctor",
            Email = "north.doctor@test.local",
            Password = "Doctor123",
            ClinicId = northClinicId,
            ProfessionalLicenseNumber = "DOC-NORTH-001"
        });

        Assert.True(registerResult.Success);

        var northDoctorId = await dbContext.Users
            .Where(user => user.Email == "north.doctor@test.local")
            .Select(user => user.Id)
            .FirstAsync();

        var secretaryId = await dbContext.Users
            .Where(user => user.Email == "secretary.admin@seed.local")
            .Select(user => user.Id)
            .FirstAsync();

        var principal = TestApplicationContext.CreatePrincipal(secretaryId, UserSystemRole.Secretary);

        var listResult = await service.GetAllAsync(null, principal);
        var listOk = Assert.IsType<OkObjectResult>(listResult.Result);
        var doctors = Assert.IsAssignableFrom<List<DoctorResponse>>(listOk.Value);

        var doctor = Assert.Single(doctors);
        Assert.Equal("doctor@seed.local", doctor.Email);

        var byClinicResult = await service.GetByClinicAsync(northClinicId, principal);
        var forbiddenClinic = Assert.IsType<ObjectResult>(byClinicResult.Result);
        Assert.Equal(403, forbiddenClinic.StatusCode);

        var byIdResult = await service.GetByIdAsync(northDoctorId, principal);
        var forbiddenDoctor = Assert.IsType<ObjectResult>(byIdResult.Result);
        Assert.Equal(403, forbiddenDoctor.StatusCode);
    }

    [Fact]
    public async Task SuperAdmin_CanUpdateDoctor_AndMoveThemToAnotherClinic()
    {
        await using var app = await TestApplicationContext.CreateAsync();
        using var scope = app.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IDoctorsService>();

        var doctor = await dbContext.Doctors
            .Include(profile => profile.User)
            .FirstAsync(profile => profile.User.Email == "doctor@seed.local");

        var northClinicId = await dbContext.Clinics
            .Where(clinic => clinic.Name == "North Family Clinic")
            .Select(clinic => clinic.ClinicId)
            .FirstAsync();

        var superAdminId = await dbContext.Users
            .Where(user => user.Email == "superadmin@test.local")
            .Select(user => user.Id)
            .FirstAsync();

        var principal = TestApplicationContext.CreatePrincipal(superAdminId, UserSystemRole.SuperAdmin);

        var updateResult = await service.UpdateAsync(doctor.DoctorId, new UpdateDoctorRequest
        {
            Name = "Transferred Doctor",
            Email = "transferred.doctor@test.local",
            PhoneNumber = "0599777777",
            Address = "Nablus",
            DateOfBirth = new DateOnly(1982, 3, 1),
            ClinicId = northClinicId,
            ProfessionalLicenseNumber = "DOC-20002"
        }, principal);

        var okResult = Assert.IsType<OkObjectResult>(updateResult.Result);
        var response = Assert.IsType<DoctorResponse>(okResult.Value);

        Assert.Equal("Transferred Doctor", response.Name);
        Assert.Equal("transferred.doctor@test.local", response.Email);
        Assert.Equal("North Family Clinic", response.ClinicName);
        Assert.Equal("DOC-20002", response.ProfessionalLicenseNumber);
    }

    [Fact]
    public async Task DeleteDoctor_ReturnsBadRequest_WhenDependentDataExists()
    {
        await using var app = await TestApplicationContext.CreateAsync();
        using var scope = app.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IDoctorsService>();

        var doctorId = await dbContext.Users
            .Where(user => user.Email == "doctor@seed.local")
            .Select(user => user.Id)
            .FirstAsync();

        var superAdminId = await dbContext.Users
            .Where(user => user.Email == "superadmin@test.local")
            .Select(user => user.Id)
            .FirstAsync();

        var principal = TestApplicationContext.CreatePrincipal(superAdminId, UserSystemRole.SuperAdmin);
        var deleteResult = await service.DeleteAsync(doctorId, principal);

        var badRequest = Assert.IsType<BadRequestObjectResult>(deleteResult);
        var message = Assert.IsType<string>(badRequest.Value);

        Assert.Contains("availability slots", message);
    }
}
