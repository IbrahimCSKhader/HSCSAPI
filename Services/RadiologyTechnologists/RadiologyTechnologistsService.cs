using System.Security.Claims;
using HSCSAPI.Data;
using HSCSAPI.DTOs.RadiologyTechnologist;
using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Identity;
using HSCSAPI.Models.Profiles;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Services.RadiologyTechnologists;

public class RadiologyTechnologistsService : IRadiologyTechnologistsService
{
    public const string SuperAdminOrSecretaryRoles = nameof(UserSystemRole.SuperAdmin) + "," + nameof(UserSystemRole.Secretary);
    public const string SuperAdminOrSecretaryOrRadiologyTechnologistRoles =
        SuperAdminOrSecretaryRoles + "," + nameof(UserSystemRole.RadiologyTechnologist);

    private readonly AppDbContext _dbContext;
    private readonly UserManager<User> _userManager;

    public RadiologyTechnologistsService(
        AppDbContext dbContext,
        UserManager<User> userManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
    }

    public async Task<ActionResult<List<RadiologyTechnologistResponse>>> GetAllAsync(
        Guid? clinicId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var query = BuildRadiologyTechnologistResponseQuery();

        if (user.IsInRole(nameof(UserSystemRole.SuperAdmin)))
        {
            if (clinicId.HasValue)
            {
                query = query.Where(technologist => technologist.ClinicId == clinicId.Value);
            }

            var technologists = await query.ToListAsync(cancellationToken);
            return new OkObjectResult(technologists);
        }

        var secretaryClinicId = await GetCurrentSecretaryClinicIdAsync(user, cancellationToken);
        if (secretaryClinicId is null)
        {
            return ForbiddenList("This secretary is not assigned to any clinic.");
        }

        if (clinicId.HasValue && clinicId.Value != secretaryClinicId.Value)
        {
            return ForbiddenList("You are not allowed to access radiology technologists outside your clinic.");
        }

        var clinicTechnologists = await query
            .Where(technologist => technologist.ClinicId == secretaryClinicId.Value)
            .ToListAsync(cancellationToken);

        return new OkObjectResult(clinicTechnologists);
    }

    public async Task<ActionResult<List<RadiologyTechnologistResponse>>> GetByClinicAsync(
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
            return ForbiddenList("You are not allowed to access radiology technologists in this clinic.");
        }

        var technologists = await BuildRadiologyTechnologistResponseQuery()
            .Where(technologist => technologist.ClinicId == clinicId)
            .ToListAsync(cancellationToken);

        return new OkObjectResult(technologists);
    }

    public async Task<ActionResult<RadiologyTechnologistResponse>> GetByIdAsync(
        Guid radiologyTechnologistId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId(user);
        if (currentUserId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        if (user.IsInRole(nameof(UserSystemRole.RadiologyTechnologist)) && currentUserId.Value != radiologyTechnologistId)
        {
            return ForbiddenSingle("You are not allowed to access another radiology technologist profile.");
        }

        var technologist = await GetRadiologyTechnologistResponseAsync(radiologyTechnologistId, cancellationToken);
        if (technologist is null)
        {
            return new NotFoundObjectResult("Radiology technologist not found.");
        }

        if (user.IsInRole(nameof(UserSystemRole.SuperAdmin)) || user.IsInRole(nameof(UserSystemRole.RadiologyTechnologist)))
        {
            return new OkObjectResult(technologist);
        }

        if (!technologist.ClinicId.HasValue || !await CanCurrentUserAccessClinicAsync(technologist.ClinicId.Value, user, cancellationToken))
        {
            return ForbiddenSingle("You are not allowed to access radiology technologists outside your clinic.");
        }

        return new OkObjectResult(technologist);
    }

    public async Task<ActionResult<RadiologyTechnologistResponse>> GetMyProfileAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId(user);
        if (currentUserId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var technologist = await GetRadiologyTechnologistResponseAsync(currentUserId.Value, cancellationToken);
        return technologist is null
            ? new NotFoundObjectResult("Radiology technologist not found.")
            : new OkObjectResult(technologist);
    }

    public async Task<ActionResult<RadiologyTechnologistResponse>> UpdateAsync(
        Guid radiologyTechnologistId,
        UpdateRadiologyTechnologistRequest request,
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

        if (string.IsNullOrWhiteSpace(request.ProfessionalLicenseNumber))
        {
            return new BadRequestObjectResult("ProfessionalLicenseNumber is required.");
        }

        var technologist = await _dbContext.RadiologyTechnologists
            .Include(profile => profile.User)
            .FirstOrDefaultAsync(
                profile => profile.RadiologyTechnologistId == radiologyTechnologistId,
                cancellationToken);

        if (technologist is null)
        {
            return new NotFoundObjectResult("Radiology technologist not found.");
        }

        if (!await CanCurrentUserManageRadiologyTechnologistAsync(technologist, user, cancellationToken))
        {
            return ForbiddenSingle("You are not allowed to manage this radiology technologist.");
        }

        if (!await CanCurrentUserAccessClinicAsync(request.ClinicId, user, cancellationToken))
        {
            return ForbiddenSingle("You are not allowed to assign the radiology technologist to this clinic.");
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
                existingUser => existingUser.Id != radiologyTechnologistId
                    && existingUser.NormalizedEmail == normalizedLookup,
                cancellationToken);

        if (emailAlreadyRegistered)
        {
            return new BadRequestObjectResult("Email already registered.");
        }

        var normalizedLicenseNumber = request.ProfessionalLicenseNumber.Trim();
        var licenseNumberExists = await _dbContext.RadiologyTechnologists
            .AsNoTracking()
            .AnyAsync(
                existingTechnologist => existingTechnologist.RadiologyTechnologistId != radiologyTechnologistId
                    && existingTechnologist.ProfessionalLicenseNumber == normalizedLicenseNumber,
                cancellationToken);

        if (licenseNumberExists)
        {
            return new BadRequestObjectResult("Professional license number is already registered.");
        }

        technologist.User.Name = request.Name.Trim();
        technologist.User.Email = normalizedEmail;
        technologist.User.UserName = normalizedEmail;
        technologist.User.PhoneNumber = NormalizeOptional(request.PhoneNumber);
        technologist.User.Address = NormalizeOptional(request.Address);
        technologist.User.DateOfBirth = request.DateOfBirth;
        technologist.User.ClinicId = request.ClinicId;
        technologist.ProfessionalLicenseNumber = normalizedLicenseNumber;

        var updateResult = await _userManager.UpdateAsync(technologist.User);
        if (!updateResult.Succeeded)
        {
            return new BadRequestObjectResult(string.Join(" ", updateResult.Errors.Select(error => error.Description)));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = await GetRadiologyTechnologistResponseAsync(radiologyTechnologistId, cancellationToken);
        return response is null
            ? new NotFoundObjectResult("Radiology technologist not found.")
            : new OkObjectResult(response);
    }

    public async Task<ActionResult<RadiologyTechnologistResponse>> UpdateMyProfileAsync(
        UpdateMyRadiologyTechnologistProfileRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return new BadRequestObjectResult("Name is required.");
        }

        var currentUserId = GetCurrentUserId(user);
        if (currentUserId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var technologist = await _dbContext.RadiologyTechnologists
            .Include(profile => profile.User)
            .FirstOrDefaultAsync(
                profile => profile.RadiologyTechnologistId == currentUserId.Value,
                cancellationToken);

        if (technologist is null)
        {
            return new NotFoundObjectResult("Radiology technologist not found.");
        }

        technologist.User.Name = request.Name.Trim();
        technologist.User.PhoneNumber = NormalizeOptional(request.PhoneNumber);
        technologist.User.Address = NormalizeOptional(request.Address);
        technologist.User.DateOfBirth = request.DateOfBirth;

        var updateResult = await _userManager.UpdateAsync(technologist.User);
        if (!updateResult.Succeeded)
        {
            return new BadRequestObjectResult(string.Join(" ", updateResult.Errors.Select(error => error.Description)));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = await GetRadiologyTechnologistResponseAsync(technologist.RadiologyTechnologistId, cancellationToken);
        return response is null
            ? new NotFoundObjectResult("Radiology technologist not found.")
            : new OkObjectResult(response);
    }

    public Task<IActionResult> DeactivateAsync(
        Guid radiologyTechnologistId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        return SetActiveStateAsync(radiologyTechnologistId, false, user, cancellationToken);
    }

    public Task<IActionResult> ActivateAsync(
        Guid radiologyTechnologistId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        return SetActiveStateAsync(radiologyTechnologistId, true, user, cancellationToken);
    }

    private async Task<IActionResult> SetActiveStateAsync(
        Guid radiologyTechnologistId,
        bool isActive,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var technologist = await _dbContext.RadiologyTechnologists
            .Include(profile => profile.User)
            .FirstOrDefaultAsync(
                profile => profile.RadiologyTechnologistId == radiologyTechnologistId,
                cancellationToken);

        if (technologist is null)
        {
            return new NotFoundObjectResult("Radiology technologist not found.");
        }

        if (!await CanCurrentUserManageRadiologyTechnologistAsync(technologist, user, cancellationToken))
        {
            return ForbiddenAction("You are not allowed to change this radiology technologist's active state.");
        }

        if (technologist.User.IsActive == isActive)
        {
            return new NoContentResult();
        }

        technologist.User.IsActive = isActive;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new NoContentResult();
    }

    private IQueryable<RadiologyTechnologistResponse> BuildRadiologyTechnologistResponseQuery()
    {
        return _dbContext.RadiologyTechnologists
            .AsNoTracking()
            .Select(technologist => new RadiologyTechnologistResponse
            {
                RadiologyTechnologistId = technologist.RadiologyTechnologistId,
                Name = technologist.User.Name,
                Email = technologist.User.Email ?? string.Empty,
                PhoneNumber = technologist.User.PhoneNumber,
                Address = technologist.User.Address,
                DateOfBirth = technologist.User.DateOfBirth,
                ClinicId = technologist.User.ClinicId,
                ClinicName = technologist.User.Clinic != null ? technologist.User.Clinic.Name : null,
                ProfessionalLicenseNumber = technologist.ProfessionalLicenseNumber,
                EmailConfirmed = technologist.User.EmailConfirmed,
                IsActive = technologist.User.IsActive
            });
    }

    private async Task<RadiologyTechnologistResponse?> GetRadiologyTechnologistResponseAsync(
        Guid radiologyTechnologistId,
        CancellationToken cancellationToken)
    {
        return await BuildRadiologyTechnologistResponseQuery()
            .FirstOrDefaultAsync(
                technologist => technologist.RadiologyTechnologistId == radiologyTechnologistId,
                cancellationToken);
    }

    private async Task<bool> CanCurrentUserManageRadiologyTechnologistAsync(
        RadiologyTechnologist technologist,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (user.IsInRole(nameof(UserSystemRole.SuperAdmin)))
        {
            return true;
        }

        if (!technologist.User.ClinicId.HasValue)
        {
            return false;
        }

        return await CanCurrentUserAccessClinicAsync(technologist.User.ClinicId.Value, user, cancellationToken);
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

    private static Guid? GetCurrentUserId(ClaimsPrincipal user)
    {
        var claim = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (claim is null)
        {
            return null;
        }

        return Guid.TryParse(claim, out var userId) ? userId : null;
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static ActionResult<List<RadiologyTechnologistResponse>> ForbiddenList(string message)
    {
        return new ObjectResult(message)
        {
            StatusCode = 403
        };
    }

    private static ActionResult<RadiologyTechnologistResponse> ForbiddenSingle(string message)
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
