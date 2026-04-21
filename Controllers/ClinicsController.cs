using System.Security.Claims;
using HSCSAPI.Data;
using HSCSAPI.DTOs.Clinic;
using HSCSAPI.Models.Clinics;
using HSCSAPI.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClinicsController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public ClinicsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    // [Authorize(Roles = nameof(UserSystemRole.SuperAdmin))]
    public async Task<ActionResult<List<ClinicResponse>>> GetAll(CancellationToken cancellationToken)
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

        return Ok(clinics);
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserSystemRole.SuperAdmin))]
    public async Task<ActionResult<ClinicResponse>> Create([FromBody] CreateClinicRequest request, CancellationToken cancellationToken)
    {
        var superAdminUserId = GetCurrentUserId();
        if (superAdminUserId is null)
        {
            return Unauthorized("Invalid token.");
        }

        if (request.AdminSecretaryId.HasValue)
        {
            var secretaryExists = await _dbContext.Secretaries
                .AnyAsync(s => s.SecretaryId == request.AdminSecretaryId.Value, cancellationToken);

            if (!secretaryExists)
            {
                return BadRequest("Admin secretary not found.");
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

        return Ok(ToResponse(clinic));
    }

    [HttpPut("{clinicId:guid}")]
    [Authorize(Roles = nameof(UserSystemRole.SuperAdmin))]
    public async Task<ActionResult<ClinicResponse>> Update(Guid clinicId, [FromBody] UpdateClinicRequest request, CancellationToken cancellationToken)
    {
        var clinic = await _dbContext.Clinics.FirstOrDefaultAsync(c => c.ClinicId == clinicId, cancellationToken);
        if (clinic == null)
        {
            return NotFound("Clinic not found.");
        }

        if (request.AdminSecretaryId.HasValue)
        {
            var secretaryExists = await _dbContext.Secretaries
                .AnyAsync(s => s.SecretaryId == request.AdminSecretaryId.Value, cancellationToken);

            if (!secretaryExists)
            {
                return BadRequest("Admin secretary not found.");
            }
        }

        clinic.Name = request.Name;
        clinic.Address = request.Address;
        clinic.AdminSecretaryId = request.AdminSecretaryId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(clinic));
    }

    [HttpDelete("{clinicId:guid}")]
    [Authorize(Roles = nameof(UserSystemRole.SuperAdmin))]
    public async Task<IActionResult> Delete(Guid clinicId, CancellationToken cancellationToken)
    {
        var clinic = await _dbContext.Clinics.FirstOrDefaultAsync(c => c.ClinicId == clinicId, cancellationToken);
        if (clinic == null)
        {
            return NotFound("Clinic not found.");
        }

        _dbContext.Clinics.Remove(clinic);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpPut("my-clinic")]
    [Authorize(Roles = nameof(UserSystemRole.Secretary))]
    public async Task<ActionResult<ClinicResponse>> UpdateMyClinic([FromBody] UpdateMyClinicRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized("Invalid token.");
        }

        var clinic = await _dbContext.Clinics
            .FirstOrDefaultAsync(c => c.AdminSecretaryId == userId.Value, cancellationToken);

        if (clinic == null)
        {
            return Forbid("This secretary is not assigned as clinic admin.");
        }

        clinic.Name = request.Name;
        clinic.Address = request.Address;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(clinic));
    }

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (claim is null)
        {
            return null;
        }

        return Guid.TryParse(claim, out var userId) ? userId : null;
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
