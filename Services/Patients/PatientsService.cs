using System.Security.Claims;
using HSCSAPI.Data;
using HSCSAPI.DTOs.Patient;
using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Identity;
using HSCSAPI.Models.Profiles;
using HSCSAPI.Services.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Services.Patients;

public class PatientsService : IPatientsService
{
    public const string SuperAdminOrSecretaryRoles = nameof(UserSystemRole.SuperAdmin) + "," + nameof(UserSystemRole.Secretary);
    public const string SuperAdminOrSecretaryOrPatientRoles = SuperAdminOrSecretaryRoles + "," + nameof(UserSystemRole.Patient);

    private readonly AppDbContext _dbContext;
    private readonly UserManager<User> _userManager;
    private readonly UserIdGeneratorService _userIdGenerator;

    public PatientsService(
        AppDbContext dbContext,
        UserManager<User> userManager,
        UserIdGeneratorService userIdGenerator)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _userIdGenerator = userIdGenerator;
    }

    public async Task<ActionResult<List<PatientResponse>>> GetAllAsync(
        Guid? clinicId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var query = BuildPatientResponseQuery();

        if (user.IsInRole(nameof(UserSystemRole.SuperAdmin)))
        {
            if (clinicId.HasValue)
            {
                query = query.Where(patient => patient.ClinicId == clinicId.Value);
            }

            var patients = await query.ToListAsync(cancellationToken);
            return new OkObjectResult(patients);
        }

        var secretaryClinicId = await GetCurrentSecretaryClinicIdAsync(user, cancellationToken);
        if (secretaryClinicId is null)
        {
            return ForbiddenList("This secretary is not assigned to any clinic.");
        }

        if (clinicId.HasValue && clinicId.Value != secretaryClinicId.Value)
        {
            return ForbiddenList("You are not allowed to access patients outside your clinic.");
        }

        var clinicPatients = await query
            .Where(patient => patient.ClinicId == secretaryClinicId.Value)
            .ToListAsync(cancellationToken);

        return new OkObjectResult(clinicPatients);
    }

    public async Task<ActionResult<List<PatientResponse>>> GetByClinicAsync(
        Guid clinicId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var clinicExists = await _dbContext.Clinics
            .AsNoTracking()
            .AnyAsync(clinic => clinic.ClinicId == clinicId, cancellationToken);

        if (!clinicExists)
        {
            return new NotFoundObjectResult("Clinic not found.");
        }

        if (!await CanCurrentUserAccessClinicAsync(clinicId, user, cancellationToken))
        {
            return ForbiddenList("You are not allowed to access patients in this clinic.");
        }

        var patients = await BuildPatientResponseQuery()
            .Where(patient => patient.ClinicId == clinicId)
            .ToListAsync(cancellationToken);

        return new OkObjectResult(patients);
    }

    public async Task<ActionResult<PatientResponse>> GetByIdAsync(
        Guid patientId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId(user);
        if (currentUserId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        if (user.IsInRole(nameof(UserSystemRole.Patient)) && currentUserId.Value != patientId)
        {
            return ForbiddenSingle("You are not allowed to access another patient profile.");
        }

        var patient = await GetPatientResponseAsync(patientId, cancellationToken);
        if (patient is null)
        {
            return new NotFoundObjectResult("Patient not found.");
        }

        if (user.IsInRole(nameof(UserSystemRole.SuperAdmin)) || user.IsInRole(nameof(UserSystemRole.Patient)))
        {
            return new OkObjectResult(patient);
        }

        if (!patient.ClinicId.HasValue || !await CanCurrentUserAccessClinicAsync(patient.ClinicId.Value, user, cancellationToken))
        {
            return ForbiddenSingle("You are not allowed to access patients outside your clinic.");
        }

        return new OkObjectResult(patient);
    }

    public async Task<ActionResult<PatientResponse>> GetMyProfileAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId(user);
        if (currentUserId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var patient = await GetPatientResponseAsync(currentUserId.Value, cancellationToken);
        return patient is null
            ? new NotFoundObjectResult("Patient not found.")
            : new OkObjectResult(patient);
    }

    public async Task<ActionResult<PatientResponse>> UpdateAsync(
        Guid patientId,
        UpdatePatientRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return new BadRequestObjectResult("Name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return new BadRequestObjectResult("Email is required.");
        }

        if (request.ClinicId == Guid.Empty)
        {
            return new BadRequestObjectResult("ClinicId is required.");
        }

        if (!TryParseGender(request.Gender, out var gender, out var genderError))
        {
            return new BadRequestObjectResult(genderError);
        }

        if (!TryParseBloodType(request.BloodType, out var bloodType, out var bloodTypeError))
        {
            return new BadRequestObjectResult(bloodTypeError);
        }

        var patient = await _dbContext.Patients
            .Include(profile => profile.User)
            .FirstOrDefaultAsync(profile => profile.PatientId == patientId, cancellationToken);

        if (patient is null)
        {
            return new NotFoundObjectResult("Patient not found.");
        }

        if (!await CanCurrentUserManagePatientAsync(patient, user, cancellationToken))
        {
            return ForbiddenSingle("You are not allowed to manage this patient.");
        }

        if (!await CanCurrentUserAccessClinicAsync(request.ClinicId, user, cancellationToken))
        {
            return ForbiddenSingle("You are not allowed to assign the patient to this clinic.");
        }

        var clinicExists = await _dbContext.Clinics
            .AsNoTracking()
            .AnyAsync(clinic => clinic.ClinicId == request.ClinicId && clinic.IsActive, cancellationToken);

        if (!clinicExists)
        {
            return new NotFoundObjectResult("Clinic not found or inactive.");
        }

        var normalizedEmail = NormalizeEmail(request.Email);
        var normalizedLookup = _userManager.NormalizeEmail(normalizedEmail);
        var emailAlreadyRegistered = await _userManager.Users
            .AsNoTracking()
            .AnyAsync(
                existingUser => existingUser.Id != patientId
                    && existingUser.NormalizedEmail == normalizedLookup,
                cancellationToken);

        if (emailAlreadyRegistered)
        {
            return new BadRequestObjectResult("Email already registered.");
        }

        patient.User.Name = request.Name.Trim();
        patient.User.Email = normalizedEmail;
        patient.User.UserName = normalizedEmail;
        patient.User.PhoneNumber = NormalizeOptional(request.PhoneNumber);
        patient.User.Address = NormalizeOptional(request.Address);
        patient.User.DateOfBirth = request.DateOfBirth;

        var clinicChanged = patient.User.ClinicId != request.ClinicId;
        patient.User.ClinicId = request.ClinicId;
        patient.Gender = gender;
        patient.BloodType = bloodType;

        if (clinicChanged)
        {
            patient.UserID = await _userIdGenerator.GenerateUserIdAsync(
                request.ClinicId,
                UserSystemRole.Patient,
                cancellationToken);
        }

        var updateResult = await _userManager.UpdateAsync(patient.User);
        if (!updateResult.Succeeded)
        {
            return new BadRequestObjectResult(string.Join(" ", updateResult.Errors.Select(error => error.Description)));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = await GetPatientResponseAsync(patientId, cancellationToken);
        return response is null
            ? new NotFoundObjectResult("Patient not found.")
            : new OkObjectResult(response);
    }

    public async Task<ActionResult<PatientResponse>> UpdateMyProfileAsync(
        UpdateMyPatientProfileRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return new BadRequestObjectResult("Name is required.");
        }

        if (!TryParseGender(request.Gender, out var gender, out var genderError))
        {
            return new BadRequestObjectResult(genderError);
        }

        if (!TryParseBloodType(request.BloodType, out var bloodType, out var bloodTypeError))
        {
            return new BadRequestObjectResult(bloodTypeError);
        }

        var currentUserId = GetCurrentUserId(user);
        if (currentUserId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var patient = await _dbContext.Patients
            .Include(profile => profile.User)
            .FirstOrDefaultAsync(profile => profile.PatientId == currentUserId.Value, cancellationToken);

        if (patient is null)
        {
            return new NotFoundObjectResult("Patient not found.");
        }

        patient.User.Name = request.Name.Trim();
        patient.User.PhoneNumber = NormalizeOptional(request.PhoneNumber);
        patient.User.Address = NormalizeOptional(request.Address);
        patient.User.DateOfBirth = request.DateOfBirth;
        patient.Gender = gender;
        patient.BloodType = bloodType;

        var updateResult = await _userManager.UpdateAsync(patient.User);
        if (!updateResult.Succeeded)
        {
            return new BadRequestObjectResult(string.Join(" ", updateResult.Errors.Select(error => error.Description)));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = await GetPatientResponseAsync(patient.PatientId, cancellationToken);
        return response is null
            ? new NotFoundObjectResult("Patient not found.")
            : new OkObjectResult(response);
    }

    public Task<IActionResult> DeactivateAsync(
        Guid patientId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        return SetActiveStateAsync(patientId, false, user, cancellationToken);
    }

    public Task<IActionResult> ActivateAsync(
        Guid patientId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        return SetActiveStateAsync(patientId, true, user, cancellationToken);
    }

    private async Task<IActionResult> SetActiveStateAsync(
        Guid patientId,
        bool isActive,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var patient = await _dbContext.Patients
            .Include(profile => profile.User)
            .FirstOrDefaultAsync(profile => profile.PatientId == patientId, cancellationToken);

        if (patient is null)
        {
            return new NotFoundObjectResult("Patient not found.");
        }

        if (!await CanCurrentUserManagePatientAsync(patient, user, cancellationToken))
        {
            return ForbiddenAction("You are not allowed to change this patient's active state.");
        }

        if (patient.User.IsActive == isActive)
        {
            return new NoContentResult();
        }

        patient.User.IsActive = isActive;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new NoContentResult();
    }

    private IQueryable<PatientResponse> BuildPatientResponseQuery()
    {
        return _dbContext.Patients
            .AsNoTracking()
            .Select(patient => new PatientResponse
            {
                PatientId = patient.PatientId,
                PatientUserId = patient.UserID,
                Name = patient.User.Name,
                Email = patient.User.Email ?? string.Empty,
                PhoneNumber = patient.User.PhoneNumber,
                Address = patient.User.Address,
                DateOfBirth = patient.User.DateOfBirth,
                Gender = patient.Gender.ToString(),
                BloodType = patient.BloodType.HasValue ? patient.BloodType.Value.ToString() : null,
                ClinicId = patient.User.ClinicId,
                ClinicName = patient.User.Clinic != null ? patient.User.Clinic.Name : null,
                EmailConfirmed = patient.User.EmailConfirmed,
                IsActive = patient.User.IsActive
            });
    }

    private async Task<PatientResponse?> GetPatientResponseAsync(Guid patientId, CancellationToken cancellationToken)
    {
        return await BuildPatientResponseQuery()
            .FirstOrDefaultAsync(patient => patient.PatientId == patientId, cancellationToken);
    }

    private async Task<bool> CanCurrentUserManagePatientAsync(
        Patient patient,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (user.IsInRole(nameof(UserSystemRole.SuperAdmin)))
        {
            return true;
        }

        if (!patient.User.ClinicId.HasValue)
        {
            return false;
        }

        return await CanCurrentUserAccessClinicAsync(patient.User.ClinicId.Value, user, cancellationToken);
    }

    private async Task<bool> CanCurrentUserAccessClinicAsync(
        Guid clinicId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (user.IsInRole(nameof(UserSystemRole.SuperAdmin)))
        {
            return true;
        }

        var secretaryClinicId = await GetCurrentSecretaryClinicIdAsync(user, cancellationToken);
        return secretaryClinicId.HasValue && secretaryClinicId.Value == clinicId;
    }

    private async Task<Guid?> GetCurrentSecretaryClinicIdAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId(user);
        if (currentUserId is null)
        {
            return null;
        }

        return await _dbContext.Secretaries
            .AsNoTracking()
            .Where(secretary => secretary.SecretaryId == currentUserId.Value)
            .Select(secretary => secretary.User.ClinicId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<List<string>> GetDeleteBlockersAsync(Guid patientId, CancellationToken cancellationToken)
    {
        var blockers = new List<string>();

        if (await _dbContext.Appointments.AnyAsync(appointment => appointment.PatientId == patientId, cancellationToken))
        {
            blockers.Add("appointments");
        }

        if (await _dbContext.Reminders.AnyAsync(reminder => reminder.PatientId == patientId, cancellationToken))
        {
            blockers.Add("reminders");
        }

        if (await _dbContext.PatientAuthorizedMembers.AnyAsync(
                relation => relation.PatientId == patientId,
                cancellationToken))
        {
            blockers.Add("authorized member relations");
        }

        if (await _dbContext.Invites.AnyAsync(invite => invite.PatientId == patientId, cancellationToken))
        {
            blockers.Add("invites");
        }

        if (await _dbContext.FileDownloadRequests.AnyAsync(
                request => request.PatientId == patientId,
                cancellationToken))
        {
            blockers.Add("file download requests");
        }

        return blockers;
    }

    private static Guid? GetCurrentUserId(ClaimsPrincipal user)
    {
        var claim = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (claim is null)
        {
            return null;
        }

        return Guid.TryParse(claim, out var userId) ? userId : null;
    }

    private static bool TryParseGender(string value, out Gender gender, out string error)
    {
        if (Enum.TryParse<Gender>(value, true, out gender))
        {
            error = string.Empty;
            return true;
        }

        error = "Invalid gender value.";
        return false;
    }

    private static bool TryParseBloodType(string? value, out BloodType? bloodType, out string error)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            bloodType = null;
            error = string.Empty;
            return true;
        }

        if (Enum.TryParse<BloodType>(value, true, out var parsedBloodType))
        {
            bloodType = parsedBloodType;
            error = string.Empty;
            return true;
        }

        bloodType = null;
        error = "Invalid blood type value.";
        return false;
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static ActionResult<List<PatientResponse>> ForbiddenList(string message)
    {
        return new ObjectResult(message)
        {
            StatusCode = 403
        };
    }

    private static ActionResult<PatientResponse> ForbiddenSingle(string message)
    {
        return new ObjectResult(message)
        {
            StatusCode = 403
        };
    }

    private static IActionResult ForbiddenAction(string message)
    {
        return new ObjectResult(message)
        {
            StatusCode = 403
        };
    }
}
