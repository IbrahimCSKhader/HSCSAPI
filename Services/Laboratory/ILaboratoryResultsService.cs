using System.Security.Claims;
using HSCSAPI.DTOs.Laboratory;
using Microsoft.AspNetCore.Mvc;

namespace HSCSAPI.Services.Laboratory;

public interface ILaboratoryResultsService
{
    Task<ActionResult<List<LabTestTemplateResponse>>> GetTemplatesAsync(
        bool activeOnly,
        CancellationToken cancellationToken = default);

    Task<ActionResult<LabTestTemplateResponse>> GetTemplateAsync(
        string templateCode,
        CancellationToken cancellationToken = default);

    Task<ActionResult<LabWorkItemsResponse>> GetMyWorkItemsAsync(
        string? status,
        int page,
        int pageSize,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<LabTestResultResponse>> CreateResultAsync(
        Guid labTestRequestId,
        CreateLabTestResultRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<LabTestResultResponse>> GetResultAsync(
        Guid labTestResultId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<LabResultPdfResponse>> GeneratePdfAsync(
        Guid labTestResultId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<IActionResult> DownloadPdfAsync(
        Guid labTestResultId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);
}
