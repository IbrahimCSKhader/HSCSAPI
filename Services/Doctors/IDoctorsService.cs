using System.Security.Claims;
using HSCSAPI.DTOs.Doctor;
using HSCSAPI.DTOs.Appointment;
using Microsoft.AspNetCore.Mvc;

namespace HSCSAPI.Services.Doctors;

public interface IDoctorsService
{
    Task<ActionResult<List<AvailabilitySlotResponse>>> GetAvailabilityAsync(Guid doctorId, DateOnly date, CancellationToken cancellationToken = default);
    Task<ActionResult<List<DoctorResponse>>> GetAllAsync(
        Guid? clinicId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<List<DoctorResponse>>> GetByClinicAsync(
        Guid clinicId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<DoctorResponse>> GetByIdAsync(
        Guid doctorId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<DoctorResponse>> GetMyProfileAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<DoctorDashboardResponse>> GetMyDashboardAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<DoctorAppointmentsScheduleResponse>> GetMyAppointmentsScheduleAsync(
        DateOnly? fromDate,
        DateOnly? toDate,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<DoctorAppointmentDetailResponse>> GetMyAppointmentDetailAsync(
        Guid appointmentId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<DoctorMedicalRecordsResponse>> GetMyMedicalRecordsAsync(
        string? patientId,
        Guid? clinicId,
        string? type,
        string? query,
        int page,
        int pageSize,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<DoctorMedicalRecordDetailResponse>> GetMyMedicalRecordAsync(
        Guid medicalFileId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<IActionResult> DownloadMyMedicalRecordAsync(
        Guid medicalFileId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<DoctorResponse>> UpdateAsync(
        Guid doctorId,
        UpdateDoctorRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<DoctorResponse>> UpdateMyProfileAsync(
        UpdateMyDoctorProfileRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<ChangeDoctorPasswordResponse>> ChangeMyPasswordAsync(
        ChangeDoctorPasswordRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<IActionResult> DeactivateAsync(
        Guid doctorId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<IActionResult> ActivateAsync(
        Guid doctorId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);
}
