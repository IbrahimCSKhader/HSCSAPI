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
                AdminSecretaryId = c.AdminSecretaryId
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
            var secretaryExists = await _dbContext.Secretaries
                .AnyAsync(s => s.SecretaryId == request.AdminSecretaryId.Value, cancellationToken);

            if (!secretaryExists)
            {
                return new BadRequestObjectResult("Admin secretary not found.");
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
            var secretaryExists = await _dbContext.Secretaries
                .AnyAsync(s => s.SecretaryId == request.AdminSecretaryId.Value, cancellationToken);

            if (!secretaryExists)
            {
                return new BadRequestObjectResult("Admin secretary not found.");
            }
        }

        clinic.Name = request.Name;
        clinic.Address = request.Address;
        clinic.AdminSecretaryId = request.AdminSecretaryId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new OkObjectResult(ToResponse(clinic));
    }

    public async Task<IActionResult> DeleteAsync(Guid clinicId, CancellationToken cancellationToken = default)
    {
        var clinic = await _dbContext.Clinics.FirstOrDefaultAsync(c => c.ClinicId == clinicId, cancellationToken);
        if (clinic == null)
        {
            return new NotFoundObjectResult("Clinic not found.");
        }

        _dbContext.Clinics.Remove(clinic);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new NoContentResult();
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
            AdminSecretaryId = clinic.AdminSecretaryId
        };
    }
}
