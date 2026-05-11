using System.Security.Claims;
using HSCSAPI.Data;
using HSCSAPI.DTOs.Doctor;
using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Identity;
using HSCSAPI.Models.Profiles;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Services.Doctors;

public class DoctorsService : IDoctorsService
{
    public const string SuperAdminOrSecretaryRoles = nameof(UserSystemRole.SuperAdmin) + "," + nameof(UserSystemRole.Secretary);
    public const string SuperAdminOrSecretaryOrDoctorRoles = SuperAdminOrSecretaryRoles + "," + nameof(UserSystemRole.Doctor);

    private readonly AppDbContext _dbContext;
    private readonly UserManager<User> _userManager;

    public DoctorsService(
        AppDbContext dbContext,
        UserManager<User> userManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
    }

    public async Task<ActionResult<List<DoctorResponse>>> GetAllAsync(
        Guid? clinicId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var query = BuildDoctorResponseQuery();

        if (user.IsInRole(nameof(UserSystemRole.SuperAdmin)))
        {
            if (clinicId.HasValue)
            {
                query = query.Where(doctor => doctor.ClinicId == clinicId.Value);
            }

            var doctors = await query.ToListAsync(cancellationToken);
            return new OkObjectResult(doctors);
        }

        var secretaryClinicId = await GetCurrentSecretaryClinicIdAsync(user, cancellationToken);
        if (secretaryClinicId is null)
        {
            return ForbiddenList("This secretary is not assigned to any clinic.");
        }

        if (clinicId.HasValue && clinicId.Value != secretaryClinicId.Value)
        {
            return ForbiddenList("You are not allowed to access doctors outside your clinic.");
        }

        var clinicDoctors = await query
            .Where(doctor => doctor.ClinicId == secretaryClinicId.Value)
            .ToListAsync(cancellationToken);

        return new OkObjectResult(clinicDoctors);
    }

    public async Task<ActionResult<List<DoctorResponse>>> GetByClinicAsync(
        Guid clinicId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var clinicExists = await _dbContext.Clinics
            .AsNoTracking()
            .AnyAsync(clinic => clinic.ClinicId == clinicId, cancellationToken);

        if (!clinicExists)
        {
            return new NotFoundObjectResult("Clinic not found.");
        }

        if (!await CanCurrentUserAccessClinicAsync(clinicId, user, cancellationToken))
        {
            return ForbiddenList("You are not allowed to access doctors in this clinic.");
        }

        var doctors = await BuildDoctorResponseQuery()
            .Where(doctor => doctor.ClinicId == clinicId)
            .ToListAsync(cancellationToken);

        return new OkObjectResult(doctors);
    }

    public async Task<ActionResult<DoctorResponse>> GetByIdAsync(
        Guid doctorId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId(user);
        if (currentUserId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        if (user.IsInRole(nameof(UserSystemRole.Doctor)) && currentUserId.Value != doctorId)
        {
            return ForbiddenSingle("You are not allowed to access another doctor profile.");
        }

        var doctor = await GetDoctorResponseAsync(doctorId, cancellationToken);
        if (doctor is null)
        {
            return new NotFoundObjectResult("Doctor not found.");
        }

        if (user.IsInRole(nameof(UserSystemRole.SuperAdmin)) || user.IsInRole(nameof(UserSystemRole.Doctor)))
        {
            return new OkObjectResult(doctor);
        }

        if (!doctor.ClinicId.HasValue || !await CanCurrentUserAccessClinicAsync(doctor.ClinicId.Value, user, cancellationToken))
        {
            return ForbiddenSingle("You are not allowed to access doctors outside your clinic.");
        }

        return new OkObjectResult(doctor);
    }

    public async Task<ActionResult<DoctorResponse>> GetMyProfileAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId(user);
        if (currentUserId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var doctor = await GetDoctorResponseAsync(currentUserId.Value, cancellationToken);
        return doctor is null
            ? new NotFoundObjectResult("Doctor not found.")
            : new OkObjectResult(doctor);
    }

    public async Task<ActionResult<DoctorResponse>> UpdateAsync(
        Guid doctorId,
        UpdateDoctorRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return new BadRequestObjectResult("Name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return new BadRequestObjectResult("Email is required.");
        }

        if (request.ClinicId == Guid.Empty)
        {
            return new BadRequestObjectResult("ClinicId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.ProfessionalLicenseNumber))
        {
            return new BadRequestObjectResult("ProfessionalLicenseNumber is required.");
        }

        var doctor = await _dbContext.Doctors
            .Include(profile => profile.User)
            .FirstOrDefaultAsync(profile => profile.DoctorId == doctorId, cancellationToken);

        if (doctor is null)
        {
            return new NotFoundObjectResult("Doctor not found.");
        }

        if (!await CanCurrentUserManageDoctorAsync(doctor, user, cancellationToken))
        {
            return ForbiddenSingle("You are not allowed to manage this doctor.");
        }

        if (!await CanCurrentUserAccessClinicAsync(request.ClinicId, user, cancellationToken))
        {
            return ForbiddenSingle("You are not allowed to assign the doctor to this clinic.");
        }

        var clinicExists = await _dbContext.Clinics
            .AsNoTracking()
            .AnyAsync(clinic => clinic.ClinicId == request.ClinicId, cancellationToken);

        if (!clinicExists)
        {
            return new NotFoundObjectResult("Clinic not found.");
        }

        var normalizedEmail = NormalizeEmail(request.Email);
        var normalizedLookup = _userManager.NormalizeEmail(normalizedEmail);
        var emailAlreadyRegistered = await _userManager.Users
            .AsNoTracking()
            .AnyAsync(
                existingUser => existingUser.Id != doctorId
                    && existingUser.NormalizedEmail == normalizedLookup,
                cancellationToken);

        if (emailAlreadyRegistered)
        {
            return new BadRequestObjectResult("Email already registered.");
        }

        var normalizedLicenseNumber = request.ProfessionalLicenseNumber.Trim();
        var licenseNumberExists = await _dbContext.Doctors
            .AsNoTracking()
            .AnyAsync(
                existingDoctor => existingDoctor.DoctorId != doctorId
                    && existingDoctor.ProfessionalLicenseNumber == normalizedLicenseNumber,
                cancellationToken);

        if (licenseNumberExists)
        {
            return new BadRequestObjectResult("Professional license number is already registered.");
        }

        doctor.User.Name = request.Name.Trim();
        doctor.User.Email = normalizedEmail;
        doctor.User.UserName = normalizedEmail;
        doctor.User.PhoneNumber = NormalizeOptional(request.PhoneNumber);
        doctor.User.Address = NormalizeOptional(request.Address);
        doctor.User.DateOfBirth = request.DateOfBirth;
        doctor.User.ClinicId = request.ClinicId;
        doctor.ProfessionalLicenseNumber = normalizedLicenseNumber;

        var updateResult = await _userManager.UpdateAsync(doctor.User);
        if (!updateResult.Succeeded)
        {
            return new BadRequestObjectResult(string.Join(" ", updateResult.Errors.Select(error => error.Description)));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = await GetDoctorResponseAsync(doctorId, cancellationToken);
        return response is null
            ? new NotFoundObjectResult("Doctor not found.")
            : new OkObjectResult(response);
    }

    public async Task<ActionResult<DoctorResponse>> UpdateMyProfileAsync(
        UpdateMyDoctorProfileRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return new BadRequestObjectResult("Name is required.");
        }

        var currentUserId = GetCurrentUserId(user);
        if (currentUserId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var doctor = await _dbContext.Doctors
            .Include(profile => profile.User)
            .FirstOrDefaultAsync(profile => profile.DoctorId == currentUserId.Value, cancellationToken);

        if (doctor is null)
        {
            return new NotFoundObjectResult("Doctor not found.");
        }

        doctor.User.Name = request.Name.Trim();
        doctor.User.PhoneNumber = NormalizeOptional(request.PhoneNumber);
        doctor.User.Address = NormalizeOptional(request.Address);
        doctor.User.DateOfBirth = request.DateOfBirth;

        var updateResult = await _userManager.UpdateAsync(doctor.User);
        if (!updateResult.Succeeded)
        {
            return new BadRequestObjectResult(string.Join(" ", updateResult.Errors.Select(error => error.Description)));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = await GetDoctorResponseAsync(doctor.DoctorId, cancellationToken);
        return response is null
            ? new NotFoundObjectResult("Doctor not found.")
            : new OkObjectResult(response);
    }

    public async Task<IActionResult> DeleteAsync(
        Guid doctorId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var doctor = await _dbContext.Doctors
            .Include(profile => profile.User)
            .FirstOrDefaultAsync(profile => profile.DoctorId == doctorId, cancellationToken);

        if (doctor is null)
        {
            return new NotFoundObjectResult("Doctor not found.");
        }

        if (!await CanCurrentUserManageDoctorAsync(doctor, user, cancellationToken))
        {
            return ForbiddenAction("You are not allowed to delete this doctor.");
        }

        var blockers = await GetDeleteBlockersAsync(doctorId, cancellationToken);
        if (blockers.Count > 0)
        {
            return new BadRequestObjectResult(
                $"Cannot delete doctor because related {string.Join(", ", blockers)} exist.");
        }

        var deleteResult = await _userManager.DeleteAsync(doctor.User);
        if (!deleteResult.Succeeded)
        {
            return new BadRequestObjectResult(string.Join(" ", deleteResult.Errors.Select(error => error.Description)));
        }

        return new NoContentResult();
    }

    private IQueryable<DoctorResponse> BuildDoctorResponseQuery()
    {
        return _dbContext.Doctors
            .AsNoTracking()
            .Select(doctor => new DoctorResponse
            {
                DoctorId = doctor.DoctorId,
                Name = doctor.User.Name,
                Email = doctor.User.Email ?? string.Empty,
                PhoneNumber = doctor.User.PhoneNumber,
                Address = doctor.User.Address,
                DateOfBirth = doctor.User.DateOfBirth,
                ClinicId = doctor.User.ClinicId,
                ClinicName = doctor.User.Clinic != null ? doctor.User.Clinic.Name : null,
                ProfessionalLicenseNumber = doctor.ProfessionalLicenseNumber,
                EmailConfirmed = doctor.User.EmailConfirmed
            });
    }

    private async Task<DoctorResponse?> GetDoctorResponseAsync(Guid doctorId, CancellationToken cancellationToken)
    {
        return await BuildDoctorResponseQuery()
            .FirstOrDefaultAsync(doctor => doctor.DoctorId == doctorId, cancellationToken);
    }

    private async Task<bool> CanCurrentUserManageDoctorAsync(
        Doctor doctor,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (user.IsInRole(nameof(UserSystemRole.SuperAdmin)))
        {
            return true;
        }

        if (!doctor.User.ClinicId.HasValue)
        {
            return false;
        }

        return await CanCurrentUserAccessClinicAsync(doctor.User.ClinicId.Value, user, cancellationToken);
    }

    private async Task<bool> CanCurrentUserAccessClinicAsync(
        Guid clinicId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (user.IsInRole(nameof(UserSystemRole.SuperAdmin)))
        {
            return true;
        }

        var secretaryClinicId = await GetCurrentSecretaryClinicIdAsync(user, cancellationToken);
        return secretaryClinicId.HasValue && secretaryClinicId.Value == clinicId;
    }

    private async Task<Guid?> GetCurrentSecretaryClinicIdAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId(user);
        if (currentUserId is null)
        {
            return null;
        }

        return await _dbContext.Secretaries
            .AsNoTracking()
            .Where(secretary => secretary.SecretaryId == currentUserId.Value)
            .Select(secretary => secretary.User.ClinicId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<List<string>> GetDeleteBlockersAsync(Guid doctorId, CancellationToken cancellationToken)
    {
        var blockers = new List<string>();

        if (await _dbContext.AvailabilitySlots.AnyAsync(slot => slot.DoctorId == doctorId, cancellationToken))
        {
            blockers.Add("availability slots");
        }

        if (await _dbContext.Appointments.AnyAsync(appointment => appointment.DoctorId == doctorId, cancellationToken))
        {
            blockers.Add("appointments");
        }

        if (await _dbContext.Reminders.AnyAsync(reminder => reminder.DoctorId == doctorId, cancellationToken))
        {
            blockers.Add("reminders");
        }

        if (await _dbContext.MedicalFiles.AnyAsync(file => file.UploadedByDoctorId == doctorId, cancellationToken))
        {
            blockers.Add("medical files");
        }

        return blockers;
    }

    private static Guid? GetCurrentUserId(ClaimsPrincipal user)
    {
        var claim = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (claim is null)
        {
            return null;
        }

        return Guid.TryParse(claim, out var userId) ? userId : null;
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static ActionResult<List<DoctorResponse>> ForbiddenList(string message)
    {
        return new ObjectResult(message)
        {
            StatusCode = 403
        };
    }

    private static ActionResult<DoctorResponse> ForbiddenSingle(string message)
    {
        return new ObjectResult(message)
        {
            StatusCode = 403
        };
    }

    private static IActionResult ForbiddenAction(string message)
    {
        return new ObjectResult(message)
        {
            StatusCode = 403
        };
    }
}
