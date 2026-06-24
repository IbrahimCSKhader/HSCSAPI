using HSCSAPI.DTOs.Doctor;
using HSCSAPI.DTOs.MedicalFiles;
using HSCSAPI.Models.Enums;
using HSCSAPI.Services.Doctors;
using HSCSAPI.Services.MedicalFiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSCSAPI.Controllers;

[ApiController]
[Authorize(Roles = nameof(UserSystemRole.Doctor))]
[Route("api/Doctors/me/medical-file-uploads")]
public class DoctorMedicalFileUploadsController : ControllerBase
{
    private readonly IMedicalFileUploadsService _medicalFileUploadsService;
    private readonly IDoctorsService _doctorsService;

    public DoctorMedicalFileUploadsController(
        IMedicalFileUploadsService medicalFileUploadsService,
        IDoctorsService doctorsService)
    {
        _medicalFileUploadsService = medicalFileUploadsService;
        _doctorsService = doctorsService;
    }

    [HttpGet("categories")]
    public async Task<ActionResult<List<MedicalFileUploadCategoryResponse>>> GetUploadCategories(
        CancellationToken cancellationToken)
    {
        return await _medicalFileUploadsService.GetUploadCategoriesAsync(cancellationToken);
    }

    [HttpGet]
    public async Task<ActionResult<DoctorMedicalRecordsResponse>> GetMyUploadHistory(
        [FromQuery] string? patientId,
        [FromQuery] Guid? clinicId,
        [FromQuery] string? type,
        [FromQuery] string? query,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken)
    {
        return await _doctorsService.GetMyMedicalRecordsAsync(
            patientId,
            clinicId,
            type,
            query,
            page,
            pageSize,
            User,
            cancellationToken);
    }

    [HttpPost]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<ActionResult<MedicalFileUploadResponse>> UploadMyMedicalFile(
        [FromForm] CreateMedicalFileUploadRequest request,
        CancellationToken cancellationToken)
    {
        return await _medicalFileUploadsService.UploadMyMedicalFileAsync(
            request,
            User,
            cancellationToken);
    }

    [HttpGet("{medicalFileId:guid}")]
    public async Task<ActionResult<DoctorMedicalRecordDetailResponse>> GetMyUploadedMedicalFile(
        Guid medicalFileId,
        CancellationToken cancellationToken)
    {
        return await _doctorsService.GetMyMedicalRecordAsync(
            medicalFileId,
            User,
            cancellationToken);
    }

    [HttpGet("{medicalFileId:guid}/download")]
    public async Task<IActionResult> DownloadMyUploadedMedicalFile(
        Guid medicalFileId,
        CancellationToken cancellationToken)
    {
        return await _doctorsService.DownloadMyMedicalRecordAsync(
            medicalFileId,
            User,
            cancellationToken);
    }
}
