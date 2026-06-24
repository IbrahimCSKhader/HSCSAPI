using System.Text.Json;
using HSCSAPI.DTOs.Standards;
using HSCSAPI.Services.Standards;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSCSAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class StandardsController : ControllerBase
{
    private readonly IStandardsService _standardsService;
    private readonly IRxNormService _rxNormService;

    public StandardsController(IStandardsService standardsService, IRxNormService rxNormService)
    {
        _standardsService = standardsService;
        _rxNormService = rxNormService;
    }

    [HttpGet("loinc")]
    public async Task<ActionResult<StandardPagedResponse<LoincCodeResponse>>> SearchLoinc(
        [FromQuery] string? query,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromQuery] bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        return await _standardsService.SearchLoincAsync(query, page, pageSize, activeOnly, cancellationToken);
    }

    [HttpGet("loinc/{code}")]
    public async Task<ActionResult<LoincCodeResponse>> GetLoincByCode(
        string code,
        CancellationToken cancellationToken)
    {
        var result = await _standardsService.GetLoincByCodeAsync(code, cancellationToken);
        return result is null ? NotFound("LOINC code not found.") : Ok(result);
    }

    [HttpGet("lab-tests")]
    public async Task<ActionResult<StandardPagedResponse<LoincCodeResponse>>> SearchLabTests(
        [FromQuery] string? query,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken)
    {
        return await _standardsService.SearchLoincAsync(query, page, pageSize, activeOnly: true, cancellationToken);
    }

    [HttpGet("icd10")]
    public async Task<ActionResult<StandardPagedResponse<Icd10CodeResponse>>> SearchIcd10(
        [FromQuery] string? query,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken)
    {
        return await _standardsService.SearchIcd10Async(query, page, pageSize, cancellationToken);
    }

    [HttpGet("icd10/{code}")]
    public async Task<ActionResult<Icd10CodeResponse>> GetIcd10ByCode(
        string code,
        CancellationToken cancellationToken)
    {
        var result = await _standardsService.GetIcd10ByCodeAsync(code, cancellationToken);
        return result is null ? NotFound("ICD-10 code not found.") : Ok(result);
    }

    [HttpGet("radiology-playbook")]
    public async Task<ActionResult<StandardPagedResponse<RadiologyPlaybookResponse>>> SearchRadiologyPlaybook(
        [FromQuery] string? query,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken)
    {
        return await _standardsService.SearchRadiologyPlaybookAsync(query, page, pageSize, cancellationToken);
    }

    [HttpGet("radiology-playbook/{rpid}")]
    public async Task<ActionResult<RadiologyPlaybookResponse>> GetRadiologyPlaybookByRpid(
        string rpid,
        CancellationToken cancellationToken)
    {
        var result = await _standardsService.GetRadiologyPlaybookByRpidAsync(rpid, cancellationToken);
        return result is null ? NotFound("Radiology playbook code not found.") : Ok(result);
    }

    [HttpGet("imaging-types")]
    public async Task<ActionResult<List<ImagingTypeResponse>>> GetImagingTypes(
        [FromQuery] string? query,
        CancellationToken cancellationToken)
    {
        return await _standardsService.GetImagingTypesAsync(query, cancellationToken);
    }

    [HttpGet("search")]
    public async Task<ActionResult<StandardPagedResponse<StandardSearchItemResponse>>> SearchAll(
        [FromQuery] string? query,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken)
    {
        return await _standardsService.SearchAllAsync(query, page, pageSize, cancellationToken);
    }

    [HttpGet("rxnorm/rxcui")]
    public async Task<ActionResult<JsonElement>> FindRxCui(
        [FromQuery] string name,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest("name is required.");
        }

        return Ok(await _rxNormService.FindRxcuiByNameAsync(name, cancellationToken));
    }

    [HttpGet("rxnorm/drugs")]
    public async Task<ActionResult<JsonElement>> FindDrugs(
        [FromQuery] string name,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest("name is required.");
        }

        return Ok(await _rxNormService.FindDrugsAsync(name, cancellationToken));
    }

    [HttpGet("rxnorm/approximate")]
    public async Task<ActionResult<JsonElement>> FindApproximate(
        [FromQuery] string term,
        [FromQuery] int maxEntries,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return BadRequest("term is required.");
        }

        return Ok(await _rxNormService.FindApproximateAsync(term, maxEntries, cancellationToken));
    }

    [HttpGet("rxnorm/{rxcui}/properties")]
    public async Task<ActionResult<JsonElement>> GetRxNormProperties(
        string rxcui,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rxcui))
        {
            return BadRequest("rxcui is required.");
        }

        return Ok(await _rxNormService.GetPropertiesAsync(rxcui, cancellationToken));
    }

    [HttpGet("rxnorm/{rxcui}/related")]
    public async Task<ActionResult<JsonElement>> GetRxNormRelated(
        string rxcui,
        [FromQuery] string? tty,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rxcui))
        {
            return BadRequest("rxcui is required.");
        }

        return Ok(await _rxNormService.GetRelatedAsync(rxcui, tty, cancellationToken));
    }

    [HttpGet("rxnorm/version")]
    public async Task<ActionResult<JsonElement>> GetRxNormVersion(CancellationToken cancellationToken)
    {
        return Ok(await _rxNormService.GetVersionAsync(cancellationToken));
    }
}
