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

    // last end point added - already-added
    [HttpGet("templates")]
    public async Task<ActionResult<List<LabTestTemplateResponse>>> GetTemplates(
        [FromQuery] bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        return await _laboratoryResultsService.GetTemplatesAsync(activeOnly, cancellationToken);
    }

    // last end point added - already-added
    [HttpGet("templates/{templateCode}")]
    public async Task<ActionResult<LabTestTemplateResponse>> GetTemplate(
        string templateCode,
        CancellationToken cancellationToken)
    {
        return await _laboratoryResultsService.GetTemplateAsync(templateCode, cancellationToken);
    }

    // last end point added - already-added
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

    // last end point added
    [HttpGet("my-requests/{labTestRequestId:guid}")]
    public async Task<ActionResult<LabWorkItemResponse>> GetMyRequest(Guid labTestRequestId, CancellationToken cancellationToken)
    {
        return await _laboratoryResultsService.GetMyWorkItemAsync(labTestRequestId, User, cancellationToken);
    }

    // last end point added - already-added
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

    // last end point added
    [HttpPost("my-requests/{labTestRequestId:guid}/result-file")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<ActionResult<LabWorkItemResponse>> UploadResultFile(
        Guid labTestRequestId,
        [FromForm] UploadLabResultFileRequest request,
        CancellationToken cancellationToken)
    {
        return await _laboratoryResultsService.UploadResultFileAsync(
            labTestRequestId,
            request,
            User,
            cancellationToken);
    }

    // last end point added
    [HttpGet("my-requests/{labTestRequestId:guid}/result-file")]
    public async Task<IActionResult> DownloadResultFile(
        Guid labTestRequestId,
        CancellationToken cancellationToken)
    {
        return await _laboratoryResultsService.DownloadResultFileAsync(
            labTestRequestId,
            User,
            cancellationToken);
    }

    // last end point added - already-added
    [HttpGet("results/{labTestResultId:guid}")]
    public async Task<ActionResult<LabTestResultResponse>> GetResult(
        Guid labTestResultId,
        CancellationToken cancellationToken)
    {
        return await _laboratoryResultsService.GetResultAsync(labTestResultId, User, cancellationToken);
    }

    // last end point added - already-added
    [HttpPost("results/{labTestResultId:guid}/pdf")]
    public async Task<ActionResult<LabResultPdfResponse>> GeneratePdf(
        Guid labTestResultId,
        CancellationToken cancellationToken)
    {
        return await _laboratoryResultsService.GeneratePdfAsync(labTestResultId, User, cancellationToken);
    }

    // last end point added - already-added
    [HttpGet("results/{labTestResultId:guid}/pdf")]
    public async Task<IActionResult> DownloadPdf(
        Guid labTestResultId,
        CancellationToken cancellationToken)
    {
        return await _laboratoryResultsService.DownloadPdfAsync(labTestResultId, User, cancellationToken);
    }
}
