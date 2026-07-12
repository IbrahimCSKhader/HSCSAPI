using System.Security.Claims;
using HSCSAPI.DTOs.Secretary;
using HSCSAPI.DTOs.Appointment;
using HSCSAPI.DTOs.Common;
using Microsoft.AspNetCore.Mvc;

namespace HSCSAPI.Services.Secretaries;

public interface ISecretariesService
{
    Task<ActionResult<SecretaryDashboardResponse>> GetDashboardAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);
    Task<ActionResult<SecretaryResponse>> GetMyProfileAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);
    Task<ActionResult<SecretaryResponse>> UpdateMyProfileAsync(UpdateSecretaryRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default);
    Task<ActionResult<ChangePasswordResponse>> ChangeMyPasswordAsync(ChangePasswordRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default);
    Task<ActionResult<List<SecretaryResponse>>> GetAllAsync(Guid? clinicId, CancellationToken cancellationToken = default);
    Task<ActionResult<List<SecretaryResponse>>> GetAvailableAsync(CancellationToken cancellationToken = default);
    Task<ActionResult<List<SecretaryResponse>>> GetByClinicAsync(Guid clinicId, ClaimsPrincipal user, CancellationToken cancellationToken = default);
    Task<ActionResult<List<SecretaryResponse>>> GetMyClinicSecretariesAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);
    Task<ActionResult<SecretaryResponse>> AssignToClinicAsync(Guid secretaryId, AssignSecretaryToClinicRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default);
    Task<ActionResult<SecretaryResponse>> RemoveFromClinicAsync(Guid secretaryId, ClaimsPrincipal user, CancellationToken cancellationToken = default);
    Task<ActionResult<SecretaryResponse>> UpdateInClinicAsync(Guid clinicId, Guid secretaryId, UpdateSecretaryRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default);
    Task<IActionResult> DeactivateInClinicAsync(Guid clinicId, Guid secretaryId, ClaimsPrincipal user, CancellationToken cancellationToken = default);
    Task<IActionResult> ActivateInClinicAsync(Guid clinicId, Guid secretaryId, ClaimsPrincipal user, CancellationToken cancellationToken = default);
    Task<ActionResult<List<AvailabilitySlotResponse>>> GetDoctorAvailabilitySlotsAsync(Guid doctorId, DateOnly? fromDate, DateOnly? toDate, ClaimsPrincipal user, CancellationToken cancellationToken = default);
    Task<ActionResult<AvailabilitySlotResponse>> CreateDoctorAvailabilitySlotAsync(Guid doctorId, CreateAvailabilitySlotRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default);
    Task<ActionResult<AvailabilitySlotResponse>> UpdateDoctorAvailabilitySlotAsync(Guid doctorId, Guid slotId, UpdateAvailabilitySlotRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default);
    Task<IActionResult> DeleteDoctorAvailabilitySlotAsync(Guid doctorId, Guid slotId, ClaimsPrincipal user, CancellationToken cancellationToken = default);
    Task<ActionResult<List<SecretaryReportResponse>>> GetReportsAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);
    Task<ActionResult<SecretaryReportOptionsResponse>> GetReportOptionsAsync(CancellationToken cancellationToken = default);
    Task<ActionResult<SecretaryReportResponse>> GenerateReportAsync(GenerateSecretaryReportRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default);
    Task<IActionResult> DeleteReportAsync(Guid reportId, ClaimsPrincipal user, CancellationToken cancellationToken = default);
    Task<SecretaryReportDownload> DownloadReportAsync(Guid reportId, Guid fileId, ClaimsPrincipal user, CancellationToken cancellationToken = default);
}

public sealed record SecretaryReportDownload(string PhysicalPath, string ContentType, string FileName);
