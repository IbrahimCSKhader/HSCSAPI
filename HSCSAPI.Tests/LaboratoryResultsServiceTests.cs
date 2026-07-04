using System.Security.Claims;
using System.Text.Json;
using HSCSAPI.Controllers;
using HSCSAPI.Data;
using HSCSAPI.DTOs.Laboratory;
using HSCSAPI.Models.Clinics;
using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Identity;
using HSCSAPI.Models.Laboratory;
using HSCSAPI.Models.Profiles;
using HSCSAPI.Services.Laboratory;
using HSCSAPI.Services.Standards;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Tests;

public class LaboratoryResultsServiceTests
{
    [Fact]
    public async Task TemplateSeeder_SeedsTenDocumentedTemplates_Idempotently()
    {
        using var context = new LaboratoryResultTestContext();

        await context.SeedTemplatesAsync();
        await context.SeedTemplatesAsync();

        var templates = await context.DbContext.LabTestTemplates
            .Include(x => x.Fields)
            .OrderBy(x => x.Code)
            .ToListAsync();

        Assert.Equal(10, templates.Count);
        Assert.Equal(10, templates.Select(x => x.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(templates, template =>
        {
            Assert.NotEmpty(template.Fields);
            Assert.StartsWith("https://", template.SourceUrl);
            Assert.Equal(template.Fields.Count, template.Fields.Select(x => x.DisplayOrder).Distinct().Count());
        });

        var cbc = Assert.Single(templates, x => x.Code == "CBC-DIFF");
        Assert.Equal(14, cbc.Fields.Count);
        Assert.Contains(cbc.Fields, x => x.Code == "hemoglobin" && x.Unit == "g/dL");

        var cmp = Assert.Single(templates, x => x.Code == "CMP");
        Assert.Equal(14, cmp.Fields.Count);
        Assert.Contains(cmp.Fields, x => x.Code == "creatinine");

        var urinalysis = Assert.Single(templates, x => x.Code == "URINALYSIS");
        Assert.Equal("24356-8", urinalysis.LoincCode);
        Assert.Contains(urinalysis.Fields, x => x.Code == "nitrite" && x.ValueType == LabResultValueType.Choice);

        var stool = Assert.Single(templates, x => x.Code == "STOOL-ANALYSIS");
        Assert.Contains(stool.Fields, x => x.Code == "undigested_fibers");
    }

    [Fact]
    public async Task GetTemplates_ReturnsOrderedFieldsAndAllowedChoices()
    {
        using var context = new LaboratoryResultTestContext();
        await context.SeedTemplatesAsync();

        var allAction = await context.Service.GetTemplatesAsync(activeOnly: true);
        var all = Ok<List<LabTestTemplateResponse>>(allAction);
        Assert.Equal(10, all.Count);

        var urineAction = await context.Service.GetTemplateAsync("urinalysis");
        var urine = Ok<LabTestTemplateResponse>(urineAction);
        Assert.Equal("Complete Urinalysis", urine.Name);
        Assert.Equal(Enumerable.Range(1, urine.Fields.Count), urine.Fields.Select(x => x.DisplayOrder));
        var nitrite = Assert.Single(urine.Fields, x => x.Code == "nitrite");
        Assert.Equal(["Negative", "Positive"], nitrite.AllowedValues);

        var missing = await context.Service.GetTemplateAsync("missing");
        Assert.IsType<NotFoundObjectResult>(missing.Result);
    }

    [Fact]
    public async Task WorkItems_AreScopedToAssignedTechnologistOrUnassignedClinic()
    {
        using var context = new LaboratoryResultTestContext();
        await context.SeedTemplatesAsync();
        var clinic = context.AddClinic("Central Lab");
        var otherClinic = context.AddClinic("Other Lab");
        var tech = context.AddTechnologist(clinic.ClinicId, "Tech One");
        var otherTech = context.AddTechnologist(clinic.ClinicId, "Tech Two");
        var foreignTech = context.AddTechnologist(otherClinic.ClinicId, "Foreign Tech");
        var doctor = context.AddDoctor(clinic.ClinicId, "Dr. Noor");
        var patient = context.AddPatient(clinic.ClinicId, "P-100", "Mona Ali");

        context.AddRequest(doctor.DoctorId, patient.PatientId, clinic.ClinicId, "4548-4", tech.LaboratoryTechnologistId);
        context.AddRequest(doctor.DoctorId, patient.PatientId, clinic.ClinicId, "24348-5", null);
        context.AddRequest(doctor.DoctorId, patient.PatientId, clinic.ClinicId, "57021-8", otherTech.LaboratoryTechnologistId);
        context.AddRequest(doctor.DoctorId, patient.PatientId, otherClinic.ClinicId, "24323-8", foreignTech.LaboratoryTechnologistId);
        await context.DbContext.SaveChangesAsync();

        var action = await context.Service.GetMyWorkItemsAsync(
            "pending",
            1,
            20,
            LaboratoryResultTestContext.Principal(tech.LaboratoryTechnologistId));
        var response = Ok<LabWorkItemsResponse>(action);

        Assert.Equal(2, response.TotalCount);
        Assert.All(response.Items, x => Assert.Equal("Pending", x.Status));
        Assert.Contains(response.Items, x => x.SuggestedTemplateCode == "HBA1C");
        Assert.Contains(response.Items, x => x.SuggestedTemplateCode == "THYROID-FT4-TSH");

        var invalidStatus = await context.Service.GetMyWorkItemsAsync(
            "unknown",
            1,
            20,
            LaboratoryResultTestContext.Principal(tech.LaboratoryTechnologistId));
        Assert.IsType<BadRequestObjectResult>(invalidStatus.Result);
    }

    [Fact]
    public async Task CreateResult_StoresTypedValuesSnapshotsAndAssignsUnassignedRequest()
    {
        using var context = new LaboratoryResultTestContext();
        await context.SeedTemplatesAsync();
        var setup = context.AddBasicSetup("4548-4", assigned: false);
        await context.DbContext.SaveChangesAsync();

        var request = await context.ValidResultRequestAsync("HBA1C", "ACC-1001");
        request.Values[0].Value = "6.2";
        request.Values[0].Flag = "High";
        request.Values[0].ReferenceRange = "Laboratory adult range";

        var action = await context.Service.CreateResultAsync(
            setup.Request.LabTestRequestId,
            request,
            LaboratoryResultTestContext.Principal(setup.Technologist.LaboratoryTechnologistId));
        var response = Ok<LabTestResultResponse>(action);

        Assert.Equal("HBA1C", response.TemplateCode);
        Assert.Equal("6.2", Assert.Single(response.Values).Value);
        Assert.Equal("High", response.Values[0].Flag);

        var stored = await context.DbContext.LabTestResults
            .Include(x => x.Values)
            .SingleAsync();
        Assert.Equal(6.2m, Assert.Single(stored.Values).NumericValue);
        Assert.Equal("Hemoglobin A1c", stored.Values.Single().FieldLabel);
        Assert.Equal(setup.Technologist.LaboratoryTechnologistId, stored.LaboratoryTechnologistId);
        Assert.Equal(
            setup.Technologist.LaboratoryTechnologistId,
            (await context.DbContext.LabTestRequests.FindAsync(setup.Request.LabTestRequestId))!.LaboratoryTechnologistId);
    }

    [Fact]
    public async Task CreateResult_RejectsMalformedFieldsTimesAndTemplateMismatch()
    {
        using var context = new LaboratoryResultTestContext();
        await context.SeedTemplatesAsync();
        var setup = context.AddBasicSetup("4548-4", assigned: true);
        await context.DbContext.SaveChangesAsync();
        var principal = LaboratoryResultTestContext.Principal(setup.Technologist.LaboratoryTechnologistId);

        var missing = await context.ValidResultRequestAsync("HBA1C", "ACC-2001");
        missing.Values.Clear();
        var missingAction = await context.Service.CreateResultAsync(setup.Request.LabTestRequestId, missing, principal);
        AssertValidationError(missingAction, "Missing required fields");

        var unknown = await context.ValidResultRequestAsync("HBA1C", "ACC-2002");
        unknown.Values.Add(new CreateLabTestResultValueRequest { FieldCode = "made_up", Value = "1" });
        var unknownAction = await context.Service.CreateResultAsync(setup.Request.LabTestRequestId, unknown, principal);
        AssertValidationError(unknownAction, "Unknown fields");

        var duplicate = await context.ValidResultRequestAsync("HBA1C", "ACC-2003");
        duplicate.Values.Add(new CreateLabTestResultValueRequest { FieldCode = "HBA1C", Value = "5.1" });
        var duplicateAction = await context.Service.CreateResultAsync(setup.Request.LabTestRequestId, duplicate, principal);
        AssertValidationError(duplicateAction, "Duplicate fields");

        var invalidNumber = await context.ValidResultRequestAsync("HBA1C", "ACC-2004");
        invalidNumber.Values[0].Value = "not-a-number";
        var numberAction = await context.Service.CreateResultAsync(setup.Request.LabTestRequestId, invalidNumber, principal);
        AssertValidationError(numberAction, "decimal number");

        var excessDecimals = await context.ValidResultRequestAsync("HBA1C", "ACC-2005");
        excessDecimals.Values[0].Value = "5.123";
        var decimalAction = await context.Service.CreateResultAsync(setup.Request.LabTestRequestId, excessDecimals, principal);
        AssertValidationError(decimalAction, "decimal places");

        var future = await context.ValidResultRequestAsync("HBA1C", "ACC-2006");
        future.ReceivedAt = DateTime.UtcNow.AddHours(1);
        var futureAction = await context.Service.CreateResultAsync(setup.Request.LabTestRequestId, future, principal);
        Assert.IsType<BadRequestObjectResult>(futureAction.Result);

        var mismatch = await context.ValidResultRequestAsync("CMP", "ACC-2007");
        var mismatchAction = await context.Service.CreateResultAsync(setup.Request.LabTestRequestId, mismatch, principal);
        var mismatchBadRequest = Assert.IsType<BadRequestObjectResult>(mismatchAction.Result);
        Assert.Contains("does not match", mismatchBadRequest.Value?.ToString());

        Assert.Empty(context.DbContext.LabTestResults);
    }

    [Fact]
    public async Task CreateResult_ValidatesChoicesFlagsAccessionAndSpecimenCondition()
    {
        using var context = new LaboratoryResultTestContext();
        await context.SeedTemplatesAsync();
        var setup = context.AddBasicSetup("24356-8", assigned: true);
        await context.DbContext.SaveChangesAsync();
        var principal = LaboratoryResultTestContext.Principal(setup.Technologist.LaboratoryTechnologistId);

        var choice = await context.ValidResultRequestAsync("URINALYSIS", "ACC-3001");
        choice.Values.Single(x => x.FieldCode == "nitrite").Value = "Maybe";
        var choiceAction = await context.Service.CreateResultAsync(setup.Request.LabTestRequestId, choice, principal);
        AssertValidationError(choiceAction, "must be one of");

        var flag = await context.ValidResultRequestAsync("URINALYSIS", "ACC-3002");
        flag.Values[0].Flag = "Unexpected";
        var flagAction = await context.Service.CreateResultAsync(setup.Request.LabTestRequestId, flag, principal);
        AssertValidationError(flagAction, "invalid flag");

        var accession = await context.ValidResultRequestAsync("URINALYSIS", "!!");
        var accessionAction = await context.Service.CreateResultAsync(setup.Request.LabTestRequestId, accession, principal);
        Assert.IsType<BadRequestObjectResult>(accessionAction.Result);

        var specimen = await context.ValidResultRequestAsync("URINALYSIS", "ACC-3004");
        specimen.SpecimenCondition = "Destroyed";
        var specimenAction = await context.Service.CreateResultAsync(setup.Request.LabTestRequestId, specimen, principal);
        Assert.IsType<BadRequestObjectResult>(specimenAction.Result);
    }

    [Fact]
    public async Task Results_EnforceAuthorizationAndUniqueness()
    {
        using var context = new LaboratoryResultTestContext();
        await context.SeedTemplatesAsync();
        var setup = context.AddBasicSetup("4548-4", assigned: true);
        var other = context.AddTechnologist(setup.Clinic.ClinicId, "Other Tech");
        await context.DbContext.SaveChangesAsync();

        var valid = await context.ValidResultRequestAsync("HBA1C", "ACC-4001");
        var createdAction = await context.Service.CreateResultAsync(
            setup.Request.LabTestRequestId,
            valid,
            LaboratoryResultTestContext.Principal(setup.Technologist.LaboratoryTechnologistId));
        var created = Ok<LabTestResultResponse>(createdAction);

        var duplicateResult = await context.Service.CreateResultAsync(
            setup.Request.LabTestRequestId,
            await context.ValidResultRequestAsync("HBA1C", "ACC-4002"),
            LaboratoryResultTestContext.Principal(setup.Technologist.LaboratoryTechnologistId));
        Assert.IsType<ConflictObjectResult>(duplicateResult.Result);

        var secondRequest = context.AddRequest(
            setup.Doctor.DoctorId,
            setup.Patient.PatientId,
            setup.Clinic.ClinicId,
            "4548-4",
            setup.Technologist.LaboratoryTechnologistId);
        await context.DbContext.SaveChangesAsync();
        var duplicateAccession = await context.Service.CreateResultAsync(
            secondRequest.LabTestRequestId,
            await context.ValidResultRequestAsync("HBA1C", "ACC-4001"),
            LaboratoryResultTestContext.Principal(setup.Technologist.LaboratoryTechnologistId));
        Assert.IsType<ConflictObjectResult>(duplicateAccession.Result);

        var forbiddenAsNotFound = await context.Service.GetResultAsync(
            created.LabTestResultId,
            LaboratoryResultTestContext.Principal(other.LaboratoryTechnologistId));
        Assert.IsType<NotFoundObjectResult>(forbiddenAsNotFound.Result);

        var invalidToken = await context.Service.GetResultAsync(created.LabTestResultId, new ClaimsPrincipal());
        Assert.IsType<UnauthorizedObjectResult>(invalidToken.Result);
    }

    [Fact]
    public async Task GeneratePdf_WritesProtectedValidPdfAndDoctorCanDownloadIt()
    {
        using var context = new LaboratoryResultTestContext();
        await context.SeedTemplatesAsync();
        var setup = context.AddBasicSetup("24348-5", assigned: true);
        await context.DbContext.SaveChangesAsync();
        var techPrincipal = LaboratoryResultTestContext.Principal(setup.Technologist.LaboratoryTechnologistId);

        var resultRequest = await context.ValidResultRequestAsync("THYROID-FT4-TSH", "ACC-5001");
        resultRequest.Values.Single(x => x.FieldCode == "tsh").Value = "2.125";
        resultRequest.Values.Single(x => x.FieldCode == "free_t4").Value = "1.20";
        var created = Ok<LabTestResultResponse>(await context.Service.CreateResultAsync(
            setup.Request.LabTestRequestId,
            resultRequest,
            techPrincipal));

        var beforeGeneration = await context.Service.DownloadPdfAsync(created.LabTestResultId, techPrincipal);
        Assert.IsType<BadRequestObjectResult>(beforeGeneration);

        var generated = Ok<LabResultPdfResponse>(await context.Service.GeneratePdfAsync(
            created.LabTestResultId,
            techPrincipal));

        Assert.EndsWith(".pdf", generated.FileName, StringComparison.OrdinalIgnoreCase);
        Assert.True(generated.FileSizeInBytes > 1000);
        Assert.Equal(64, generated.Sha256Checksum.Length);

        var stored = await context.DbContext.LabTestResults.SingleAsync();
        var physicalPath = Path.Combine(context.ContentRootPath, stored.PdfFilePath!.Replace('/', Path.DirectorySeparatorChar));
        var bytes = await File.ReadAllBytesAsync(physicalPath);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));

        var labDownload = await context.Service.DownloadPdfAsync(created.LabTestResultId, techPrincipal);
        var labFile = Assert.IsType<PhysicalFileResult>(labDownload);
        Assert.Equal("application/pdf", labFile.ContentType);
        Assert.True(labFile.EnableRangeProcessing);

        var standards = new StandardsService(context.DbContext, new TestWebHostEnvironment(context.ContentRootPath));
        var doctorService = new LabTestRequestsService(
            context.DbContext,
            standards,
            new TestWebHostEnvironment(context.ContentRootPath));
        var doctorDownload = await doctorService.DownloadMyResultFileAsync(
            setup.Request.LabTestRequestId,
            LaboratoryResultTestContext.Principal(setup.Doctor.DoctorId));
        Assert.IsType<PhysicalFileResult>(doctorDownload);

        var regenerated = Ok<LabResultPdfResponse>(await context.Service.GeneratePdfAsync(
            created.LabTestResultId,
            techPrincipal));
        Assert.Equal(generated.FileName, regenerated.FileName);
        Assert.True(File.Exists(physicalPath));
    }

    [Fact]
    public void LaboratoryTestsController_HasExpectedUniqueRoutes()
    {
        var controllerType = typeof(LaboratoryTestsController);
        var controllerRoute = controllerType.GetCustomAttributes(typeof(RouteAttribute), true)
            .Cast<RouteAttribute>()
            .Single()
            .Template;
        var routes = controllerType.GetMethods()
            .SelectMany(method => method.GetCustomAttributes(true)
                .OfType<HttpMethodAttribute>()
                .Select(attribute => $"{string.Join(',', attribute.HttpMethods)} {controllerRoute}/{attribute.Template}"))
            .ToList();

        Assert.Equal(8, routes.Count);
        Assert.Equal(routes.Count, routes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains("GET api/[controller]/templates", routes);
        Assert.Contains("POST api/[controller]/my-requests/{labTestRequestId:guid}/results", routes);
        Assert.Contains("GET api/[controller]/my-requests/{labTestRequestId:guid}", routes);
        Assert.Contains("POST api/[controller]/results/{labTestResultId:guid}/pdf", routes);
        Assert.Contains("GET api/[controller]/results/{labTestResultId:guid}/pdf", routes);
    }

    private static T Ok<T>(ActionResult<T> action)
    {
        var ok = Assert.IsType<OkObjectResult>(action.Result);
        return Assert.IsType<T>(ok.Value);
    }

    private static void AssertValidationError(ActionResult<LabTestResultResponse> action, string expected)
    {
        var badRequest = Assert.IsType<BadRequestObjectResult>(action.Result);
        var json = JsonSerializer.Serialize(badRequest.Value);
        Assert.Contains(expected, json, StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class LaboratoryResultTestContext : IDisposable
{
    public LaboratoryResultTestContext()
    {
        ContentRootPath = Path.Combine(Path.GetTempPath(), "hscsapi-lab-result-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ContentRootPath);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        DbContext = new AppDbContext(options);
        DbContext.Database.EnsureCreated();
        Environment = new TestWebHostEnvironment(ContentRootPath);
        Service = new LaboratoryResultsService(DbContext, new LabResultPdfGenerator(), Environment);
    }

    public string ContentRootPath { get; }
    public AppDbContext DbContext { get; }
    public TestWebHostEnvironment Environment { get; }
    public LaboratoryResultsService Service { get; }

    public Task SeedTemplatesAsync() => new LabTestTemplateSeeder(DbContext).SeedAsync();

    public Clinic AddClinic(string name)
    {
        var clinic = new Clinic
        {
            ClinicId = Guid.NewGuid(),
            Name = name,
            CreatedBySuperAdminUserId = Guid.NewGuid()
        };
        DbContext.Clinics.Add(clinic);
        return clinic;
    }

    public LaboratoryTechnologist AddTechnologist(Guid clinicId, string name)
    {
        var user = AddUser(clinicId, name);
        var technologist = new LaboratoryTechnologist
        {
            LaboratoryTechnologistId = user.Id,
            ProfessionalLicenseNumber = $"LAB-{Guid.NewGuid():N}",
            User = user
        };
        DbContext.LaboratoryTechnologists.Add(technologist);
        return technologist;
    }

    public Doctor AddDoctor(Guid clinicId, string name)
    {
        var user = AddUser(clinicId, name);
        var doctor = new Doctor
        {
            DoctorId = user.Id,
            ProfessionalLicenseNumber = $"DOC-{Guid.NewGuid():N}",
            User = user
        };
        DbContext.Doctors.Add(doctor);
        return doctor;
    }

    public Patient AddPatient(Guid clinicId, string userId, string name)
    {
        var user = AddUser(clinicId, name);
        user.DateOfBirth = new DateOnly(1992, 4, 18);
        var patient = new Patient
        {
            PatientId = user.Id,
            UserID = userId,
            Gender = Gender.Female,
            BloodType = BloodType.APositive,
            User = user
        };
        DbContext.Patients.Add(patient);
        return patient;
    }

    public LabTestRequest AddRequest(
        Guid doctorId,
        Guid patientId,
        Guid clinicId,
        string loincCode,
        Guid? technologistId)
    {
        var request = new LabTestRequest
        {
            LabTestRequestId = Guid.NewGuid(),
            TestName = $"Test {loincCode}",
            PatientId = patientId,
            RequestedByDoctorId = doctorId,
            TestingClinicId = clinicId,
            LoincCode = loincCode,
            Priority = "Routine",
            RequestedAt = DateTime.UtcNow.AddHours(-3),
            LaboratoryTechnologistId = technologistId
        };
        DbContext.LabTestRequests.Add(request);
        return request;
    }

    public BasicSetup AddBasicSetup(string loincCode, bool assigned)
    {
        var clinic = AddClinic("Main Laboratory");
        var technologist = AddTechnologist(clinic.ClinicId, "Lina Lab");
        var doctor = AddDoctor(clinic.ClinicId, "Dr. Sami");
        var patient = AddPatient(clinic.ClinicId, "P-9001", "Rana Saleh");
        var request = AddRequest(
            doctor.DoctorId,
            patient.PatientId,
            clinic.ClinicId,
            loincCode,
            assigned ? technologist.LaboratoryTechnologistId : null);
        return new(clinic, technologist, doctor, patient, request);
    }

    public async Task<CreateLabTestResultRequest> ValidResultRequestAsync(string templateCode, string accession)
    {
        var template = await DbContext.LabTestTemplates
            .AsNoTracking()
            .Include(x => x.Fields)
            .SingleAsync(x => x.Code == templateCode);

        return new CreateLabTestResultRequest
        {
            TemplateCode = template.Code,
            AccessionNumber = accession,
            CollectedAt = DateTime.UtcNow.AddHours(-2),
            ReceivedAt = DateTime.UtcNow.AddHours(-1),
            SpecimenCondition = "Accepted",
            Comments = "Verified laboratory result.",
            Values = template.Fields
                .Where(x => x.IsRequired)
                .OrderBy(x => x.DisplayOrder)
                .Select(x => new CreateLabTestResultValueRequest
                {
                    FieldCode = x.Code,
                    Value = x.ValueType switch
                    {
                        LabResultValueType.Numeric => "1",
                        LabResultValueType.Choice => JsonSerializer.Deserialize<List<string>>(x.AllowedValuesJson!)![0],
                        _ => "None seen"
                    },
                    Flag = "Normal"
                })
                .ToList()
        };
    }

    public static ClaimsPrincipal Principal(Guid userId) => new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, nameof(UserSystemRole.LaboratoryTechnologist))
        ],
        "Test"));

    public void Dispose()
    {
        DbContext.Dispose();
        if (Directory.Exists(ContentRootPath))
        {
            Directory.Delete(ContentRootPath, recursive: true);
        }
    }

    private User AddUser(Guid clinicId, string name)
    {
        var id = Guid.NewGuid();
        var email = $"{id:N}@test.local";
        var user = new User
        {
            Id = id,
            Name = name,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            RegisteredAt = DateTime.UtcNow,
            ClinicId = clinicId
        };
        DbContext.Users.Add(user);
        return user;
    }

    public sealed record BasicSetup(
        Clinic Clinic,
        LaboratoryTechnologist Technologist,
        Doctor Doctor,
        Patient Patient,
        LabTestRequest Request);
}
