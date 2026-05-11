using System.Security.Claims;
using HSCSAPI.Data;
using HSCSAPI.DTOs.LaboratoryTechnologist;
using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Identity;
using HSCSAPI.Models.Profiles;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Services.LaboratoryTechnologists;

public class LaboratoryTechnologistsService : ILaboratoryTechnologistsService
{
    public const string SuperAdminOrSecretaryRoles = nameof(UserSystemRole.SuperAdmin) + "," + nameof(UserSystemRole.Secretary);
    public const string SuperAdminOrSecretaryOrLaboratoryTechnologistRoles =
        SuperAdminOrSecretaryRoles + "," + nameof(UserSystemRole.LaboratoryTechnologist);

    private readonly AppDbContext _dbContext;
    private readonly UserManager<User> _userManager;

    public LaboratoryTechnologistsService(
        AppDbContext dbContext,
        UserManager<User> userManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
    }

    public async Task<ActionResult<List<LaboratoryTechnologistResponse>>> GetAllAsync(
        Guid? clinicId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var query = BuildLaboratoryTechnologistResponseQuery();

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
            return ForbiddenList("You are not allowed to access laboratory technologists outside your clinic.");
        }

        var clinicTechnologists = await query
            .Where(technologist => technologist.ClinicId == secretaryClinicId.Value)
            .ToListAsync(cancellationToken);

        return new OkObjectResult(clinicTechnologists);
    }

    public async Task<ActionResult<List<LaboratoryTechnologistResponse>>> GetByClinicAsync(
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
            return ForbiddenList("You are not allowed to access laboratory technologists in this clinic.");
        }

        var technologists = await BuildLaboratoryTechnologistResponseQuery()
            .Where(technologist => technologist.ClinicId == clinicId)
            .ToListAsync(cancellationToken);

        return new OkObjectResult(technologists);
    }

    public async Task<ActionResult<LaboratoryTechnologistResponse>> GetByIdAsync(
        Guid laboratoryTechnologistId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId(user);
        if (currentUserId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        if (user.IsInRole(nameof(UserSystemRole.LaboratoryTechnologist)) && currentUserId.Value != laboratoryTechnologistId)
        {
            return ForbiddenSingle("You are not allowed to access another laboratory technologist profile.");
        }

        var technologist = await GetLaboratoryTechnologistResponseAsync(laboratoryTechnologistId, cancellationToken);
        if (technologist is null)
        {
            return new NotFoundObjectResult("Laboratory technologist not found.");
        }

        if (user.IsInRole(nameof(UserSystemRole.SuperAdmin)) || user.IsInRole(nameof(UserSystemRole.LaboratoryTechnologist)))
        {
            return new OkObjectResult(technologist);
        }

        if (!technologist.ClinicId.HasValue || !await CanCurrentUserAccessClinicAsync(technologist.ClinicId.Value, user, cancellationToken))
        {
            return ForbiddenSingle("You are not allowed to access laboratory technologists outside your clinic.");
        }

        return new OkObjectResult(technologist);
    }

    public async Task<ActionResult<LaboratoryTechnologistResponse>> GetMyProfileAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId(user);
        if (currentUserId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var technologist = await GetLaboratoryTechnologistResponseAsync(currentUserId.Value, cancellationToken);
        return technologist is null
            ? new NotFoundObjectResult("Laboratory technologist not found.")
            : new OkObjectResult(technologist);
    }

    public async Task<ActionResult<LaboratoryTechnologistResponse>> UpdateAsync(
        Guid laboratoryTechnologistId,
        UpdateLaboratoryTechnologistRequest request,
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

        var technologist = await _dbContext.LaboratoryTechnologists
            .Include(profile => profile.User)
            .FirstOrDefaultAsync(
                profile => profile.LaboratoryTechnologistId == laboratoryTechnologistId,
                cancellationToken);

        if (technologist is null)
        {
            return new NotFoundObjectResult("Laboratory technologist not found.");
        }

        if (!await CanCurrentUserManageLaboratoryTechnologistAsync(technologist, user, cancellationToken))
        {
            return ForbiddenSingle("You are not allowed to manage this laboratory technologist.");
        }

        if (!await CanCurrentUserAccessClinicAsync(request.ClinicId, user, cancellationToken))
        {
            return ForbiddenSingle("You are not allowed to assign the laboratory technologist to this clinic.");
        }

        var clinicExists = await _dbContext.Clinics
            .AsNoTracking()
            .AnyAsync(clinic => clinic.ClinicId == request.ClinicId, cancellationToken);

        if (!clinicExists)
        {
            return new NotFoundObjectResult("Clinic not found.");
        }

        var normalizedEmail = NormalizeEmail(request.Email);
        var normalizedLookup = _userManager.NormalizeEmail(normalizedEmail);
        var emailAlreadyRegistered = await _userManager.Users
            .AsNoTracking()
            .AnyAsync(
                existingUser => existingUser.Id != laboratoryTechnologistId
                    && existingUser.NormalizedEmail == normalizedLookup,
                cancellationToken);

        if (emailAlreadyRegistered)
        {
            return new BadRequestObjectResult("Email already registered.");
        }

        var normalizedLicenseNumber = request.ProfessionalLicenseNumber.Trim();
        var licenseNumberExists = await _dbContext.LaboratoryTechnologists
            .AsNoTracking()
            .AnyAsync(
                existingTechnologist => existingTechnologist.LaboratoryTechnologistId != laboratoryTechnologistId
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

        var response = await GetLaboratoryTechnologistResponseAsync(laboratoryTechnologistId, cancellationToken);
        return response is null
            ? new NotFoundObjectResult("Laboratory technologist not found.")
            : new OkObjectResult(response);
    }

    public async Task<ActionResult<LaboratoryTechnologistResponse>> UpdateMyProfileAsync(
        UpdateMyLaboratoryTechnologistProfileRequest request,
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

        var technologist = await _dbContext.LaboratoryTechnologists
            .Include(profile => profile.User)
            .FirstOrDefaultAsync(
                profile => profile.LaboratoryTechnologistId == currentUserId.Value,
                cancellationToken);

        if (technologist is null)
        {
            return new NotFoundObjectResult("Laboratory technologist not found.");
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

        var response = await GetLaboratoryTechnologistResponseAsync(technologist.LaboratoryTechnologistId, cancellationToken);
        return response is null
            ? new NotFoundObjectResult("Laboratory technologist not found.")
            : new OkObjectResult(response);
    }

    public async Task<IActionResult> DeleteAsync(
        Guid laboratoryTechnologistId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var technologist = await _dbContext.LaboratoryTechnologists
            .Include(profile => profile.User)
            .FirstOrDefaultAsync(
                profile => profile.LaboratoryTechnologistId == laboratoryTechnologistId,
                cancellationToken);

        if (technologist is null)
        {
            return new NotFoundObjectResult("Laboratory technologist not found.");
        }

        if (!await CanCurrentUserManageLaboratoryTechnologistAsync(technologist, user, cancellationToken))
        {
            return ForbiddenAction("You are not allowed to delete this laboratory technologist.");
        }

        var deleteResult = await _userManager.DeleteAsync(technologist.User);
        if (!deleteResult.Succeeded)
        {
            return new BadRequestObjectResult(string.Join(" ", deleteResult.Errors.Select(error => error.Description)));
        }

        return new NoContentResult();
    }

    private IQueryable<LaboratoryTechnologistResponse> BuildLaboratoryTechnologistResponseQuery()
    {
        return _dbContext.LaboratoryTechnologists
            .AsNoTracking()
            .Select(technologist => new LaboratoryTechnologistResponse
            {
                LaboratoryTechnologistId = technologist.LaboratoryTechnologistId,
                Name = technologist.User.Name,
                Email = technologist.User.Email ?? string.Empty,
                PhoneNumber = technologist.User.PhoneNumber,
                Address = technologist.User.Address,
                DateOfBirth = technologist.User.DateOfBirth,
                ClinicId = technologist.User.ClinicId,
                ClinicName = technologist.User.Clinic != null ? technologist.User.Clinic.Name : null,
                ProfessionalLicenseNumber = technologist.ProfessionalLicenseNumber,
                EmailConfirmed = technologist.User.EmailConfirmed
            });
    }

    private async Task<LaboratoryTechnologistResponse?> GetLaboratoryTechnologistResponseAsync(
        Guid laboratoryTechnologistId,
        CancellationToken cancellationToken)
    {
        return await BuildLaboratoryTechnologistResponseQuery()
            .FirstOrDefaultAsync(
                technologist => technologist.LaboratoryTechnologistId == laboratoryTechnologistId,
                cancellationToken);
    }

    private async Task<bool> CanCurrentUserManageLaboratoryTechnologistAsync(
        LaboratoryTechnologist technologist,
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

    private static ActionResult<List<LaboratoryTechnologistResponse>> ForbiddenList(string message)
    {
        return new ObjectResult(message)
        {
            StatusCode = 403
        };
    }

    private static ActionResult<LaboratoryTechnologistResponse> ForbiddenSingle(string message)
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
