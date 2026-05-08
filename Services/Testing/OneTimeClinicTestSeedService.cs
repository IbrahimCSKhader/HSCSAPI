using HSCSAPI.Data;
using HSCSAPI.DTOs.TestData;
using HSCSAPI.Models.Clinics;
using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Identity;
using HSCSAPI.Models.Profiles;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Services.Testing;

public class OneTimeClinicTestSeedService
{
    private const string DefaultSeedPassword = "TestSeed123";

    private static readonly IReadOnlyList<ClinicSeedPlan> ClinicPlans = new List<ClinicSeedPlan>
    {
        new(
            "Test Seed Clinic - Hebron",
            "Hebron - Ain Sara",
            new List<StaffSeedPlan>
            {
                new("Hebron Admin Secretary", "testseed.hebron.admin.secretary@seed.local", UserSystemRole.Secretary, "0597000001", "Hebron", new DateOnly(1991, 2, 14), IsClinicAdmin: true),
                new("Hebron Front Desk Secretary", "testseed.hebron.secretary@seed.local", UserSystemRole.Secretary, "0597000002", "Hebron", new DateOnly(1993, 7, 8)),
                new("Dr. Heba Qawasmi", "testseed.hebron.doctor1@seed.local", UserSystemRole.Doctor, "0597000003", "Hebron", new DateOnly(1984, 1, 11), "TST-HBR-DOC-001"),
                new("Dr. Majd Rajabi", "testseed.hebron.doctor2@seed.local", UserSystemRole.Doctor, "0597000004", "Hebron", new DateOnly(1986, 10, 5), "TST-HBR-DOC-002"),
                new("Hebron Lab Technologist", "testseed.hebron.lab@seed.local", UserSystemRole.LaboratoryTechnologist, "0597000005", "Hebron", new DateOnly(1990, 5, 20), "TST-HBR-LAB-001"),
                new("Hebron Radiology Technologist", "testseed.hebron.radiology@seed.local", UserSystemRole.RadiologyTechnologist, "0597000006", "Hebron", new DateOnly(1992, 4, 16), "TST-HBR-RAD-001")
            }),
        new(
            "Test Seed Clinic - Ramallah",
            "Ramallah - Al-Masyoun",
            new List<StaffSeedPlan>
            {
                new("Ramallah Admin Secretary", "testseed.ramallah.admin.secretary@seed.local", UserSystemRole.Secretary, "0597000011", "Ramallah", new DateOnly(1990, 3, 19), IsClinicAdmin: true),
                new("Ramallah Front Desk Secretary", "testseed.ramallah.secretary@seed.local", UserSystemRole.Secretary, "0597000012", "Ramallah", new DateOnly(1994, 9, 2)),
                new("Dr. Lina Barghouti", "testseed.ramallah.doctor1@seed.local", UserSystemRole.Doctor, "0597000013", "Ramallah", new DateOnly(1983, 8, 23), "TST-RML-DOC-001"),
                new("Dr. Tareq Hammad", "testseed.ramallah.doctor2@seed.local", UserSystemRole.Doctor, "0597000014", "Ramallah", new DateOnly(1987, 6, 17), "TST-RML-DOC-002"),
                new("Ramallah Lab Technologist", "testseed.ramallah.lab@seed.local", UserSystemRole.LaboratoryTechnologist, "0597000015", "Ramallah", new DateOnly(1991, 11, 7), "TST-RML-LAB-001"),
                new("Ramallah Radiology Technologist", "testseed.ramallah.radiology@seed.local", UserSystemRole.RadiologyTechnologist, "0597000016", "Ramallah", new DateOnly(1995, 1, 27), "TST-RML-RAD-001")
            }),
        new(
            "Test Seed Clinic - Nablus",
            "Nablus - Rafidia",
            new List<StaffSeedPlan>
            {
                new("Nablus Admin Secretary", "testseed.nablus.admin.secretary@seed.local", UserSystemRole.Secretary, "0597000021", "Nablus", new DateOnly(1992, 12, 6), IsClinicAdmin: true),
                new("Nablus Front Desk Secretary", "testseed.nablus.secretary@seed.local", UserSystemRole.Secretary, "0597000022", "Nablus", new DateOnly(1996, 2, 13)),
                new("Dr. Nour Odeh", "testseed.nablus.doctor1@seed.local", UserSystemRole.Doctor, "0597000023", "Nablus", new DateOnly(1982, 4, 30), "TST-NBL-DOC-001"),
                new("Dr. Kareem Abu Saleh", "testseed.nablus.doctor2@seed.local", UserSystemRole.Doctor, "0597000024", "Nablus", new DateOnly(1988, 7, 1), "TST-NBL-DOC-002"),
                new("Nablus Lab Technologist", "testseed.nablus.lab@seed.local", UserSystemRole.LaboratoryTechnologist, "0597000025", "Nablus", new DateOnly(1989, 8, 18), "TST-NBL-LAB-001"),
                new("Nablus Radiology Technologist", "testseed.nablus.radiology@seed.local", UserSystemRole.RadiologyTechnologist, "0597000026", "Nablus", new DateOnly(1993, 3, 9), "TST-NBL-RAD-001")
            })
    };

    private readonly AppDbContext _dbContext;
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<Role> _roleManager;

    public OneTimeClinicTestSeedService(
        AppDbContext dbContext,
        UserManager<User> userManager,
        RoleManager<Role> roleManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<ClinicTestDataSeedResponse> SeedOnceAsync(CancellationToken cancellationToken = default)
    {
        await EnsureRolesAsync();

        var seedState = await GetSeedStateAsync(cancellationToken);
        if (seedState.IsAlreadySeeded)
        {
            return await BuildExistingResponseAsync(
                success: false,
                alreadyExecuted: true,
                message: "This test clinic seed endpoint was already executed.",
                cancellationToken);
        }

        if (seedState.HasReservedConflicts)
        {
            return new ClinicTestDataSeedResponse
            {
                Success = false,
                AlreadyExecuted = false,
                Message = "Cannot run the test clinic seed because reserved seed clinic names, emails, or license numbers already exist.",
                DefaultPassword = DefaultSeedPassword
            };
        }

        var superAdmin = await GetSuperAdminAsync();
        if (superAdmin == null)
        {
            return new ClinicTestDataSeedResponse
            {
                Success = false,
                AlreadyExecuted = false,
                Message = "A super admin user is required before seeding the test clinics.",
                DefaultPassword = DefaultSeedPassword
            };
        }

        var strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var createdClinics = new List<CreatedClinicSeedContext>();

            foreach (var clinicPlan in ClinicPlans)
            {
                var clinic = new Clinic
                {
                    Name = clinicPlan.Name,
                    Address = clinicPlan.Address,
                    CreatedBySuperAdminUserId = superAdmin.Id
                };

                _dbContext.Clinics.Add(clinic);
                createdClinics.Add(new CreatedClinicSeedContext(clinicPlan, clinic));
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            foreach (var createdClinic in createdClinics)
            {
                foreach (var staffPlan in createdClinic.Plan.Staff)
                {
                    var user = await CreateUserAsync(staffPlan, createdClinic.Clinic.ClinicId, cancellationToken);
                    AddProfile(user.Id, staffPlan);

                    createdClinic.Staff.Add(new CreatedStaffSeedContext(user, staffPlan));

                    if (staffPlan.IsClinicAdmin)
                    {
                        createdClinic.Clinic.AdminSecretaryId = user.Id;
                    }
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return BuildResponse(
                success: true,
                alreadyExecuted: false,
                message: "Test clinics and staff were created successfully.",
                createdClinics);
        });
    }

    private async Task EnsureRolesAsync()
    {
        foreach (var roleName in Enum.GetNames<UserSystemRole>())
        {
            if (await _roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var role = Enum.Parse<UserSystemRole>(roleName);
            var result = await _roleManager.CreateAsync(new Role
            {
                Id = IdentityRoleIds.Get(role),
                Name = roleName
            });

            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Failed to create required role {roleName}: {string.Join(" ", result.Errors.Select(error => error.Description))}");
            }
        }
    }

    private async Task<SeedState> GetSeedStateAsync(CancellationToken cancellationToken)
    {
        var targetClinicNames = ClinicPlans
            .Select(plan => plan.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var targetEmails = ClinicPlans
            .SelectMany(plan => plan.Staff)
            .Select(plan => NormalizeEmail(plan.Email))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var targetLicenses = ClinicPlans
            .SelectMany(plan => plan.Staff)
            .Where(plan => !string.IsNullOrWhiteSpace(plan.ProfessionalLicenseNumber))
            .Select(plan => plan.ProfessionalLicenseNumber!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var existingClinicNames = (await _dbContext.Clinics
                .AsNoTracking()
                .Where(clinic => targetClinicNames.Contains(clinic.Name))
                .Select(clinic => clinic.Name)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var existingEmails = (await _dbContext.Users
                .AsNoTracking()
                .Where(user => user.Email != null && targetEmails.Contains(user.Email))
                .Select(user => user.Email!)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var doctorLicenses = await _dbContext.Doctors
            .AsNoTracking()
            .Where(doctor => targetLicenses.Contains(doctor.ProfessionalLicenseNumber))
            .Select(doctor => doctor.ProfessionalLicenseNumber)
            .ToListAsync(cancellationToken);

        var laboratoryLicenses = await _dbContext.LaboratoryTechnologists
            .AsNoTracking()
            .Where(tech => targetLicenses.Contains(tech.ProfessionalLicenseNumber))
            .Select(tech => tech.ProfessionalLicenseNumber)
            .ToListAsync(cancellationToken);

        var radiologyLicenses = await _dbContext.RadiologyTechnologists
            .AsNoTracking()
            .Where(tech => targetLicenses.Contains(tech.ProfessionalLicenseNumber))
            .Select(tech => tech.ProfessionalLicenseNumber)
            .ToListAsync(cancellationToken);

        var existingLicenses = doctorLicenses
            .Concat(laboratoryLicenses)
            .Concat(radiologyLicenses)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var isAlreadySeeded =
            existingClinicNames.SetEquals(targetClinicNames)
            && existingEmails.SetEquals(targetEmails)
            && existingLicenses.SetEquals(targetLicenses);

        var hasReservedConflicts =
            existingClinicNames.Count > 0
            || existingEmails.Count > 0
            || existingLicenses.Count > 0;

        return new SeedState(isAlreadySeeded, hasReservedConflicts);
    }

    private async Task<User?> GetSuperAdminAsync()
    {
        var users = await _userManager.GetUsersInRoleAsync(nameof(UserSystemRole.SuperAdmin));
        return users
            .OrderBy(user => user.Email)
            .FirstOrDefault();
    }

    private async Task<User> CreateUserAsync(
        StaffSeedPlan staffPlan,
        Guid clinicId,
        CancellationToken cancellationToken)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = staffPlan.Name,
            Email = NormalizeEmail(staffPlan.Email),
            UserName = NormalizeEmail(staffPlan.Email),
            PhoneNumber = staffPlan.PhoneNumber,
            Address = staffPlan.Address,
            DateOfBirth = staffPlan.DateOfBirth,
            ClinicId = clinicId,
            EmailConfirmed = true
        };

        var createResult = await _userManager.CreateAsync(user, DefaultSeedPassword);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException($"Failed to create seed user {staffPlan.Email}: {string.Join(" ", createResult.Errors.Select(error => error.Description))}");
        }

        var addToRoleResult = await _userManager.AddToRoleAsync(user, staffPlan.Role.ToString());
        if (!addToRoleResult.Succeeded)
        {
            throw new InvalidOperationException($"Failed to assign role {staffPlan.Role} to {staffPlan.Email}: {string.Join(" ", addToRoleResult.Errors.Select(error => error.Description))}");
        }

        return user;
    }

    private void AddProfile(Guid userId, StaffSeedPlan staffPlan)
    {
        switch (staffPlan.Role)
        {
            case UserSystemRole.Secretary:
                _dbContext.Secretaries.Add(new Secretary
                {
                    SecretaryId = userId
                });
                break;

            case UserSystemRole.Doctor:
                _dbContext.Doctors.Add(new Doctor
                {
                    DoctorId = userId,
                    ProfessionalLicenseNumber = RequireLicenseNumber(staffPlan)
                });
                break;

            case UserSystemRole.LaboratoryTechnologist:
                _dbContext.LaboratoryTechnologists.Add(new LaboratoryTechnologist
                {
                    LaboratoryTechnologistId = userId,
                    ProfessionalLicenseNumber = RequireLicenseNumber(staffPlan)
                });
                break;

            case UserSystemRole.RadiologyTechnologist:
                _dbContext.RadiologyTechnologists.Add(new RadiologyTechnologist
                {
                    RadiologyTechnologistId = userId,
                    ProfessionalLicenseNumber = RequireLicenseNumber(staffPlan)
                });
                break;

            default:
                throw new InvalidOperationException($"Unsupported seeded staff role: {staffPlan.Role}");
        }
    }

    private static string RequireLicenseNumber(StaffSeedPlan staffPlan)
    {
        if (string.IsNullOrWhiteSpace(staffPlan.ProfessionalLicenseNumber))
        {
            throw new InvalidOperationException($"A professional license number is required for role {staffPlan.Role}.");
        }

        return staffPlan.ProfessionalLicenseNumber;
    }

    private ClinicTestDataSeedResponse BuildResponse(
        bool success,
        bool alreadyExecuted,
        string message,
        List<CreatedClinicSeedContext> createdClinics)
    {
        var clinicResponses = createdClinics
            .Select(createdClinic => new SeededClinicResponse
            {
                ClinicId = createdClinic.Clinic.ClinicId,
                Name = createdClinic.Clinic.Name,
                Address = createdClinic.Clinic.Address,
                AdminSecretaryId = createdClinic.Clinic.AdminSecretaryId,
                Staff = createdClinic.Staff
                    .Select(createdStaff => new SeededStaffMemberResponse
                    {
                        UserId = createdStaff.User.Id,
                        Name = createdStaff.User.Name,
                        Email = createdStaff.User.Email ?? string.Empty,
                        Role = createdStaff.Plan.Role.ToString(),
                        IsClinicAdmin = createdStaff.Plan.IsClinicAdmin
                    })
                    .ToList()
            })
            .ToList();

        return new ClinicTestDataSeedResponse
        {
            Success = success,
            AlreadyExecuted = alreadyExecuted,
            Message = message,
            DefaultPassword = DefaultSeedPassword,
            ClinicsCount = clinicResponses.Count,
            StaffCount = clinicResponses.Sum(clinic => clinic.Staff.Count),
            Clinics = clinicResponses
        };
    }

    private async Task<ClinicTestDataSeedResponse> BuildExistingResponseAsync(
        bool success,
        bool alreadyExecuted,
        string message,
        CancellationToken cancellationToken)
    {
        var targetClinicNames = ClinicPlans
            .Select(plan => plan.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var targetEmails = ClinicPlans
            .SelectMany(plan => plan.Staff)
            .Select(plan => NormalizeEmail(plan.Email))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var clinics = (await _dbContext.Clinics
                .AsNoTracking()
                .Where(clinic => targetClinicNames.Contains(clinic.Name))
                .ToListAsync(cancellationToken))
            .ToDictionary(clinic => clinic.Name, StringComparer.OrdinalIgnoreCase);

        var users = (await _dbContext.Users
                .AsNoTracking()
                .Where(user => user.Email != null && targetEmails.Contains(user.Email))
                .ToListAsync(cancellationToken))
            .ToDictionary(user => NormalizeEmail(user.Email!), StringComparer.OrdinalIgnoreCase);

        var clinicResponses = new List<SeededClinicResponse>();

        foreach (var clinicPlan in ClinicPlans)
        {
            if (!clinics.TryGetValue(clinicPlan.Name, out var clinic))
            {
                continue;
            }

            var staffResponses = new List<SeededStaffMemberResponse>();

            foreach (var staffPlan in clinicPlan.Staff)
            {
                if (!users.TryGetValue(NormalizeEmail(staffPlan.Email), out var user))
                {
                    continue;
                }

                staffResponses.Add(new SeededStaffMemberResponse
                {
                    UserId = user.Id,
                    Name = user.Name,
                    Email = user.Email ?? string.Empty,
                    Role = staffPlan.Role.ToString(),
                    IsClinicAdmin = staffPlan.IsClinicAdmin
                });
            }

            clinicResponses.Add(new SeededClinicResponse
            {
                ClinicId = clinic.ClinicId,
                Name = clinic.Name,
                Address = clinic.Address,
                AdminSecretaryId = clinic.AdminSecretaryId,
                Staff = staffResponses
            });
        }

        return new ClinicTestDataSeedResponse
        {
            Success = success,
            AlreadyExecuted = alreadyExecuted,
            Message = message,
            DefaultPassword = DefaultSeedPassword,
            ClinicsCount = clinicResponses.Count,
            StaffCount = clinicResponses.Sum(clinic => clinic.Staff.Count),
            Clinics = clinicResponses
        };
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private sealed record ClinicSeedPlan(string Name, string Address, IReadOnlyList<StaffSeedPlan> Staff);

    private sealed record StaffSeedPlan(
        string Name,
        string Email,
        UserSystemRole Role,
        string PhoneNumber,
        string Address,
        DateOnly DateOfBirth,
        string? ProfessionalLicenseNumber = null,
        bool IsClinicAdmin = false);

    private sealed record SeedState(bool IsAlreadySeeded, bool HasReservedConflicts);

    private sealed class CreatedClinicSeedContext
    {
        public CreatedClinicSeedContext(ClinicSeedPlan plan, Clinic clinic)
        {
            Plan = plan;
            Clinic = clinic;
        }

        public ClinicSeedPlan Plan { get; }
        public Clinic Clinic { get; }
        public List<CreatedStaffSeedContext> Staff { get; } = new();
    }

    private sealed record CreatedStaffSeedContext(User User, StaffSeedPlan Plan);
}
