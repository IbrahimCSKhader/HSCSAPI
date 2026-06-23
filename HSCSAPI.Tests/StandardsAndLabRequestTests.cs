using System.Net;
using System.Security.Claims;
using System.Text;
using HSCSAPI.Data;
using HSCSAPI.DTOs.Laboratory;
using HSCSAPI.Models.Clinics;
using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Identity;
using HSCSAPI.Models.Profiles;
using HSCSAPI.Models.Standards;
using HSCSAPI.Services.Laboratory;
using HSCSAPI.Services.Standards;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Tests;

public class StandardsAndLabRequestTests
{
    [Fact]
    public async Task StandardsService_SearchesLocalStandardsFiles()
    {
        using var context = new StandardsTestContext();
        context.WriteStandardsFiles();
        var service = new StandardsService(context.DbContext, new TestWebHostEnvironment(context.ContentRootPath));

        var loinc = await service.SearchLoincAsync("lipid", page: 1, pageSize: 10);
        var icd10 = await service.SearchIcd10Async("cholera", page: 1, pageSize: 10);
        var radiology = await service.SearchRadiologyPlaybookAsync("abdomen", page: 1, pageSize: 10);

        Assert.Equal("24331-1", Assert.Single(loinc.Items).Code);
        Assert.Equal("A00", Assert.Single(icd10.Items).Code);
        Assert.Equal("RPID2", Assert.Single(radiology.Items).Rpid);
    }

    [Fact]
    public async Task StandardsService_PrefersLoincDatabaseTableWhenAvailable()
    {
        using var context = new StandardsTestContext();
        context.DbContext.LoincCodes.Add(new LoincCode
        {
            Code = "777-3",
            LongCommonName = "Platelets [#/volume] in Blood",
            ShortName = "Platelets Bld",
            Status = "ACTIVE"
        });
        await context.DbContext.SaveChangesAsync();

        var service = new StandardsService(context.DbContext, new TestWebHostEnvironment(context.ContentRootPath));

        var loinc = await service.GetLoincByCodeAsync("777-3");

        Assert.NotNull(loinc);
        Assert.Equal("Platelets [#/volume] in Blood", loinc.Display);
    }

    [Fact]
    public async Task RxNormService_UsesRxNavJsonEndpoints()
    {
        var handler = new RecordingHttpMessageHandler("""{"idGroup":{"rxnormId":["161"]}}""");
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://rxnav.nlm.nih.gov/REST/")
        };
        var service = new RxNormService(httpClient);

        var json = await service.FindRxcuiByNameAsync("aspirin");

        Assert.Equal("161", json.GetProperty("idGroup").GetProperty("rxnormId")[0].GetString());
        Assert.Equal("https://rxnav.nlm.nih.gov/REST/rxcui.json?name=aspirin", handler.LastRequestUri?.ToString());
    }

    [Fact]
    public async Task CreateMyRequestAsync_StoresLoincBackedLabRequest()
    {
        using var context = new LabRequestTestContext();
        var clinic = context.AddClinic("SHCS Main Clinic");
        var doctor = context.AddDoctor(clinic.ClinicId, "Dr. Lina Haddad");
        var patient = context.AddPatient(clinic.ClinicId, "pat-001", "Sarah Al-Hassan");
        var technologist = context.AddLaboratoryTechnologist(clinic.ClinicId, "Rana Lab");
        context.AddLoinc("58410-2", "CBC panel - Blood by Automated count");
        await context.DbContext.SaveChangesAsync();

        var response = await context.Service.CreateMyRequestAsync(
            new CreateLabTestRequestRequest
            {
                PatientId = patient.UserID,
                TestingClinicId = clinic.ClinicId,
                LoincCode = "58410-2",
                Priority = "Urgent",
                ClinicalNotes = "Rule out iron deficiency."
            },
            LabRequestTestContext.Principal(doctor.DoctorId),
            CancellationToken.None);

        var created = OkValue(response);
        Assert.Equal("58410-2", created.LoincCode);
        Assert.Equal("CBC panel - Blood by Automated count", created.TestName);
        Assert.Equal("Urgent", created.Priority);
        Assert.Equal("Pending", created.Status);
        Assert.Equal(patient.PatientId, created.PatientId);
        Assert.Equal(technologist.LaboratoryTechnologistId, created.LaboratoryTechnologistId);

        var stored = await context.DbContext.LabTestRequests.SingleAsync();
        Assert.Equal("58410-2", stored.LoincCode);
        Assert.Equal(doctor.DoctorId, stored.RequestedByDoctorId);
        Assert.Equal(clinic.ClinicId, stored.TestingClinicId);
    }

    [Fact]
    public async Task CreateMyRequestAsync_RejectsUnknownLoincCode()
    {
        using var context = new LabRequestTestContext();
        var clinic = context.AddClinic("SHCS Main Clinic");
        var doctor = context.AddDoctor(clinic.ClinicId, "Dr. Lina Haddad");
        var patient = context.AddPatient(clinic.ClinicId, "pat-001", "Sarah Al-Hassan");
        await context.DbContext.SaveChangesAsync();

        var response = await context.Service.CreateMyRequestAsync(
            new CreateLabTestRequestRequest
            {
                PatientId = patient.UserID,
                TestingClinicId = clinic.ClinicId,
                LoincCode = "missing-code",
                Priority = "Routine"
            },
            LabRequestTestContext.Principal(doctor.DoctorId),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Empty(context.DbContext.LabTestRequests);
    }

    private static T OkValue<T>(ActionResult<T> response)
    {
        var ok = Assert.IsType<OkObjectResult>(response.Result);
        return Assert.IsType<T>(ok.Value);
    }
}

internal sealed class StandardsTestContext : IDisposable
{
    public StandardsTestContext()
    {
        ContentRootPath = Path.Combine(Path.GetTempPath(), "hscsapi-standards-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(ContentRootPath, "Files"));

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        DbContext = new AppDbContext(options);
        DbContext.Database.EnsureCreated();
    }

    public string ContentRootPath { get; }
    public AppDbContext DbContext { get; }

    public void WriteStandardsFiles()
    {
        File.WriteAllText(
            Path.Combine(ContentRootPath, "Files", "LoincTableCore.csv"),
            """
            "LOINC_NUM","COMPONENT","PROPERTY","TIME_ASPCT","SYSTEM","SCALE_TYP","METHOD_TYP","CLASS","CLASSTYPE","LONG_COMMON_NAME","SHORTNAME","EXTERNAL_COPYRIGHT_NOTICE","STATUS","VersionFirstReleased","VersionLastChanged"
            "24331-1","Lipid 1996 panel","-","Pt","Ser/Plas","Qn","","PANEL.CHEM","1","Lipid 1996 panel - Serum or Plasma","Lipid 1996 Pnl SerPl","","ACTIVE","1.0o","2.73"
            """,
            Encoding.UTF8);

        File.WriteAllText(
            Path.Combine(ContentRootPath, "Files", "ICD 10.csv"),
            "A00,\"Cholera\"" + Environment.NewLine,
            Encoding.UTF8);

        File.WriteAllText(
            Path.Combine(ContentRootPath, "Files", "core-playbook-dev.csv"),
            """
            RPID,LETTER_CODE,SHORT_NAME,LONG_NAME,MODALITY,PLAYBOOK_TYPE,POPULATION,BODY_REGION,LATERALITY,REASON_FOR_EXAM,RIDS
            RPID2,CTABCA,"CT Abd Angio w/wo","CT Abdomen Angio w and wo IV Contrast",CT,"RADIOLOGY ORDERABLE"," ",ABDOMEN," "," ","RID10321"
            """,
            Encoding.UTF8);
    }

    public void Dispose()
    {
        DbContext.Dispose();
        if (Directory.Exists(ContentRootPath))
        {
            Directory.Delete(ContentRootPath, recursive: true);
        }
    }
}

internal sealed class LabRequestTestContext : IDisposable
{
    public LabRequestTestContext()
    {
        ContentRootPath = Path.Combine(Path.GetTempPath(), "hscsapi-lab-request-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ContentRootPath);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        DbContext = new AppDbContext(options);
        DbContext.Database.EnsureCreated();
        StandardsService = new StandardsService(DbContext, new TestWebHostEnvironment(ContentRootPath));
        Service = new LabTestRequestsService(DbContext, StandardsService, new TestWebHostEnvironment(ContentRootPath));
    }

    public string ContentRootPath { get; }
    public AppDbContext DbContext { get; }
    public StandardsService StandardsService { get; }
    public LabTestRequestsService Service { get; }

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

    public Doctor AddDoctor(Guid clinicId, string name)
    {
        var user = AddUser(name, clinicId);
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
        var user = AddUser(name, clinicId);
        var patient = new Patient
        {
            PatientId = user.Id,
            UserID = userId,
            Gender = Gender.Female,
            BloodType = BloodType.OPositive,
            User = user
        };
        DbContext.Patients.Add(patient);
        return patient;
    }

    public LaboratoryTechnologist AddLaboratoryTechnologist(Guid clinicId, string name)
    {
        var user = AddUser(name, clinicId);
        var technologist = new LaboratoryTechnologist
        {
            LaboratoryTechnologistId = user.Id,
            ProfessionalLicenseNumber = $"LAB-{Guid.NewGuid():N}",
            User = user
        };
        DbContext.LaboratoryTechnologists.Add(technologist);
        return technologist;
    }

    public void AddLoinc(string code, string display)
    {
        DbContext.LoincCodes.Add(new LoincCode
        {
            Code = code,
            LongCommonName = display,
            Status = "ACTIVE"
        });
    }

    public static ClaimsPrincipal Principal(Guid userId)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, nameof(UserSystemRole.Doctor))
            ],
            "Test"));
    }

    public void Dispose()
    {
        DbContext.Dispose();
        if (Directory.Exists(ContentRootPath))
        {
            Directory.Delete(ContentRootPath, recursive: true);
        }
    }

    private User AddUser(string name, Guid clinicId)
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
}

internal sealed class RecordingHttpMessageHandler : HttpMessageHandler
{
    private readonly string _json;

    public RecordingHttpMessageHandler(string json)
    {
        _json = json;
    }

    public Uri? LastRequestUri { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastRequestUri = request.RequestUri;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_json, Encoding.UTF8, "application/json")
        });
    }
}
