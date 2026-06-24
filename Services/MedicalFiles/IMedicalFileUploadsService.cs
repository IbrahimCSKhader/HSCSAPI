using System.Security.Claims;
using HSCSAPI.DTOs.MedicalFiles;
using Microsoft.AspNetCore.Mvc;

namespace HSCSAPI.Services.MedicalFiles;

public interface IMedicalFileUploadsService
{
    Task<ActionResult<List<MedicalFileUploadCategoryResponse>>> GetUploadCategoriesAsync(
        CancellationToken cancellationToken = default);

    Task<ActionResult<MedicalFileUploadResponse>> UploadMyMedicalFileAsync(
        CreateMedicalFileUploadRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);
}
