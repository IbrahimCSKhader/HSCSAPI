using HSCSAPI.Controllers;
using HSCSAPI.Data;
using HSCSAPI.Models.Appointments;
using HSCSAPI.Models.Clinics;
using HSCSAPI.Models.Identity;
using HSCSAPI.Models.Relations;
using HSCSAPI.Services.Clinics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Tests;

public class ActivationStateTests
{
    [Fact]
    public void PersistedDeactivatableEntities_HaveRequiredIsActiveProperties()
    {
        using var context = CreateDbContext();

        foreach (var entityType in new[]
                 {
                     typeof(User), typeof(Clinic), typeof(Appointment),
                     typeof(PatientAuthorizedMember), typeof(Invite)
                 })
        {
            var property = context.Model.FindEntityType(entityType)?.FindProperty("IsActive");
            Assert.NotNull(property);
            Assert.False(property.IsNullable);
        }
    }

    [Fact]
    public void Controllers_ExposeDeleteOnlyForExplicitUserOwnedResources()
    {
        var deleteEndpoints = typeof(AuthController).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(type => type.GetMethods())
            .SelectMany(method => method.GetCustomAttributes(typeof(HttpMethodAttribute), true)
                .Cast<HttpMethodAttribute>())
            .Where(attribute => attribute.HttpMethods.Any(method => string.Equals(method, "DELETE", StringComparison.OrdinalIgnoreCase)))
            .Select(attribute => attribute.Template)
            .OrderBy(template => template)
            .ToArray();

        Assert.Equal(new[]
        {
            "{chatId:guid}/messages/{messageId:guid}",
            "{notificationId:guid}",
            "my-clinic/doctors/{doctorId:guid}/availability-slots/{slotId:guid}"
        }, deleteEndpoints);
    }

    [Fact]
    public async Task ClinicDeactivateAndActivate_PreserveTheClinicRecord()
    {
        await using var context = CreateDbContext();
        var clinic = new Clinic
        {
            ClinicId = Guid.NewGuid(),
            Name = "Activation Test Clinic",
            CreatedBySuperAdminUserId = Guid.NewGuid()
        };
        context.Clinics.Add(clinic);
        await context.SaveChangesAsync();

        var service = new ClinicsService(context);
        Assert.IsType<OkObjectResult>(await service.DeactivateAsync(clinic.ClinicId));
        Assert.False(clinic.IsActive);
        Assert.Equal(1, await context.Clinics.CountAsync());

        Assert.IsType<OkObjectResult>(await service.ActivateAsync(clinic.ClinicId));
        Assert.True(clinic.IsActive);
        Assert.Equal(1, await context.Clinics.CountAsync());
    }

    [Fact]
    public async Task AppointmentDeactivateAndActivate_PreserveTheAppointmentRecord()
    {
        using var context = new AppointmentFilteringTestContext();
        var clinic = context.AddClinic();
        var doctor = context.AddDoctor(clinic.ClinicId);
        var patient = context.AddPatient(clinic.ClinicId, "activation-patient");
        var appointment = context.AddAppointment(
            doctor.DoctorId,
            patient.PatientId,
            new DateOnly(2026, 7, 1),
            new TimeOnly(10, 0));
        await context.DbContext.SaveChangesAsync();
        var principal = AppointmentFilteringTestContext.Principal(Guid.NewGuid(), HSCSAPI.Models.Enums.UserSystemRole.SuperAdmin);

        Assert.IsType<NoContentResult>(await context.Service.DeactivateAsync(appointment.AppointmentId, principal));
        Assert.False(appointment.IsActive);
        Assert.Equal(1, await context.DbContext.Appointments.CountAsync());

        Assert.IsType<NoContentResult>(await context.Service.ActivateAsync(appointment.AppointmentId, principal));
        Assert.True(appointment.IsActive);
        Assert.Equal(1, await context.DbContext.Appointments.CountAsync());
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
