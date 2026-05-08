using HSCSAPI.Data;
using HSCSAPI.Models.Enums;
using HSCSAPI.Services.Identity;
using HSCSAPI.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Tests;

public class SeedServiceTests
{
    [Fact]
    public async Task SeedService_CoversApplicationTables_AndIsIdempotent()
    {
        await using var app = await TestApplicationContext.CreateAsync();
        using var scope = app.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seedService = scope.ServiceProvider.GetRequiredService<IdentitySeedService>();

        var countsBeforeReseed = await LoadCountsAsync(dbContext);

        Assert.Equal(Enum.GetNames<UserSystemRole>().Length, countsBeforeReseed["Roles"]);
        Assert.True(countsBeforeReseed["Users"] >= 8);
        Assert.True(countsBeforeReseed["UserRoles"] >= 8);
        Assert.Equal(2, countsBeforeReseed["Clinics"]);
        Assert.True(countsBeforeReseed["Patients"] > 0);
        Assert.True(countsBeforeReseed["Doctors"] > 0);
        Assert.True(countsBeforeReseed["Secretaries"] > 0);
        Assert.True(countsBeforeReseed["AuthorizedMembers"] > 0);
        Assert.True(countsBeforeReseed["LaboratoryTechnologists"] > 0);
        Assert.True(countsBeforeReseed["RadiologyTechnologists"] > 0);
        Assert.True(countsBeforeReseed["UserVerificationCodes"] > 0);
        Assert.True(countsBeforeReseed["PatientAuthorizedMembers"] > 0);
        Assert.True(countsBeforeReseed["Invites"] > 0);
        Assert.True(countsBeforeReseed["Notifications"] > 0);
        Assert.True(countsBeforeReseed["Reminders"] > 0);
        Assert.True(countsBeforeReseed["Reports"] > 0);
        Assert.True(countsBeforeReseed["ReportInformations"] > 0);
        Assert.True(countsBeforeReseed["MedicalFiles"] > 0);
        Assert.True(countsBeforeReseed["FileDownloadRequests"] > 0);
        Assert.True(countsBeforeReseed["LabTestRequests"] > 0);
        Assert.True(countsBeforeReseed["ImagingTestRequests"] > 0);
        Assert.True(countsBeforeReseed["AvailabilitySlots"] > 0);
        Assert.True(countsBeforeReseed["Appointments"] > 0);

        await seedService.SeedAsync();

        var countsAfterReseed = await LoadCountsAsync(dbContext);

        foreach (var (tableName, expectedCount) in countsBeforeReseed)
        {
            Assert.Equal(expectedCount, countsAfterReseed[tableName]);
        }
    }

    private static async Task<Dictionary<string, int>> LoadCountsAsync(AppDbContext dbContext)
    {
        return new Dictionary<string, int>
        {
            ["Roles"] = await dbContext.Roles.CountAsync(),
            ["Users"] = await dbContext.Users.CountAsync(),
            ["UserRoles"] = await dbContext.UserRoles.CountAsync(),
            ["Clinics"] = await dbContext.Clinics.CountAsync(),
            ["Patients"] = await dbContext.Patients.CountAsync(),
            ["Doctors"] = await dbContext.Doctors.CountAsync(),
            ["Secretaries"] = await dbContext.Secretaries.CountAsync(),
            ["AuthorizedMembers"] = await dbContext.AuthorizedMembers.CountAsync(),
            ["LaboratoryTechnologists"] = await dbContext.LaboratoryTechnologists.CountAsync(),
            ["RadiologyTechnologists"] = await dbContext.RadiologyTechnologists.CountAsync(),
            ["UserVerificationCodes"] = await dbContext.UserVerificationCodes.CountAsync(),
            ["PatientAuthorizedMembers"] = await dbContext.PatientAuthorizedMembers.CountAsync(),
            ["Invites"] = await dbContext.Invites.CountAsync(),
            ["Notifications"] = await dbContext.Notifications.CountAsync(),
            ["Reminders"] = await dbContext.Reminders.CountAsync(),
            ["Reports"] = await dbContext.Reports.CountAsync(),
            ["ReportInformations"] = await dbContext.ReportInformations.CountAsync(),
            ["MedicalFiles"] = await dbContext.MedicalFiles.CountAsync(),
            ["FileDownloadRequests"] = await dbContext.FileDownloadRequests.CountAsync(),
            ["LabTestRequests"] = await dbContext.LabTestRequests.CountAsync(),
            ["ImagingTestRequests"] = await dbContext.ImagingTestRequests.CountAsync(),
            ["AvailabilitySlots"] = await dbContext.AvailabilitySlots.CountAsync(),
            ["Appointments"] = await dbContext.Appointments.CountAsync()
        };
    }
}
