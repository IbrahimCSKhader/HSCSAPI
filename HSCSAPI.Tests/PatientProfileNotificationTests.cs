using System.Security.Claims;
using HSCSAPI.Data;
using HSCSAPI.Models.Clinics;
using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Identity;
using HSCSAPI.Models.Profiles;
using HSCSAPI.Models.Relations;
using HSCSAPI.Services.PatientProfile;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Tests;

public class PatientProfileNotificationTests
{
    [Fact]
    public async Task DeactivateAuthorizedMember_CreatesAccessRemovedNotification()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var dbContext = new AppDbContext(options);
        var clinicId = Guid.NewGuid();
        dbContext.Clinics.Add(new Clinic
        {
            ClinicId = clinicId,
            Name = "Central Clinic",
            CreatedBySuperAdminUserId = Guid.NewGuid()
        });
        var patientUser = AddUser("Sarah Patient", clinicId);
        var memberUser = AddUser("Care Giver", null);
        dbContext.Patients.Add(new Patient
        {
            PatientId = patientUser.Id,
            UserID = "P-100",
            Gender = Gender.Female,
            BloodType = BloodType.OPositive,
            User = patientUser
        });
        dbContext.AuthorizedMembers.Add(new AuthorizedMember
        {
            AuthorizedMemberId = memberUser.Id,
            User = memberUser
        });
        dbContext.PatientAuthorizedMembers.Add(new PatientAuthorizedMember
        {
            PatientId = patientUser.Id,
            AuthorizedMemberId = memberUser.Id,
            RelationshipType = RelationshipType.Guardian,
            AuthorizedAt = DateTime.UtcNow,
            IsActive = true
        });
        await dbContext.SaveChangesAsync();

        var service = new PatientProfileService(
            dbContext,
            null!,
            new TestWebHostEnvironment(Path.Combine(Path.GetTempPath(), "hscsapi-patient-profile-tests", Guid.NewGuid().ToString("N"))));

        await service.DeactivateAuthorizedMemberAsync(
            memberUser.Id,
            Principal(patientUser.Id),
            CancellationToken.None);

        var notification = Assert.Single(dbContext.Notifications.Where(x => x.UserId == memberUser.Id));
        Assert.Equal("Access removed", notification.Title);
        Assert.Equal("Sarah Patient removed your access to their health information.", notification.Message);
        Assert.Equal("MedicalRecord", notification.Category);
        Assert.False(dbContext.PatientAuthorizedMembers.Single().IsActive);
    }

    private static User AddUser(string name, Guid? clinicId)
    {
        var id = Guid.NewGuid();
        var email = $"{id:N}@test.local";
        return new User
        {
            Id = id,
            Name = name,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            RegisteredAt = DateTime.UtcNow,
            ClinicId = clinicId,
            IsActive = true
        };
    }

    private static ClaimsPrincipal Principal(Guid userId)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, nameof(UserSystemRole.Patient))
            ],
            "Test"));
    }
}
