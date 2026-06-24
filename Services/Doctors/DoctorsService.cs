using System.Security.Claims;
using HSCSAPI.Data;
using HSCSAPI.DTOs.Doctor;
using HSCSAPI.Models.Enums;
using HSCSAPI.Models.Identity;
using HSCSAPI.Models.Profiles;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Services.Doctors;

public class DoctorsService : IDoctorsService
{
    public const string SuperAdminOrSecretaryRoles = nameof(UserSystemRole.SuperAdmin) + "," + nameof(UserSystemRole.Secretary);
    public const string SuperAdminOrSecretaryOrDoctorRoles = SuperAdminOrSecretaryRoles + "," + nameof(UserSystemRole.Doctor);
    private const int MaxPageSize = 100;

    private readonly AppDbContext _dbContext;
    private readonly UserManager<User> _userManager;
    private readonly IWebHostEnvironment _environment;

    public DoctorsService(
        AppDbContext dbContext,
        UserManager<User> userManager,
        IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _environment = environment;
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

    public async Task<ActionResult<DoctorDashboardResponse>> GetMyDashboardAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var (doctor, error) = await GetCurrentDoctorInfoOrErrorAsync(user, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        var weekStartDate = GetWeekStartDate(today);
        var weekEndDate = weekStartDate.AddDays(6);

        var todayAppointmentsCount = await _dbContext.Appointments
            .AsNoTracking()
            .CountAsync(
                appointment => appointment.DoctorId == doctor!.DoctorId
                    && appointment.AppointmentDate == today,
                cancellationToken);

        var pendingLabRequestsCount = await CountPendingLabRequestsForClinicAsync(
            doctor!.ClinicId,
            cancellationToken);
        var pendingImagingRequestsCount = await CountPendingImagingRequestsForClinicAsync(
            doctor.ClinicId,
            cancellationToken);
        var unreadMessagesCount = await CountUnreadMessagesAsync(doctor.DoctorId, cancellationToken);

        var upcomingAppointments = await BuildDoctorAppointmentProjectionQuery(doctor.DoctorId)
            .Where(appointment => appointment.AppointmentDate >= today)
            .OrderBy(appointment => appointment.AppointmentDate)
            .ThenBy(appointment => appointment.AppointmentTime)
            .Take(3)
            .ToListAsync(cancellationToken);

        var weeklyCounts = await _dbContext.Appointments
            .AsNoTracking()
            .Where(appointment => appointment.DoctorId == doctor.DoctorId
                && appointment.AppointmentDate >= weekStartDate
                && appointment.AppointmentDate <= weekEndDate)
            .GroupBy(appointment => appointment.AppointmentDate)
            .Select(group => new
            {
                Date = group.Key,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        var weeklyCountsByDate = weeklyCounts.ToDictionary(item => item.Date, item => item.Count);
        var weeklyAppointments = BuildWeekDates(weekStartDate)
            .Select(date => new DoctorWeeklyAppointmentCountResponse
            {
                Date = date,
                DayOfWeek = date.DayOfWeek.ToString(),
                AppointmentsCount = weeklyCountsByDate.TryGetValue(date, out var count) ? count : 0
            })
            .ToList();

        var totalPendingClinicalRequests = pendingLabRequestsCount + pendingImagingRequestsCount;

        return new OkObjectResult(new DoctorDashboardResponse
        {
            DoctorId = doctor.DoctorId,
            DoctorName = doctor.DoctorName,
            ClinicId = doctor.ClinicId,
            ClinicName = doctor.ClinicName,
            Today = today,
            WeekStartDate = weekStartDate,
            WeekEndDate = weekEndDate,
            TodayAppointmentsCount = todayAppointmentsCount,
            PendingLabRequestsCount = pendingLabRequestsCount,
            PendingImagingRequestsCount = pendingImagingRequestsCount,
            TotalPendingClinicalRequestsCount = totalPendingClinicalRequests,
            UnreadMessagesCount = unreadMessagesCount,
            UpcomingAppointments = upcomingAppointments.Select(ToDoctorAppointmentSummary).ToList(),
            WeeklyAppointments = weeklyAppointments,
            ClinicalRequests = new DoctorClinicalRequestsSummaryResponse
            {
                TotalPending = totalPendingClinicalRequests,
                LabRequests = pendingLabRequestsCount,
                ImagingRequests = pendingImagingRequestsCount
            }
        });
    }

    public async Task<ActionResult<DoctorAppointmentsScheduleResponse>> GetMyAppointmentsScheduleAsync(
        DateOnly? fromDate,
        DateOnly? toDate,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var (doctor, error) = await GetCurrentDoctorInfoOrErrorAsync(user, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        var (rangeStart, rangeEnd) = ResolveScheduleRange(fromDate, toDate, today);
        if (rangeStart > rangeEnd)
        {
            return new BadRequestObjectResult("fromDate must be before or equal to toDate.");
        }

        var appointments = await BuildDoctorAppointmentProjectionQuery(doctor!.DoctorId)
            .Where(appointment => appointment.AppointmentDate >= rangeStart
                && appointment.AppointmentDate <= rangeEnd)
            .OrderBy(appointment => appointment.AppointmentDate)
            .ThenBy(appointment => appointment.AppointmentTime)
            .ToListAsync(cancellationToken);

        var appointmentsByDate = appointments
            .GroupBy(appointment => appointment.AppointmentDate)
            .ToDictionary(
                group => group.Key,
                group => group.Select(ToDoctorAppointmentSummary).ToList());

        var days = new List<DoctorScheduleDayResponse>();
        for (var date = rangeStart; date <= rangeEnd; date = date.AddDays(1))
        {
            days.Add(new DoctorScheduleDayResponse
            {
                Date = date,
                DayOfWeek = date.DayOfWeek.ToString(),
                IsToday = date == today,
                Appointments = appointmentsByDate.TryGetValue(date, out var dayAppointments)
                    ? dayAppointments
                    : []
            });
        }

        return new OkObjectResult(new DoctorAppointmentsScheduleResponse
        {
            FromDate = rangeStart,
            ToDate = rangeEnd,
            TotalAppointmentsCount = appointments.Count,
            Days = days
        });
    }

    public async Task<ActionResult<DoctorAppointmentDetailResponse>> GetMyAppointmentDetailAsync(
        Guid appointmentId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var (doctor, error) = await GetCurrentDoctorInfoOrErrorAsync(user, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var appointment = await BuildDoctorAppointmentProjectionQuery(doctor!.DoctorId)
            .FirstOrDefaultAsync(
                appointment => appointment.AppointmentId == appointmentId,
                cancellationToken);

        if (appointment is null)
        {
            return new NotFoundObjectResult("Appointment not found.");
        }

        return new OkObjectResult(ToDoctorAppointmentDetail(appointment));
    }

    public async Task<ActionResult<DoctorMedicalRecordsResponse>> GetMyMedicalRecordsAsync(
        string? patientId,
        Guid? clinicId,
        string? type,
        string? query,
        int page,
        int pageSize,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var (_, error) = await GetCurrentDoctorInfoOrErrorAsync(user, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        NormalizePaging(ref page, ref pageSize);

        var recordsQuery = BuildDoctorMedicalRecordProjectionQuery();
        recordsQuery = ApplyMedicalRecordScopeFilters(recordsQuery, patientId, clinicId, query);

        var typeCounts = new DoctorMedicalRecordTypeCountsResponse
        {
            All = await recordsQuery.CountAsync(cancellationToken),
            LabTest = await recordsQuery.CountAsync(record => record.HasLabResult, cancellationToken),
            ImagingTest = await recordsQuery.CountAsync(record => record.HasImagingResult, cancellationToken),
            Visit = await recordsQuery.CountAsync(
                record => !record.HasLabResult && !record.HasImagingResult,
                cancellationToken)
        };

        if (!ApplyDoctorMedicalRecordTypeFilter(ref recordsQuery, type, out var typeError))
        {
            return new BadRequestObjectResult(typeError);
        }

        var totalCount = await recordsQuery.CountAsync(cancellationToken);
        var records = await recordsQuery
            .OrderByDescending(record => record.UploadedAt)
            .ThenBy(record => record.MedicalFileId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new OkObjectResult(new DoctorMedicalRecordsResponse
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            TypeCounts = typeCounts,
            Items = records.Select(MapDoctorMedicalRecord).ToList()
        });
    }

    public async Task<ActionResult<DoctorMedicalRecordDetailResponse>> GetMyMedicalRecordAsync(
        Guid medicalFileId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var (_, error) = await GetCurrentDoctorInfoOrErrorAsync(user, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var record = await BuildDoctorMedicalRecordProjectionQuery()
            .FirstOrDefaultAsync(
                medicalRecord => medicalRecord.MedicalFileId == medicalFileId,
                cancellationToken);

        if (record is null)
        {
            return new NotFoundObjectResult("Medical record not found.");
        }

        return new OkObjectResult(MapDoctorMedicalRecordDetail(record));
    }

    public async Task<IActionResult> DownloadMyMedicalRecordAsync(
        Guid medicalFileId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var (_, error) = await GetCurrentDoctorInfoOrErrorAsync(user, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var record = await BuildDoctorMedicalRecordProjectionQuery()
            .FirstOrDefaultAsync(
                medicalRecord => medicalRecord.MedicalFileId == medicalFileId,
                cancellationToken);

        if (record is null)
        {
            return new NotFoundObjectResult("Medical record not found.");
        }

        var filePath = ResolvePhysicalFilePath(record.FilePath);
        if (!File.Exists(filePath))
        {
            return new NotFoundObjectResult("The medical record exists, but the physical file is not available on this server.");
        }

        return new PhysicalFileResult(filePath, GetContentType(filePath))
        {
            FileDownloadName = Path.GetFileName(filePath),
            EnableRangeProcessing = true
        };
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

    public async Task<ActionResult<ChangeDoctorPasswordResponse>> ChangeMyPasswordAsync(
        ChangeDoctorPasswordRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
        {
            return new BadRequestObjectResult(new ChangeDoctorPasswordResponse
            {
                Success = false,
                Message = "Current password is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return new BadRequestObjectResult(new ChangeDoctorPasswordResponse
            {
                Success = false,
                Message = "New password is required."
            });
        }

        if (request.NewPassword != request.ConfirmNewPassword)
        {
            return new BadRequestObjectResult(new ChangeDoctorPasswordResponse
            {
                Success = false,
                Message = "New password and confirm password do not match."
            });
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

        var changeResult = await _userManager.ChangePasswordAsync(
            doctor.User,
            request.CurrentPassword,
            request.NewPassword);

        if (!changeResult.Succeeded)
        {
            return new BadRequestObjectResult(new ChangeDoctorPasswordResponse
            {
                Success = false,
                Message = string.Join(" ", changeResult.Errors.Select(error => error.Description))
            });
        }

        return new OkObjectResult(new ChangeDoctorPasswordResponse
        {
            Success = true,
            Message = "Password updated successfully."
        });
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

    private async Task<(CurrentDoctorInfo? Doctor, ActionResult? Error)> GetCurrentDoctorInfoOrErrorAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId(user);
        if (currentUserId is null)
        {
            return (null, new UnauthorizedObjectResult("Invalid token."));
        }

        var doctor = await _dbContext.Doctors
            .AsNoTracking()
            .Where(profile => profile.DoctorId == currentUserId.Value)
            .Select(profile => new CurrentDoctorInfo
            {
                DoctorId = profile.DoctorId,
                DoctorName = profile.User.Name,
                ClinicId = profile.User.ClinicId,
                ClinicName = profile.User.Clinic != null ? profile.User.Clinic.Name : null
            })
            .FirstOrDefaultAsync(cancellationToken);

        return doctor is null
            ? (null, new NotFoundObjectResult("Doctor not found."))
            : (doctor, null);
    }

    private IQueryable<DoctorAppointmentProjection> BuildDoctorAppointmentProjectionQuery(Guid doctorId)
    {
        return _dbContext.Appointments
            .AsNoTracking()
            .Where(appointment => appointment.DoctorId == doctorId)
            .Select(appointment => new DoctorAppointmentProjection
            {
                AppointmentId = appointment.AppointmentId,
                DoctorId = appointment.DoctorId,
                DoctorName = appointment.Doctor.User.Name,
                PatientId = appointment.PatientId,
                PatientName = appointment.Patient.User.Name,
                PatientUserId = appointment.Patient.UserID,
                ClinicId = appointment.Doctor.User.ClinicId,
                ClinicName = appointment.Doctor.User.Clinic != null ? appointment.Doctor.User.Clinic.Name : null,
                AppointmentDate = appointment.AppointmentDate,
                AppointmentTime = appointment.AppointmentTime,
                SlotStartTime = appointment.AvailabilitySlot.StartTime,
                SlotEndTime = appointment.AvailabilitySlot.EndTime,
                Notes = appointment.Notes
            });
    }

    private IQueryable<DoctorMedicalRecordProjection> BuildDoctorMedicalRecordProjectionQuery()
    {
        return _dbContext.MedicalFiles
            .AsNoTracking()
            .Select(file => new DoctorMedicalRecordProjection
            {
                MedicalFileId = file.MedicalFileId,
                AppointmentId = file.AppointmentId,
                FileType = file.FileType,
                FilePath = file.FilePath,
                FileSizeInBytes = file.FileSizeInBytes,
                SeverityLevel = file.SeverityLevel,
                UploadedAt = file.UploadedAt,
                PatientId = file.Appointment.PatientId,
                PatientName = file.Appointment.Patient.User.Name,
                PatientUserId = file.Appointment.Patient.UserID,
                ClinicId = file.Appointment.Doctor.User.ClinicId,
                ClinicName = file.Appointment.Doctor.User.Clinic != null ? file.Appointment.Doctor.User.Clinic.Name : null,
                AppointmentDate = file.Appointment.AppointmentDate,
                AppointmentTime = file.Appointment.AppointmentTime,
                AppointmentNotes = file.Appointment.Notes,
                RecordedByDoctorId = file.UploadedByDoctorId,
                RecordedByDoctorName = file.UploadedByDoctor.User.Name,
                LabTestName = file.LabTestRequestsAsResult
                    .OrderBy(test => test.TestName)
                    .Select(test => test.TestName)
                    .FirstOrDefault(),
                LabClinicalNotes = file.LabTestRequestsAsResult
                    .OrderBy(test => test.TestName)
                    .Select(test => test.ClinicalNotes)
                    .FirstOrDefault(),
                ImagingTestName = file.ImagingTestRequestsAsResult
                    .OrderBy(test => test.TestName)
                    .Select(test => test.TestName)
                    .FirstOrDefault(),
                ImagingClinicalNotes = file.ImagingTestRequestsAsResult
                    .OrderBy(test => test.TestName)
                    .Select(test => test.ClinicalNotes)
                    .FirstOrDefault(),
                HasLabResult = file.LabTestRequestsAsResult.Any(),
                HasImagingResult = file.ImagingTestRequestsAsResult.Any()
            });
    }

    private static IQueryable<DoctorMedicalRecordProjection> ApplyMedicalRecordScopeFilters(
        IQueryable<DoctorMedicalRecordProjection> recordsQuery,
        string? patientId,
        Guid? clinicId,
        string? query)
    {
        var normalizedPatientId = NormalizeOptional(patientId);
        if (normalizedPatientId is not null)
        {
            if (Guid.TryParse(normalizedPatientId, out var patientGuid))
            {
                recordsQuery = recordsQuery.Where(record => record.PatientId == patientGuid);
            }
            else
            {
                recordsQuery = recordsQuery.Where(record => record.PatientUserId == normalizedPatientId);
            }
        }

        if (clinicId.HasValue)
        {
            recordsQuery = recordsQuery.Where(record => record.ClinicId == clinicId.Value);
        }

        var normalizedQuery = NormalizeOptional(query);
        if (normalizedQuery is not null)
        {
            recordsQuery = recordsQuery.Where(record =>
                record.PatientName.Contains(normalizedQuery)
                || record.PatientUserId.Contains(normalizedQuery)
                || record.RecordedByDoctorName.Contains(normalizedQuery)
                || (record.ClinicName != null && record.ClinicName.Contains(normalizedQuery))
                || (record.AppointmentNotes != null && record.AppointmentNotes.Contains(normalizedQuery))
                || (record.LabClinicalNotes != null && record.LabClinicalNotes.Contains(normalizedQuery))
                || (record.ImagingClinicalNotes != null && record.ImagingClinicalNotes.Contains(normalizedQuery))
                || (record.LabTestName != null && record.LabTestName.Contains(normalizedQuery))
                || (record.ImagingTestName != null && record.ImagingTestName.Contains(normalizedQuery))
                || record.FilePath.Contains(normalizedQuery));
        }

        return recordsQuery;
    }

    private static bool ApplyDoctorMedicalRecordTypeFilter(
        ref IQueryable<DoctorMedicalRecordProjection> recordsQuery,
        string? type,
        out string error)
    {
        error = string.Empty;
        var normalizedType = NormalizeOptional(type)?.ToLowerInvariant() ?? "all";

        switch (normalizedType)
        {
            case "all":
                return true;
            case "lab":
            case "labs":
            case "lab-test":
            case "lab-tests":
            case "lab-results":
                recordsQuery = recordsQuery.Where(record => record.HasLabResult);
                return true;
            case "imaging":
            case "imaging-test":
            case "imaging-tests":
            case "imaging-results":
                recordsQuery = recordsQuery.Where(record => record.HasImagingResult);
                return true;
            case "visit":
            case "visits":
            case "medical-record":
            case "medical-records":
            case "prescription":
            case "prescriptions":
                recordsQuery = recordsQuery.Where(record => !record.HasLabResult && !record.HasImagingResult);
                return true;
            default:
                error = "Invalid record type. Use all, lab-test, imaging-test, or visit.";
                return false;
        }
    }

    private async Task<int> CountPendingLabRequestsForClinicAsync(
        Guid? clinicId,
        CancellationToken cancellationToken)
    {
        if (!clinicId.HasValue)
        {
            return 0;
        }

        return await _dbContext.LabTestRequests
            .AsNoTracking()
            .CountAsync(
                request => request.ResultMedicalFileId == null
                    && (request.TestingClinicId == clinicId.Value
                        || (request.LaboratoryTechnologist != null
                            && request.LaboratoryTechnologist.User.ClinicId == clinicId.Value)),
                cancellationToken);
    }

    private async Task<int> CountPendingImagingRequestsForClinicAsync(
        Guid? clinicId,
        CancellationToken cancellationToken)
    {
        if (!clinicId.HasValue)
        {
            return 0;
        }

        return await _dbContext.ImagingTestRequests
            .AsNoTracking()
            .CountAsync(
                request => request.ResultMedicalFileId == null
                    && (request.RadiologyClinicId == clinicId.Value
                        || (request.RadiologyTechnologist != null
                            && request.RadiologyTechnologist.User.ClinicId == clinicId.Value)),
                cancellationToken);
    }

    private async Task<int> CountUnreadMessagesAsync(
        Guid doctorId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.ChatMessages
            .AsNoTracking()
            .CountAsync(
                message => message.SenderId != doctorId
                    && message.ReadAt == null
                    && (message.Chat.UserOneId == doctorId || message.Chat.UserTwoId == doctorId),
                cancellationToken);
    }

    private static (DateOnly FromDate, DateOnly ToDate) ResolveScheduleRange(
        DateOnly? fromDate,
        DateOnly? toDate,
        DateOnly today)
    {
        if (!fromDate.HasValue && !toDate.HasValue)
        {
            var weekStart = GetWeekStartDate(today);
            return (weekStart, weekStart.AddDays(6));
        }

        if (fromDate.HasValue && !toDate.HasValue)
        {
            return (fromDate.Value, fromDate.Value);
        }

        if (!fromDate.HasValue && toDate.HasValue)
        {
            return (toDate.Value, toDate.Value);
        }

        return (fromDate!.Value, toDate!.Value);
    }

    private static DateOnly GetWeekStartDate(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-offset);
    }

    private static IEnumerable<DateOnly> BuildWeekDates(DateOnly weekStartDate)
    {
        for (var index = 0; index < 7; index++)
        {
            yield return weekStartDate.AddDays(index);
        }
    }

    private static DoctorAppointmentSummaryResponse ToDoctorAppointmentSummary(
        DoctorAppointmentProjection appointment)
    {
        return new DoctorAppointmentSummaryResponse
        {
            AppointmentId = appointment.AppointmentId,
            PatientId = appointment.PatientId,
            PatientName = appointment.PatientName,
            PatientUserId = appointment.PatientUserId,
            AppointmentDate = appointment.AppointmentDate,
            DayOfWeek = appointment.AppointmentDate.DayOfWeek.ToString(),
            AppointmentTime = appointment.AppointmentTime,
            DurationMinutes = CalculateDurationMinutes(appointment.SlotStartTime, appointment.SlotEndTime),
            Status = "Scheduled",
            ReasonForVisit = appointment.Notes
        };
    }

    private static DoctorAppointmentDetailResponse ToDoctorAppointmentDetail(
        DoctorAppointmentProjection appointment)
    {
        return new DoctorAppointmentDetailResponse
        {
            AppointmentId = appointment.AppointmentId,
            DoctorId = appointment.DoctorId,
            DoctorName = appointment.DoctorName,
            PatientId = appointment.PatientId,
            PatientName = appointment.PatientName,
            PatientUserId = appointment.PatientUserId,
            ClinicId = appointment.ClinicId,
            ClinicName = appointment.ClinicName,
            AppointmentDate = appointment.AppointmentDate,
            DayOfWeek = appointment.AppointmentDate.DayOfWeek.ToString(),
            AppointmentTime = appointment.AppointmentTime,
            DurationMinutes = CalculateDurationMinutes(appointment.SlotStartTime, appointment.SlotEndTime),
            Status = "Scheduled",
            ReasonForVisit = appointment.Notes
        };
    }

    private static DoctorMedicalRecordResponse MapDoctorMedicalRecord(
        DoctorMedicalRecordProjection record)
    {
        var notes = GetDoctorMedicalRecordNotes(record);

        return new DoctorMedicalRecordResponse
        {
            MedicalFileId = record.MedicalFileId,
            AppointmentId = record.AppointmentId,
            RecordCode = BuildDoctorMedicalRecordCode(record),
            RecordType = GetDoctorMedicalRecordType(record),
            Title = GetDoctorMedicalRecordTitle(record),
            Description = notes,
            FileName = Path.GetFileName(record.FilePath),
            FileType = record.FileType.ToString(),
            FileSizeInBytes = record.FileSizeInBytes,
            SeverityLevel = record.SeverityLevel.ToString(),
            UploadedAt = record.UploadedAt,
            PatientId = record.PatientId,
            PatientUserId = record.PatientUserId,
            PatientName = record.PatientName,
            ClinicId = record.ClinicId,
            ClinicName = record.ClinicName,
            RecordedByDoctorId = record.RecordedByDoctorId,
            RecordedByDoctorName = record.RecordedByDoctorName,
            AppointmentDate = record.AppointmentDate,
            AppointmentTime = record.AppointmentTime,
            LabTestName = record.LabTestName,
            ImagingTestName = record.ImagingTestName,
            FileUrl = $"/api/Doctors/me/medical-records/{record.MedicalFileId}/download"
        };
    }

    private static DoctorMedicalRecordDetailResponse MapDoctorMedicalRecordDetail(
        DoctorMedicalRecordProjection record)
    {
        var notes = GetDoctorMedicalRecordNotes(record);
        var summary = notes
            ?? $"{GetDoctorMedicalRecordTitle(record)} recorded by {record.RecordedByDoctorName}.";

        return new DoctorMedicalRecordDetailResponse
        {
            MedicalFileId = record.MedicalFileId,
            AppointmentId = record.AppointmentId,
            RecordCode = BuildDoctorMedicalRecordCode(record),
            RecordType = GetDoctorMedicalRecordType(record),
            Title = GetDoctorMedicalRecordTitle(record),
            Description = notes,
            FileName = Path.GetFileName(record.FilePath),
            FileType = record.FileType.ToString(),
            FileSizeInBytes = record.FileSizeInBytes,
            SeverityLevel = record.SeverityLevel.ToString(),
            UploadedAt = record.UploadedAt,
            PatientId = record.PatientId,
            PatientUserId = record.PatientUserId,
            PatientName = record.PatientName,
            ClinicId = record.ClinicId,
            ClinicName = record.ClinicName,
            RecordedByDoctorId = record.RecordedByDoctorId,
            RecordedByDoctorName = record.RecordedByDoctorName,
            AppointmentDate = record.AppointmentDate,
            AppointmentTime = record.AppointmentTime,
            LabTestName = record.LabTestName,
            ImagingTestName = record.ImagingTestName,
            FileUrl = $"/api/Doctors/me/medical-records/{record.MedicalFileId}/download",
            AppointmentNotes = record.AppointmentNotes,
            Summary = summary,
            ClinicalDetails = notes
        };
    }

    private static string? GetDoctorMedicalRecordNotes(DoctorMedicalRecordProjection record)
    {
        return record.LabClinicalNotes
            ?? record.ImagingClinicalNotes
            ?? record.AppointmentNotes;
    }

    private static string GetDoctorMedicalRecordType(DoctorMedicalRecordProjection record)
    {
        if (record.HasLabResult)
        {
            return "LabTest";
        }

        if (record.HasImagingResult)
        {
            return "ImagingTest";
        }

        return "Visit";
    }

    private static string GetDoctorMedicalRecordTitle(DoctorMedicalRecordProjection record)
    {
        return record.LabTestName
            ?? record.ImagingTestName
            ?? Path.GetFileNameWithoutExtension(record.FilePath)
            ?? $"{record.FileType} medical record";
    }

    private static string BuildDoctorMedicalRecordCode(DoctorMedicalRecordProjection record)
    {
        return $"MR-{record.UploadedAt:yyyy}-{record.MedicalFileId.ToString("N")[..8].ToUpperInvariant()}";
    }

    private static int CalculateDurationMinutes(TimeOnly startTime, TimeOnly endTime)
    {
        var minutes = (int)(endTime.ToTimeSpan() - startTime.ToTimeSpan()).TotalMinutes;
        return minutes > 0 ? minutes : 45;
    }

    private string ResolvePhysicalFilePath(string filePath)
    {
        if (Path.IsPathRooted(filePath))
        {
            return filePath;
        }

        return Path.GetFullPath(Path.Combine(_environment.ContentRootPath, filePath));
    }

    private static string GetContentType(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
        };
    }

    private static void NormalizePaging(ref int page, ref int pageSize)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, MaxPageSize);
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

    private sealed class CurrentDoctorInfo
    {
        public Guid DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public Guid? ClinicId { get; set; }
        public string? ClinicName { get; set; }
    }

    private sealed class DoctorAppointmentProjection
    {
        public Guid AppointmentId { get; set; }
        public Guid DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public Guid PatientId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string PatientUserId { get; set; } = string.Empty;
        public Guid? ClinicId { get; set; }
        public string? ClinicName { get; set; }
        public DateOnly AppointmentDate { get; set; }
        public TimeOnly AppointmentTime { get; set; }
        public TimeOnly SlotStartTime { get; set; }
        public TimeOnly SlotEndTime { get; set; }
        public string? Notes { get; set; }
    }

    private sealed class DoctorMedicalRecordProjection
    {
        public Guid MedicalFileId { get; set; }
        public Guid AppointmentId { get; set; }
        public MedicalFileType FileType { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public long FileSizeInBytes { get; set; }
        public SeverityLevel SeverityLevel { get; set; }
        public DateTime UploadedAt { get; set; }
        public Guid PatientId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string PatientUserId { get; set; } = string.Empty;
        public Guid? ClinicId { get; set; }
        public string? ClinicName { get; set; }
        public DateOnly AppointmentDate { get; set; }
        public TimeOnly AppointmentTime { get; set; }
        public string? AppointmentNotes { get; set; }
        public Guid RecordedByDoctorId { get; set; }
        public string RecordedByDoctorName { get; set; } = string.Empty;
        public string? LabTestName { get; set; }
        public string? LabClinicalNotes { get; set; }
        public string? ImagingTestName { get; set; }
        public string? ImagingClinicalNotes { get; set; }
        public bool HasLabResult { get; set; }
        public bool HasImagingResult { get; set; }
    }
}
