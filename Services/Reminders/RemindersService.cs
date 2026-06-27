using System.Security.Claims;
using System.Linq.Expressions;
using HSCSAPI.Data;
using HSCSAPI.DTOs.Reminders;
using HSCSAPI.Models.Reminders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Services.Reminders;

public class RemindersService : IRemindersService
{
    private const int MaxPageSize = 100;

    private readonly AppDbContext _dbContext;

    public RemindersService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ActionResult<ReminderListResponse>> GetMyRemindersAsync(
        string? status,
        int page,
        int pageSize,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId(user);
        if (userId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        if (!TryNormalizeStatus(status, out var normalizedStatus, out var statusError))
        {
            return new BadRequestObjectResult(statusError);
        }

        NormalizePaging(ref page, ref pageSize);

        var now = DateTime.UtcNow;
        var activeReminders = BuildAccessibleReminderQuery(userId.Value)
            .Where(reminder => reminder.DismissedAt == null);

        var allCount = await activeReminders.CountAsync(cancellationToken);
        var upcomingCount = await activeReminders
            .CountAsync(reminder => reminder.ReminderAt >= now, cancellationToken);
        var pastCount = await activeReminders
            .CountAsync(reminder => reminder.ReminderAt < now, cancellationToken);

        var filteredReminders = normalizedStatus switch
        {
            "upcoming" => activeReminders.Where(reminder => reminder.ReminderAt >= now),
            "past" => activeReminders.Where(reminder => reminder.ReminderAt < now),
            _ => activeReminders
        };

        var totalCount = await filteredReminders.CountAsync(cancellationToken);
        var reminders = await filteredReminders
            .OrderBy(reminder => reminder.ReminderAt < now)
            .ThenBy(reminder => reminder.ReminderAt)
            .ThenBy(reminder => reminder.ReminderId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ProjectReminder(now))
            .ToListAsync(cancellationToken);

        return new OkObjectResult(new ReminderListResponse
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            AllCount = allCount,
            UpcomingCount = upcomingCount,
            PastCount = pastCount,
            Items = reminders
        });
    }

    public async Task<ActionResult<ReminderResponse>> CreateReminderAsync(
        CreateReminderRequest? request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId(user);
        if (userId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        if (!ValidateCreateRequest(request, out var validationError))
        {
            return new BadRequestObjectResult(validationError);
        }

        var validRequest = request!;
        if (!IsRequestedReminderParticipant(userId.Value, validRequest))
        {
            return new ForbidResult();
        }

        var participantError = await ValidateParticipantsAsync(
            validRequest.PatientId,
            validRequest.DoctorId,
            validRequest.AuthorizedMemberId,
            cancellationToken);
        if (participantError is not null)
        {
            return new BadRequestObjectResult(participantError);
        }

        var reminder = new Reminder
        {
            PatientId = validRequest.PatientId,
            DoctorId = validRequest.DoctorId,
            AuthorizedMemberId = validRequest.AuthorizedMemberId,
            Title = validRequest.Title.Trim(),
            ReminderText = validRequest.Message.Trim(),
            Category = NormalizeCategory(validRequest.Category, validRequest.Title, validRequest.Message),
            ReminderAt = validRequest.ReminderAt,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Reminders.Add(reminder);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = await BuildAccessibleReminderQuery(userId.Value)
            .Where(x => x.ReminderId == reminder.ReminderId)
            .Select(ProjectReminder(DateTime.UtcNow))
            .FirstAsync(cancellationToken);

        return new CreatedAtActionResult(
            actionName: "GetReminder",
            controllerName: "Reminders",
            routeValues: new { reminderId = reminder.ReminderId },
            value: response);
    }

    public async Task<ActionResult<ReminderResponse>> GetReminderAsync(
        Guid reminderId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId(user);
        if (userId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var now = DateTime.UtcNow;
        var reminder = await BuildAccessibleReminderQuery(userId.Value)
            .Where(x => x.ReminderId == reminderId)
            .Select(ProjectReminder(now))
            .FirstOrDefaultAsync(cancellationToken);

        return reminder is null
            ? new NotFoundObjectResult("Reminder not found.")
            : new OkObjectResult(reminder);
    }

    public async Task<ActionResult<ReminderResponse>> UpdateReminderAsync(
        Guid reminderId,
        UpdateReminderRequest? request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId(user);
        if (userId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        if (!ValidateUpdateRequest(request, out var validationError))
        {
            return new BadRequestObjectResult(validationError);
        }

        var reminder = await _dbContext.Reminders
            .FirstOrDefaultAsync(
                x => x.ReminderId == reminderId
                    && (x.DoctorId == userId.Value
                        || x.PatientId == userId.Value
                        || x.AuthorizedMemberId == userId.Value),
                cancellationToken);

        if (reminder is null)
        {
            return new NotFoundObjectResult("Reminder not found.");
        }

        var participantError = await ValidateAuthorizedMemberAsync(
            reminder.PatientId,
            request!.AuthorizedMemberId,
            cancellationToken);
        if (participantError is not null)
        {
            return new BadRequestObjectResult(participantError);
        }

        reminder.Title = request.Title.Trim();
        reminder.ReminderText = request.Message.Trim();
        reminder.Category = NormalizeCategory(request.Category, request.Title, request.Message);
        reminder.ReminderAt = request.ReminderAt;
        reminder.AuthorizedMemberId = request.AuthorizedMemberId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = await BuildAccessibleReminderQuery(userId.Value)
            .Where(x => x.ReminderId == reminderId)
            .Select(ProjectReminder(DateTime.UtcNow))
            .FirstAsync(cancellationToken);

        return new OkObjectResult(response);
    }

    public async Task<ActionResult<ReminderResponse>> DismissReminderAsync(
        Guid reminderId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId(user);
        if (userId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var reminder = await _dbContext.Reminders
            .FirstOrDefaultAsync(
                x => x.ReminderId == reminderId
                    && (x.DoctorId == userId.Value
                        || x.PatientId == userId.Value
                        || x.AuthorizedMemberId == userId.Value),
                cancellationToken);

        if (reminder is null)
        {
            return new NotFoundObjectResult("Reminder not found.");
        }

        reminder.DismissedAt ??= DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = await BuildAccessibleReminderQuery(userId.Value)
            .Where(x => x.ReminderId == reminderId)
            .Select(ProjectReminder(DateTime.UtcNow))
            .FirstAsync(cancellationToken);

        return new OkObjectResult(response);
    }

    public async Task<ActionResult<ReminderPreferencesResponse>> GetMyPreferencesAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId(user);
        if (userId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var preferences = await _dbContext.ReminderPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId.Value, cancellationToken);

        return new OkObjectResult(preferences is null
            ? BuildDefaultPreferences(userId.Value)
            : MapPreferences(preferences));
    }

    public async Task<ActionResult<ReminderPreferencesResponse>> UpdateMyPreferencesAsync(
        UpdateReminderPreferencesRequest? request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId(user);
        if (userId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        if (!ValidatePreferencesRequest(request, out var validationError))
        {
            return new BadRequestObjectResult(validationError);
        }

        if (!await _dbContext.Users.AsNoTracking().AnyAsync(x => x.Id == userId.Value, cancellationToken))
        {
            return new NotFoundObjectResult("User not found.");
        }

        var preferences = await _dbContext.ReminderPreferences
            .FirstOrDefaultAsync(x => x.UserId == userId.Value, cancellationToken);

        if (preferences is null)
        {
            preferences = new ReminderPreference { UserId = userId.Value };
            _dbContext.ReminderPreferences.Add(preferences);
        }

        var validRequest = request!;
        preferences.AppointmentRemindersEnabled = validRequest.AppointmentRemindersEnabled.GetValueOrDefault();
        preferences.LabResultRemindersEnabled = validRequest.LabResultRemindersEnabled.GetValueOrDefault();
        preferences.MessageRemindersEnabled = validRequest.MessageRemindersEnabled.GetValueOrDefault();
        preferences.InAppNotificationsEnabled = validRequest.InAppNotificationsEnabled.GetValueOrDefault();
        preferences.EmailRemindersEnabled = validRequest.EmailRemindersEnabled.GetValueOrDefault();
        preferences.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new OkObjectResult(MapPreferences(preferences));
    }

    private IQueryable<Reminder> BuildAccessibleReminderQuery(Guid userId)
    {
        return _dbContext.Reminders
            .AsNoTracking()
            .Where(reminder => reminder.DoctorId == userId
                || reminder.PatientId == userId
                || reminder.AuthorizedMemberId == userId);
    }

    private static Expression<Func<Reminder, ReminderResponse>> ProjectReminder(DateTime now)
    {
        return reminder => new ReminderResponse
        {
            ReminderId = reminder.ReminderId,
            PatientId = reminder.PatientId,
            PatientName = reminder.Patient.User.Name,
            DoctorId = reminder.DoctorId,
            DoctorName = reminder.Doctor.User.Name,
            AuthorizedMemberId = reminder.AuthorizedMemberId,
            AuthorizedMemberName = reminder.AuthorizedMember == null ? null : reminder.AuthorizedMember.User.Name,
            Title = reminder.Title,
            Message = reminder.ReminderText,
            Category = reminder.Category,
            Status = reminder.DismissedAt != null ? "Dismissed" : reminder.ReminderAt >= now ? "Upcoming" : "Past",
            ReminderAt = reminder.ReminderAt,
            CreatedAt = reminder.CreatedAt,
            DismissedAt = reminder.DismissedAt
        };
    }

    private static ReminderPreferencesResponse BuildDefaultPreferences(Guid userId)
    {
        return new ReminderPreferencesResponse
        {
            UserId = userId,
            AppointmentRemindersEnabled = true,
            LabResultRemindersEnabled = true,
            MessageRemindersEnabled = true,
            InAppNotificationsEnabled = true,
            EmailRemindersEnabled = false,
            UpdatedAt = null
        };
    }

    private static ReminderPreferencesResponse MapPreferences(ReminderPreference preferences)
    {
        return new ReminderPreferencesResponse
        {
            UserId = preferences.UserId,
            AppointmentRemindersEnabled = preferences.AppointmentRemindersEnabled,
            LabResultRemindersEnabled = preferences.LabResultRemindersEnabled,
            MessageRemindersEnabled = preferences.MessageRemindersEnabled,
            InAppNotificationsEnabled = preferences.InAppNotificationsEnabled,
            EmailRemindersEnabled = preferences.EmailRemindersEnabled,
            UpdatedAt = preferences.UpdatedAt
        };
    }

    private async Task<string?> ValidateParticipantsAsync(
        Guid patientId,
        Guid doctorId,
        Guid? authorizedMemberId,
        CancellationToken cancellationToken)
    {
        if (!await _dbContext.Patients.AsNoTracking().AnyAsync(x => x.PatientId == patientId, cancellationToken))
        {
            return "Patient profile not found.";
        }

        if (!await _dbContext.Doctors.AsNoTracking().AnyAsync(x => x.DoctorId == doctorId, cancellationToken))
        {
            return "Doctor profile not found.";
        }

        return await ValidateAuthorizedMemberAsync(patientId, authorizedMemberId, cancellationToken);
    }

    private async Task<string?> ValidateAuthorizedMemberAsync(
        Guid patientId,
        Guid? authorizedMemberId,
        CancellationToken cancellationToken)
    {
        if (!authorizedMemberId.HasValue)
        {
            return null;
        }

        if (!await _dbContext.AuthorizedMembers.AsNoTracking().AnyAsync(
            x => x.AuthorizedMemberId == authorizedMemberId.Value,
            cancellationToken))
        {
            return "Authorized member profile not found.";
        }

        var isAuthorizedForPatient = await _dbContext.PatientAuthorizedMembers
            .AsNoTracking()
            .AnyAsync(
                x => x.PatientId == patientId
                    && x.AuthorizedMemberId == authorizedMemberId.Value
                    && x.IsActive,
                cancellationToken);

        return isAuthorizedForPatient
            ? null
            : "Authorized member is not linked to this patient.";
    }

    private static bool ValidateCreateRequest(CreateReminderRequest? request, out string error)
    {
        if (request is null)
        {
            error = "Request body is required.";
            return false;
        }

        if (request.PatientId == Guid.Empty)
        {
            error = "PatientId is required.";
            return false;
        }

        if (request.DoctorId == Guid.Empty)
        {
            error = "DoctorId is required.";
            return false;
        }

        return ValidateReminderContent(request.Title, request.Message, request.Category, request.ReminderAt, out error);
    }

    private static bool ValidateUpdateRequest(UpdateReminderRequest? request, out string error)
    {
        if (request is null)
        {
            error = "Request body is required.";
            return false;
        }

        return ValidateReminderContent(request.Title, request.Message, request.Category, request.ReminderAt, out error);
    }

    private static bool ValidateReminderContent(
        string title,
        string message,
        string? category,
        DateTime reminderAt,
        out string error)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            error = "Title is required.";
            return false;
        }

        if (title.Trim().Length > 200)
        {
            error = "Title cannot exceed 200 characters.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            error = "Message is required.";
            return false;
        }

        if (message.Trim().Length > 500)
        {
            error = "Message cannot exceed 500 characters.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(category) && category.Trim().Length > 50)
        {
            error = "Category cannot exceed 50 characters.";
            return false;
        }

        if (reminderAt == default)
        {
            error = "ReminderAt is required.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool ValidatePreferencesRequest(UpdateReminderPreferencesRequest? request, out string error)
    {
        if (request is null)
        {
            error = "Request body is required.";
            return false;
        }

        if (request.AppointmentRemindersEnabled is null
            || request.LabResultRemindersEnabled is null
            || request.MessageRemindersEnabled is null
            || request.InAppNotificationsEnabled is null
            || request.EmailRemindersEnabled is null)
        {
            error = "All reminder preference flags are required.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool IsRequestedReminderParticipant(Guid userId, CreateReminderRequest request)
    {
        return request.PatientId == userId
            || request.DoctorId == userId
            || request.AuthorizedMemberId == userId;
    }

    private static string NormalizeCategory(string? category, string title, string message)
    {
        if (!string.IsNullOrWhiteSpace(category))
        {
            return category.Trim();
        }

        var searchableText = string.Join(' ', title, message);
        if (ContainsTerm(searchableText, "appointment", "clinic", "visit"))
        {
            return "Appointment";
        }

        if (ContainsTerm(searchableText, "lab", "cbc", "lipid", "result"))
        {
            return "Lab";
        }

        if (ContainsTerm(searchableText, "message", "messages", "chat"))
        {
            return "Message";
        }

        if (ContainsTerm(searchableText, "imaging", "radiology", "mri", "x-ray"))
        {
            return "Imaging";
        }

        return "General";
    }

    private static bool ContainsTerm(string value, params string[] terms)
    {
        foreach (var term in terms)
        {
            var index = value.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            while (index >= 0)
            {
                var beforeIsBoundary = index == 0 || !char.IsLetterOrDigit(value[index - 1]);
                var afterIndex = index + term.Length;
                var afterIsBoundary = afterIndex == value.Length || !char.IsLetterOrDigit(value[afterIndex]);

                if (beforeIsBoundary && afterIsBoundary)
                {
                    return true;
                }

                index = value.IndexOf(term, index + 1, StringComparison.OrdinalIgnoreCase);
            }
        }

        return false;
    }

    private static bool TryNormalizeStatus(string? status, out string normalizedStatus, out string error)
    {
        error = string.Empty;
        normalizedStatus = string.IsNullOrWhiteSpace(status) ? "all" : status.Trim().ToLowerInvariant();

        switch (normalizedStatus)
        {
            case "all":
            case "upcoming":
            case "past":
                return true;
            default:
                error = "Invalid reminder status. Use all, upcoming, or past.";
                return false;
        }
    }

    private static void NormalizePaging(ref int page, ref int pageSize)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, MaxPageSize);
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
}
