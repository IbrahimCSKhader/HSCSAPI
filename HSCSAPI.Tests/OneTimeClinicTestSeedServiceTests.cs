using HSCSAPI.Data;
using HSCSAPI.Models.Enums;
using HSCSAPI.Services.Testing;
using HSCSAPI.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Tests;

public class OneTimeClinicTestSeedServiceTests
{
    private static readonly string[] TargetClinicNames =
    {
        "Test Seed Clinic - Hebron",
        "Test Seed Clinic - Ramallah",
        "Test Seed Clinic - Nablus"
    };

    [Fact]
    public async Task SeedOnce_CreatesMultipleClinicsWithStaff_AndBlocksSecondExecution()
    {
        await using var app = await TestApplicationContext.CreateAsync();

        using (var firstScope = app.CreateScope())
        {
            var dbContext = firstScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userManager = firstScope.ServiceProvider.GetRequiredService<UserManager<Models.Identity.User>>();
            var seedService = firstScope.ServiceProvider.GetRequiredService<OneTimeClinicTestSeedService>();

            var result = await seedService.SeedOnceAsync();

            Assert.True(result.Success);
            Assert.False(result.AlreadyExecuted);
            Assert.Equal("TestSeed123", result.DefaultPassword);
            Assert.Equal(3, result.ClinicsCount);
            Assert.Equal(18, result.StaffCount);

            var clinics = await dbContext.Clinics
                .Where(clinic => TargetClinicNames.Contains(clinic.Name))
                .OrderBy(clinic => clinic.Name)
                .ToListAsync();

            Assert.Equal(3, clinics.Count);

            foreach (var clinic in clinics)
            {
                Assert.NotNull(clinic.AdminSecretaryId);

                var clinicUsers = await dbContext.Users
                    .Where(user => user.ClinicId == clinic.ClinicId)
                    .ToListAsync();

                Assert.Equal(6, clinicUsers.Count);
                Assert.Contains(clinicUsers, user => user.Id == clinic.AdminSecretaryId);

                var roles = new List<string>();
                foreach (var clinicUser in clinicUsers)
                {
                    var userRoles = await userManager.GetRolesAsync(clinicUser);
                    var role = Assert.Single(userRoles);
                    roles.Add(role);
                }

                Assert.Equal(2, roles.Count(role => role == nameof(UserSystemRole.Secretary)));
                Assert.Equal(2, roles.Count(role => role == nameof(UserSystemRole.Doctor)));
                Assert.Equal(1, roles.Count(role => role == nameof(UserSystemRole.LaboratoryTechnologist)));
                Assert.Equal(1, roles.Count(role => role == nameof(UserSystemRole.RadiologyTechnologist)));
            }
        }

        using (var secondScope = app.CreateScope())
        {
            var dbContext = secondScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seedService = secondScope.ServiceProvider.GetRequiredService<OneTimeClinicTestSeedService>();

            var secondResult = await seedService.SeedOnceAsync();

            Assert.False(secondResult.Success);
            Assert.True(secondResult.AlreadyExecuted);
            Assert.Equal(3, secondResult.ClinicsCount);
            Assert.Equal(18, secondResult.StaffCount);

            var totalTargetClinics = await dbContext.Clinics
                .CountAsync(clinic => TargetClinicNames.Contains(clinic.Name));

            var totalTargetUsers = await dbContext.Users
                .CountAsync(user => user.ClinicId != null && TargetClinicNames.Contains(user.Clinic!.Name));

            Assert.Equal(3, totalTargetClinics);
            Assert.Equal(18, totalTargetUsers);
        }
    }
}
