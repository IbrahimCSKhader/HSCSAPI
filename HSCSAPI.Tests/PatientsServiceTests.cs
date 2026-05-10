using HSCSAPI.Data;
using HSCSAPI.DTOs.Auth;
using HSCSAPI.DTOs.Patient;
using HSCSAPI.Models.Enums;
using HSCSAPI.Services.Auth;
using HSCSAPI.Services.Patients;
using HSCSAPI.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Tests;

public class PatientsServiceTests
{
    [Fact]
    public async Task Patient_CanReadAndUpdateOwnProfile()
    {
        await using var app = await TestApplicationContext.CreateAsync();
        using var scope = app.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IPatientsService>();

        var patientId = await dbContext.Users
            .Where(user => user.Email == "patient@seed.local")
            .Select(user => user.Id)
            .FirstAsync();

        var principal = TestApplicationContext.CreatePrincipal(patientId, UserSystemRole.Patient);

        var profileResult = await service.GetMyProfileAsync(principal);
        var profileOk = Assert.IsType<OkObjectResult>(profileResult.Result);
        var profile = Assert.IsType<PatientResponse>(profileOk.Value);

        Assert.Equal("Alaa Nassar", profile.Name);
        Assert.Equal("Male", profile.Gender);
        Assert.Equal("OPositive", profile.BloodType);

        var updateResult = await service.UpdateMyProfileAsync(new UpdateMyPatientProfileRequest
        {
            Name = "Alaa Updated",
            PhoneNumber = "0599555555",
            Address = "Jerusalem",
            DateOfBirth = new DateOnly(1998, 7, 1),
            Gender = "Other",
            BloodType = "ANegative"
        }, principal);

        var updateOk = Assert.IsType<OkObjectResult>(updateResult.Result);
        var updated = Assert.IsType<PatientResponse>(updateOk.Value);

        Assert.Equal("Alaa Updated", updated.Name);
        Assert.Equal("Jerusalem", updated.Address);
        Assert.Equal("Other", updated.Gender);
        Assert.Equal("ANegative", updated.BloodType);

        var persistedPatient = await dbContext.Patients
            .Include(patient => patient.User)
            .FirstAsync(patient => patient.PatientId == patientId);

        Assert.Equal("Alaa Updated", persistedPatient.User.Name);
        Assert.Equal("0599555555", persistedPatient.User.PhoneNumber);
        Assert.Equal(Gender.Other, persistedPatient.Gender);
        Assert.Equal(BloodType.ANegative, persistedPatient.BloodType);
    }

    [Fact]
    public async Task Secretary_CanListOwnClinicPatients_ButCannotAccessOtherClinicPatients()
    {
        await using var app = await TestApplicationContext.CreateAsync();
        using var scope = app.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var service = scope.ServiceProvider.GetRequiredService<IPatientsService>();

        var northClinicId = await dbContext.Clinics
            .Where(clinic => clinic.Name == "North Family Clinic")
            .Select(clinic => clinic.ClinicId)
            .FirstAsync();

        var registerResult = await authService.RegisterPatientAsync(new RegisterPatientRequest
        {
            Name = "North Clinic Patient",
            Email = "north.patient@test.local",
            Password = "Patient123",
            ClinicId = northClinicId,
            Gender = "Female"
        });

        Assert.True(registerResult.Success);

        var northPatientId = await dbContext.Users
            .Where(user => user.Email == "north.patient@test.local")
            .Select(user => user.Id)
            .FirstAsync();

        var secretaryId = await dbContext.Users
            .Where(user => user.Email == "secretary.admin@seed.local")
            .Select(user => user.Id)
            .FirstAsync();

        var principal = TestApplicationContext.CreatePrincipal(secretaryId, UserSystemRole.Secretary);

        var listResult = await service.GetAllAsync(null, principal);
        var listOk = Assert.IsType<OkObjectResult>(listResult.Result);
        var patients = Assert.IsAssignableFrom<List<PatientResponse>>(listOk.Value);

        var patient = Assert.Single(patients);
        Assert.Equal("patient@seed.local", patient.Email);

        var byClinicResult = await service.GetByClinicAsync(northClinicId, principal);
        var forbiddenClinic = Assert.IsType<ObjectResult>(byClinicResult.Result);
        Assert.Equal(403, forbiddenClinic.StatusCode);

        var byIdResult = await service.GetByIdAsync(northPatientId, principal);
        var forbiddenPatient = Assert.IsType<ObjectResult>(byIdResult.Result);
        Assert.Equal(403, forbiddenPatient.StatusCode);
    }

    [Fact]
    public async Task SuperAdmin_CanUpdatePatient_AndMoveThemToAnotherClinic()
    {
        await using var app = await TestApplicationContext.CreateAsync();
        using var scope = app.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IPatientsService>();

        var patient = await dbContext.Patients
            .Include(profile => profile.User)
            .FirstAsync(profile => profile.User.Email == "patient@seed.local");

        var oldPatientUserId = patient.UserID;
        var northClinicId = await dbContext.Clinics
            .Where(clinic => clinic.Name == "North Family Clinic")
            .Select(clinic => clinic.ClinicId)
            .FirstAsync();

        var superAdminId = await dbContext.Users
            .Where(user => user.Email == "superadmin@test.local")
            .Select(user => user.Id)
            .FirstAsync();

        var principal = TestApplicationContext.CreatePrincipal(superAdminId, UserSystemRole.SuperAdmin);

        var updateResult = await service.UpdateAsync(patient.PatientId, new UpdatePatientRequest
        {
            Name = "Transferred Patient",
            Email = "transferred.patient@test.local",
            PhoneNumber = "0599444444",
            Address = "Nablus",
            DateOfBirth = new DateOnly(1999, 1, 2),
            Gender = "Female",
            BloodType = "BPositive",
            ClinicId = northClinicId
        }, principal);

        var okResult = Assert.IsType<OkObjectResult>(updateResult.Result);
        var response = Assert.IsType<PatientResponse>(okResult.Value);

        Assert.Equal("Transferred Patient", response.Name);
        Assert.Equal("transferred.patient@test.local", response.Email);
        Assert.Equal("North Family Clinic", response.ClinicName);
        Assert.NotEqual(oldPatientUserId, response.PatientUserId);
        Assert.StartsWith(northClinicId.ToString("N")[..4].ToUpperInvariant(), response.PatientUserId);
    }

    [Fact]
    public async Task DeletePatient_ReturnsBadRequest_WhenDependentDataExists()
    {
        await using var app = await TestApplicationContext.CreateAsync();
        using var scope = app.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IPatientsService>();

        var patientId = await dbContext.Users
            .Where(user => user.Email == "patient@seed.local")
            .Select(user => user.Id)
            .FirstAsync();

        var superAdminId = await dbContext.Users
            .Where(user => user.Email == "superadmin@test.local")
            .Select(user => user.Id)
            .FirstAsync();

        var principal = TestApplicationContext.CreatePrincipal(superAdminId, UserSystemRole.SuperAdmin);
        var deleteResult = await service.DeleteAsync(patientId, principal);

        var badRequest = Assert.IsType<BadRequestObjectResult>(deleteResult);
        var message = Assert.IsType<string>(badRequest.Value);

        Assert.Contains("appointments", message);
    }
}
