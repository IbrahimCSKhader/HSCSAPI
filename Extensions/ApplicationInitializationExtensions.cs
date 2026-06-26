using HSCSAPI.Data;
using HSCSAPI.Services.Identity;
using HSCSAPI.Services.Laboratory;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Extensions;

public static class ApplicationInitializationExtensions
{
    public static async Task ApplyMigrationsAndSeedAsync(this WebApplication app)
    {
        var logger = app.Services
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("ApplicationInitialization");

        try
        {
            await using var scope = app.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.MigrateAsync();

            var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeedService>();
            await seeder.SeedAsync();

            var labTemplateSeeder = scope.ServiceProvider.GetRequiredService<LabTestTemplateSeeder>();
            await labTemplateSeeder.SeedAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while applying database migrations or seeding application data.");
        }
    }
}
