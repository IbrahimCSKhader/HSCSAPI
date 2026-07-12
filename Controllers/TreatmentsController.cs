using HSCSAPI.Data;
using HSCSAPI.DTOs.Clinic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public class TreatmentsController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public TreatmentsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // last end point added
    [HttpGet("Clinics/{clinicId:guid}/treatments")]
    public async Task<ActionResult<List<TreatmentCatalogResponse>>> GetByClinicRoute(
        Guid clinicId,
        CancellationToken cancellationToken)
    {
        return await GetTreatmentsByClinicAsync(clinicId, cancellationToken);
    }

    // last end point added
    [HttpGet("Treatments")]
    public async Task<ActionResult<List<TreatmentCatalogResponse>>> GetByClinicQuery(
        [FromQuery] Guid clinicId,
        CancellationToken cancellationToken)
    {
        if (clinicId == Guid.Empty)
        {
            return BadRequest("clinicId is required.");
        }

        return await GetTreatmentsByClinicAsync(clinicId, cancellationToken);
    }

    private async Task<ActionResult<List<TreatmentCatalogResponse>>> GetTreatmentsByClinicAsync(
        Guid clinicId,
        CancellationToken cancellationToken)
    {
        var clinicExists = await _dbContext.Clinics
            .AsNoTracking()
            .AnyAsync(clinic => clinic.ClinicId == clinicId && clinic.IsActive, cancellationToken);
        if (!clinicExists)
        {
            return NotFound("Clinic not found or inactive.");
        }

        var treatments = await _dbContext.Doctors
            .AsNoTracking()
            .Where(doctor => doctor.User.ClinicId == clinicId && doctor.User.IsActive)
            .GroupBy(doctor => doctor.Specialty)
            .Select(group => new TreatmentCatalogResponse
            {
                TreatmentId = group.Key.ToString(),
                TreatmentName = group.Key.ToString(),
                DoctorSpecialty = group.Key.ToString(),
                ActiveDoctorsCount = group.Count()
            })
            .OrderBy(treatment => treatment.TreatmentName)
            .ToListAsync(cancellationToken);

        return Ok(treatments);
    }
}
