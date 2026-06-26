using HSCSAPI.DTOs.Laboratory;
using HSCSAPI.Models.Enums;
using HSCSAPI.Services.Laboratory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSCSAPI.Controllers;

[ApiController]
[Authorize(Roles = nameof(UserSystemRole.LaboratoryTechnologist))]
[Route("api/[controller]")]
public class LaboratoryTestsController : ControllerBase
{
    private readonly ILaboratoryResultsService _laboratoryResultsService;

    public LaboratoryTestsController(ILaboratoryResultsService laboratoryResultsService)
    {
        _laboratoryResultsService = laboratoryResultsService;
    }

    [HttpGet("templates")]
    public async Task<ActionResult<List<LabTestTemplateResponse>>> GetTemplates(
        [FromQuery] bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        return await _laboratoryResultsService.GetTemplatesAsync(activeOnly, cancellationToken);
    }

    [HttpGet("templates/{templateCode}")]
    public async Task<ActionResult<LabTestTemplateResponse>> GetTemplate(
        string templateCode,
        CancellationToken cancellationToken)
    {
        return await _laboratoryResultsService.GetTemplateAsync(templateCode, cancellationToken);
    }

    [HttpGet("my-requests")]
    public async Task<ActionResult<LabWorkItemsResponse>> GetMyRequests(
        [FromQuery] string? status,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken)
    {
        return await _laboratoryResultsService.GetMyWorkItemsAsync(
            status,
            page,
            pageSize,
            User,
            cancellationToken);
    }

    [HttpPost("my-requests/{labTestRequestId:guid}/results")]
    public async Task<ActionResult<LabTestResultResponse>> CreateResult(
        Guid labTestRequestId,
        [FromBody] CreateLabTestResultRequest request,
        CancellationToken cancellationToken)
    {
        return await _laboratoryResultsService.CreateResultAsync(
            labTestRequestId,
            request,
            User,
            cancellationToken);
    }

    [HttpGet("results/{labTestResultId:guid}")]
    public async Task<ActionResult<LabTestResultResponse>> GetResult(
        Guid labTestResultId,
        CancellationToken cancellationToken)
    {
        return await _laboratoryResultsService.GetResultAsync(labTestResultId, User, cancellationToken);
    }

    [HttpPost("results/{labTestResultId:guid}/pdf")]
    public async Task<ActionResult<LabResultPdfResponse>> GeneratePdf(
        Guid labTestResultId,
        CancellationToken cancellationToken)
    {
        return await _laboratoryResultsService.GeneratePdfAsync(labTestResultId, User, cancellationToken);
    }

    [HttpGet("results/{labTestResultId:guid}/pdf")]
    public async Task<IActionResult> DownloadPdf(
        Guid labTestResultId,
        CancellationToken cancellationToken)
    {
        return await _laboratoryResultsService.DownloadPdfAsync(labTestResultId, User, cancellationToken);
    }
}
