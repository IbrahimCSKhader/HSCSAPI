using HSCSAPI.Data;
using HSCSAPI.DTOs.AuthorizedMember;
using HSCSAPI.DTOs.Auth;
using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Relations;
using HSCSAPI.Services.Auth;
using HSCSAPI.Services.AuthorizedMembers;
using HSCSAPI.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Tests;

public class AuthorizedMembersServiceTests
{
    [Fact]
    public async Task AuthorizedMember_CanReadProfilePatientsAndInvites()
    {
        await using var app = await TestApplicationContext.CreateAsync();
        using var scope = app.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IAuthorizedMembersService>();

        var authorizedMemberId = await dbContext.Users
            .Where(user => user.Email == "member@seed.local")
            .Select(user => user.Id)
            .FirstAsync();

        var principal = TestApplicationContext.CreatePrincipal(authorizedMemberId, UserSystemRole.AuthorizedMember);

        var profileResult = await service.GetMyProfileAsync(principal);
        var profileOk = Assert.IsType<OkObjectResult>(profileResult.Result);
        var profile = Assert.IsType<AuthorizedMemberProfileResponse>(profileOk.Value);

        Assert.Equal(authorizedMemberId, profile.AuthorizedMemberId);
        Assert.Equal("Mona Nassar", profile.Name);
        Assert.Equal("member@seed.local", profile.Email);

        var patientsResult = await service.GetMyPatientsAsync(principal);
        var patientsOk = Assert.IsType<OkObjectResult>(patientsResult.Result);
        var patients = Assert.IsAssignableFrom<List<AuthorizedMemberPatientResponse>>(patientsOk.Value);

        var patient = Assert.Single(patients);
        Assert.Equal("Alaa Nassar", patient.Name);
        Assert.Equal("Mother", patient.RelationshipType);
        Assert.Equal("Central Care Clinic", patient.ClinicName);

        var invitesResult = await service.GetMyInvitesAsync(principal);
        var invitesOk = Assert.IsType<OkObjectResult>(invitesResult.Result);
        var invites = Assert.IsAssignableFrom<List<AuthorizedMemberInviteResponse>>(invitesOk.Value);

        var invite = Assert.Single(invites);
        Assert.Equal("Accepted", invite.Status);
        Assert.Equal("Mother", invite.RelationshipType);
        Assert.Equal("Alaa Nassar", invite.PatientName);
    }

    [Fact]
    public async Task AcceptInvite_AddsPatientRelation_AndMarksInviteAccepted()
    {
        await using var app = await TestApplicationContext.CreateAsync();
        using var scope = app.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var service = scope.ServiceProvider.GetRequiredService<IAuthorizedMembersService>();

        var clinicId = await dbContext.Clinics
            .Where(clinic => clinic.Name == "Central Care Clinic")
            .Select(clinic => clinic.ClinicId)
            .FirstAsync();

        var registerResult = await authService.RegisterPatientAsync(new RegisterPatientRequest
        {
            Name = "Pending Relation Patient",
            Email = "pending.relation.patient@test.local",
            Password = "Patient123",
            ClinicId = clinicId,
            Gender = "Male"
        });

        Assert.True(registerResult.Success);

        var patientId = await dbContext.Users
            .Where(user => user.Email == "pending.relation.patient@test.local")
            .Select(user => user.Id)
            .FirstAsync();

        var authorizedMemberId = await dbContext.Users
            .Where(user => user.Email == "member@seed.local")
            .Select(user => user.Id)
            .FirstAsync();

        var invite = new Invite
        {
            PatientId = patientId,
            AuthorizedMemberId = authorizedMemberId,
            RelationshipType = RelationshipType.Guardian,
            Status = InviteStatus.Pending,
            SentAt = DateTime.UtcNow.AddMinutes(-5)
        };

        dbContext.Invites.Add(invite);
        await dbContext.SaveChangesAsync();

        var principal = TestApplicationContext.CreatePrincipal(authorizedMemberId, UserSystemRole.AuthorizedMember);
        var result = await service.AcceptInviteAsync(invite.InviteId, principal);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AuthorizedMemberInviteResponse>(okResult.Value);

        Assert.Equal("Accepted", response.Status);
        Assert.Equal("Guardian", response.RelationshipType);
        Assert.NotNull(response.RespondedAt);

        var relation = await dbContext.PatientAuthorizedMembers
            .FirstOrDefaultAsync(x => x.PatientId == patientId && x.AuthorizedMemberId == authorizedMemberId);

        Assert.NotNull(relation);
        Assert.Equal(RelationshipType.Guardian, relation!.RelationshipType);

        var persistedInvite = await dbContext.Invites.FirstAsync(x => x.InviteId == invite.InviteId);
        Assert.Equal(InviteStatus.Accepted, persistedInvite.Status);
        Assert.NotNull(persistedInvite.RespondedAt);
    }

    [Fact]
    public async Task RejectInvite_DoesNotAddPatientRelation_AndMarksInviteRejected()
    {
        await using var app = await TestApplicationContext.CreateAsync();
        using var scope = app.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var service = scope.ServiceProvider.GetRequiredService<IAuthorizedMembersService>();

        var clinicId = await dbContext.Clinics
            .Where(clinic => clinic.Name == "Central Care Clinic")
            .Select(clinic => clinic.ClinicId)
            .FirstAsync();

        var registerResult = await authService.RegisterPatientAsync(new RegisterPatientRequest
        {
            Name = "Rejected Relation Patient",
            Email = "rejected.relation.patient@test.local",
            Password = "Patient123",
            ClinicId = clinicId,
            Gender = "Female"
        });

        Assert.True(registerResult.Success);

        var patientId = await dbContext.Users
            .Where(user => user.Email == "rejected.relation.patient@test.local")
            .Select(user => user.Id)
            .FirstAsync();

        var authorizedMemberId = await dbContext.Users
            .Where(user => user.Email == "member@seed.local")
            .Select(user => user.Id)
            .FirstAsync();

        var invite = new Invite
        {
            PatientId = patientId,
            AuthorizedMemberId = authorizedMemberId,
            RelationshipType = RelationshipType.Sister,
            Status = InviteStatus.Pending,
            SentAt = DateTime.UtcNow.AddMinutes(-10)
        };

        dbContext.Invites.Add(invite);
        await dbContext.SaveChangesAsync();

        var principal = TestApplicationContext.CreatePrincipal(authorizedMemberId, UserSystemRole.AuthorizedMember);
        var result = await service.RejectInviteAsync(invite.InviteId, principal);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AuthorizedMemberInviteResponse>(okResult.Value);

        Assert.Equal("Rejected", response.Status);
        Assert.Equal("Sister", response.RelationshipType);
        Assert.NotNull(response.RespondedAt);

        var relationExists = await dbContext.PatientAuthorizedMembers
            .AnyAsync(x => x.PatientId == patientId && x.AuthorizedMemberId == authorizedMemberId);

        Assert.False(relationExists);

        var persistedInvite = await dbContext.Invites.FirstAsync(x => x.InviteId == invite.InviteId);
        Assert.Equal(InviteStatus.Rejected, persistedInvite.Status);
        Assert.NotNull(persistedInvite.RespondedAt);
    }
}
