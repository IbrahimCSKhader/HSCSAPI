using System.Reflection;
using HSCSAPI.Controllers;
using HSCSAPI.Data;
using HSCSAPI.DTOs.Appointment;
using HSCSAPI.DTOs.Clinic;
using HSCSAPI.DTOs.Doctor;
using HSCSAPI.DTOs.MedicalFiles;
using HSCSAPI.DTOs.Notifications;
using HSCSAPI.DTOs.Secretary;
using HSCSAPI.Models.Clinics;
using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Identity;
using HSCSAPI.Models.Profiles;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Tests;

public class RequestedEndpointBatch5Tests
{
    [Fact]
    public void TreatmentsCatalogEndpoints_AreGetRoutes()
    {
        AssertRoute<TreatmentsController>(nameof(TreatmentsController.GetByClinicRoute), "GET", "Clinics/{clinicId:guid}/treatments");
        AssertRoute<TreatmentsController>(nameof(TreatmentsController.GetByClinicQuery), "GET", "Treatments");
    }

    [Fact]
    public void SecretarySelfAndReportEndpoints_AreDocumentedRoutes()
    {
        AssertRoute<SecretariesController>(nameof(SecretariesController.GetMyProfile), "GET", "me");
        AssertRoute<SecretariesController>(nameof(SecretariesController.UpdateMyProfile), "PUT", "me");
        AssertRoute<SecretariesController>(nameof(SecretariesController.ChangeMyPassword), "PUT", "me/password");
        AssertRoute<SecretariesController>(nameof(SecretariesController.GetReportOptions), "GET", "my-clinic/reports/options");
        AssertRoute<SecretariesController>(nameof(SecretariesController.DeleteReport), "DELETE", "my-clinic/reports/{reportId:guid}");
    }

    [Fact]
    public void DoctorSpecialtiesEndpoint_IsGetRoute()
    {
        AssertRoute<DoctorsController>(nameof(DoctorsController.GetSpecialties), "GET", "specialties");
    }

    [Fact]
    public void RequestedContractDtos_ExposeFrontendFields()
    {
        AssertProperty<AppointmentResponse>(nameof(AppointmentResponse.DoctorSpecialty));
        AssertProperty<AppointmentResponse>(nameof(AppointmentResponse.AppointmentEndTime));
        AssertProperty<AppointmentResponse>(nameof(AppointmentResponse.TreatmentId));
        AssertProperty<AppointmentResponse>(nameof(AppointmentResponse.TreatmentName));

        AssertProperty<CreateAppointmentRequest>(nameof(CreateAppointmentRequest.TreatmentId));
        AssertProperty<UpdateAppointmentRequest>(nameof(UpdateAppointmentRequest.TreatmentName));
        AssertProperty<NotificationResponse>(nameof(NotificationResponse.ActionPath));
        AssertProperty<UpdateMyDoctorProfileRequest>(nameof(UpdateMyDoctorProfileRequest.Email));
        AssertProperty<CreateMedicalFileUploadRequest>(nameof(CreateMedicalFileUploadRequest.DiagnosisCode));
        AssertProperty<CreateMedicalFileUploadRequest>(nameof(CreateMedicalFileUploadRequest.ActivityCode));
        AssertProperty<MedicalFileUploadResponse>(nameof(MedicalFileUploadResponse.DiagnosisName));
        AssertProperty<MedicalFileUploadResponse>(nameof(MedicalFileUploadResponse.ActivityName));
        AssertProperty<GenerateSecretaryReportRequest>(nameof(GenerateSecretaryReportRequest.Period));
        AssertProperty<GenerateSecretaryReportRequest>(nameof(GenerateSecretaryReportRequest.FromDate));
        AssertProperty<SecretaryReportResponse>(nameof(SecretaryReportResponse.ToDate));
    }

    [Fact]
    public async Task TreatmentsCatalog_ReturnsActiveDoctorSpecialtiesForClinic()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var dbContext = new AppDbContext(options);
        var clinic = new Clinic
        {
            ClinicId = Guid.NewGuid(),
            Name = "Central Clinic",
            CreatedBySuperAdminUserId = Guid.NewGuid(),
            IsActive = true
        };
        dbContext.Clinics.Add(clinic);
        AddDoctor(dbContext, clinic.ClinicId, DoctorSpecialty.Cardiology, isActive: true);
        AddDoctor(dbContext, clinic.ClinicId, DoctorSpecialty.Cardiology, isActive: true);
        AddDoctor(dbContext, clinic.ClinicId, DoctorSpecialty.Neurology, isActive: false);
        await dbContext.SaveChangesAsync();

        var controller = new TreatmentsController(dbContext);
        var response = await controller.GetByClinicRoute(clinic.ClinicId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var treatments = Assert.IsType<List<TreatmentCatalogResponse>>(ok.Value);
        var treatment = Assert.Single(treatments);
        Assert.Equal("Cardiology", treatment.TreatmentId);
        Assert.Equal(2, treatment.ActiveDoctorsCount);
    }

    private static void AssertRoute<T>(string methodName, string verb, string template)
    {
        var method = typeof(T).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        var route = Assert.Single(method!.GetCustomAttributes<HttpMethodAttribute>(true));
        Assert.Contains(verb, route.HttpMethods);
        Assert.Equal(template, route.Template);
    }

    private static void AssertProperty<T>(string propertyName)
    {
        Assert.NotNull(typeof(T).GetProperty(propertyName));
    }

    private static void AddDoctor(AppDbContext dbContext, Guid clinicId, DoctorSpecialty specialty, bool isActive)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = $"{specialty} doctor",
            UserName = $"{Guid.NewGuid():N}@test.local",
            NormalizedUserName = $"{Guid.NewGuid():N}@TEST.LOCAL",
            Email = $"{Guid.NewGuid():N}@test.local",
            NormalizedEmail = $"{Guid.NewGuid():N}@TEST.LOCAL",
            EmailConfirmed = true,
            RegisteredAt = DateTime.UtcNow,
            ClinicId = clinicId,
            IsActive = isActive
        };

        dbContext.Users.Add(user);
        dbContext.Doctors.Add(new Doctor
        {
            DoctorId = user.Id,
            ProfessionalLicenseNumber = $"DOC-{Guid.NewGuid():N}",
            Specialty = specialty,
            User = user
        });
    }
}
