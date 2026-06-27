using System.Security.Claims;
using HSCSAPI.Data;
using HSCSAPI.DTOs.Clinic;
using HSCSAPI.Models.Clinics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Services.Clinics;

public class ClinicsService : IClinicsService
{
    private readonly AppDbContext _dbContext;

    public ClinicsService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ActionResult<List<ClinicResponse>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var clinics = await _dbContext.Clinics
            .AsNoTracking()
            .Select(c => new ClinicResponse
            {
                ClinicId = c.ClinicId,
                Name = c.Name,
                Address = c.Address,
                CreatedBySuperAdminUserId = c.CreatedBySuperAdminUserId,
                AdminSecretaryId = c.AdminSecretaryId,
                IsActive = c.IsActive
            })
            .ToListAsync(cancellationToken);

        return new OkObjectResult(clinics);
    }

    public async Task<ActionResult<ClinicResponse>> CreateAsync(
        CreateClinicRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var superAdminUserId = GetCurrentUserId(user);
        if (superAdminUserId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        if (request.AdminSecretaryId.HasValue)
        {
            var adminSecretaryValidation = await ValidateAdminSecretaryAsync(request.AdminSecretaryId.Value, cancellationToken);
            if (!adminSecretaryValidation.Success)
            {
                return new BadRequestObjectResult(adminSecretaryValidation.Message);
            }
        }

        var clinic = new Clinic
        {
            Name = request.Name,
            Address = request.Address,
            CreatedBySuperAdminUserId = superAdminUserId.Value,
            AdminSecretaryId = request.AdminSecretaryId
        };

        _dbContext.Clinics.Add(clinic);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (request.AdminSecretaryId.HasValue)
        {
            var adminSecretary = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Id == request.AdminSecretaryId.Value, cancellationToken);

            if (adminSecretary != null)
            {
                adminSecretary.ClinicId = clinic.ClinicId;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        return new OkObjectResult(ToResponse(clinic));
    }

    public async Task<ActionResult<ClinicResponse>> UpdateAsync(
        Guid clinicId,
        UpdateClinicRequest request,
        CancellationToken cancellationToken = default)
    {
        var clinic = await _dbContext.Clinics.FirstOrDefaultAsync(c => c.ClinicId == clinicId, cancellationToken);
        if (clinic == null)
        {
            return new NotFoundObjectResult("Clinic not found.");
        }

        if (request.AdminSecretaryId.HasValue)
        {
            var adminSecretaryValidation = await ValidateAdminSecretaryForClinicAsync(
                request.AdminSecretaryId.Value,
                clinicId,
                cancellationToken);

            if (!adminSecretaryValidation.Success)
            {
                return new BadRequestObjectResult(adminSecretaryValidation.Message);
            }
        }

        clinic.Name = request.Name;
        clinic.Address = request.Address;
        clinic.AdminSecretaryId = request.AdminSecretaryId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (request.AdminSecretaryId.HasValue)
        {
            var adminSecretary = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Id == request.AdminSecretaryId.Value, cancellationToken);

            if (adminSecretary != null && adminSecretary.ClinicId != clinicId)
            {
                adminSecretary.ClinicId = clinicId;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        return new OkObjectResult(ToResponse(clinic));
    }

    public Task<IActionResult> DeactivateAsync(Guid clinicId, CancellationToken cancellationToken = default)
    {
        return SetActiveStateAsync(clinicId, false, cancellationToken);
    }

    public Task<IActionResult> ActivateAsync(Guid clinicId, CancellationToken cancellationToken = default)
    {
        return SetActiveStateAsync(clinicId, true, cancellationToken);
    }

    public async Task<ActionResult<ClinicResponse>> UpdateMyClinicAsync(
        UpdateMyClinicRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId(user);
        if (userId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var clinic = await _dbContext.Clinics
            .FirstOrDefaultAsync(c => c.AdminSecretaryId == userId.Value, cancellationToken);

        if (clinic == null)
        {
            return Forbidden("This secretary is not assigned as clinic admin.");
        }

        clinic.Name = request.Name;
        clinic.Address = request.Address;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new OkObjectResult(ToResponse(clinic));
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

    private static ActionResult<ClinicResponse> Forbidden(string message)
    {
        return new ObjectResult(message)
        {
            StatusCode = 403
        };
    }

    private static ClinicResponse ToResponse(Clinic clinic)
    {
        return new ClinicResponse
        {
            ClinicId = clinic.ClinicId,
            Name = clinic.Name,
            Address = clinic.Address,
            CreatedBySuperAdminUserId = clinic.CreatedBySuperAdminUserId,
            AdminSecretaryId = clinic.AdminSecretaryId,
            IsActive = clinic.IsActive
        };
    }

    private async Task<IActionResult> SetActiveStateAsync(
        Guid clinicId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var clinic = await _dbContext.Clinics
            .FirstOrDefaultAsync(c => c.ClinicId == clinicId, cancellationToken);

        if (clinic is null)
        {
            return new NotFoundObjectResult("Clinic not found.");
        }

        if (clinic.IsActive == isActive)
        {
            return new OkObjectResult(ToResponse(clinic));
        }

        clinic.IsActive = isActive;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new OkObjectResult(ToResponse(clinic));
    }

    private async Task<(bool Success, string Message)> ValidateAdminSecretaryAsync(Guid secretaryId, CancellationToken cancellationToken)
    {
        var secretaryUser = await _dbContext.Secretaries
            .AsNoTracking()
            .Select(s => new
            {
                s.SecretaryId,
                s.User.ClinicId,
                s.User.IsActive,
                ManagedClinicId = s.ManagedClinic != null ? s.ManagedClinic.ClinicId : (Guid?)null
            })
            .FirstOrDefaultAsync(s => s.SecretaryId == secretaryId, cancellationToken);

        if (secretaryUser == null)
        {
            return (false, "Admin secretary not found.");
        }

        if (!secretaryUser.IsActive)
        {
            return (false, "Admin secretary account is inactive.");
        }

        if (secretaryUser.ManagedClinicId.HasValue)
        {
            return (false, "This secretary is already assigned as admin to another clinic.");
        }

        if (secretaryUser.ClinicId.HasValue)
        {
            return (false, "Assign only a secretary who is not already attached to another clinic.");
        }

        return (true, string.Empty);
    }

    private async Task<(bool Success, string Message)> ValidateAdminSecretaryForClinicAsync(
        Guid secretaryId,
        Guid clinicId,
        CancellationToken cancellationToken)
    {
        var secretaryUser = await _dbContext.Secretaries
            .AsNoTracking()
            .Select(s => new
            {
                s.SecretaryId,
                s.User.ClinicId,
                s.User.IsActive,
                ManagedClinicId = s.ManagedClinic != null ? s.ManagedClinic.ClinicId : (Guid?)null
            })
            .FirstOrDefaultAsync(s => s.SecretaryId == secretaryId, cancellationToken);

        if (secretaryUser == null)
        {
            return (false, "Admin secretary not found.");
        }

        if (!secretaryUser.IsActive)
        {
            return (false, "Admin secretary account is inactive.");
        }

        if (secretaryUser.ManagedClinicId.HasValue && secretaryUser.ManagedClinicId != clinicId)
        {
            return (false, "This secretary is already assigned as admin to another clinic.");
        }

        if (secretaryUser.ClinicId.HasValue && secretaryUser.ClinicId != clinicId)
        {
            return (false, "Assign the secretary to this clinic first before making them clinic admin.");
        }

        return (true, string.Empty);
    }
}
