using System.Security.Claims;
using HSCSAPI.Data;
using HSCSAPI.DTOs.Notifications;
using HSCSAPI.Models.Notifications;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI.Services.Notifications;

public class NotificationsService : INotificationsService
{
    private const int MaxPageSize = 100;

    private readonly AppDbContext _dbContext;

    public NotificationsService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ActionResult<NotificationListResponse>> GetMyNotificationsAsync(
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

        if (!TryNormalizeReadStatus(status, out var isRead, out var statusError))
        {
            return new BadRequestObjectResult(statusError);
        }

        NormalizePaging(ref page, ref pageSize);

        var userNotifications = _dbContext.Notifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId.Value);

        var unreadCount = await userNotifications
            .CountAsync(notification => !notification.IsRead, cancellationToken);
        var readCount = await userNotifications
            .CountAsync(notification => notification.IsRead, cancellationToken);

        var filteredNotifications = userNotifications;
        if (isRead.HasValue)
        {
            filteredNotifications = filteredNotifications
                .Where(notification => notification.IsRead == isRead.Value);
        }

        var totalCount = await filteredNotifications.CountAsync(cancellationToken);
        var notifications = await filteredNotifications
            .OrderBy(notification => notification.IsRead)
            .ThenByDescending(notification => notification.CreatedAt)
            .ThenByDescending(notification => notification.NotificationId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(notification => new NotificationResponse
            {
                NotificationId = notification.NotificationId,
                Title = notification.Title,
                Message = notification.Message,
                IsRead = notification.IsRead,
                Category = InferNotificationCategory(notification.Title, notification.Message),
                CreatedAt = notification.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new OkObjectResult(new NotificationListResponse
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            UnreadCount = unreadCount,
            ReadCount = readCount,
            Items = notifications
        });
    }

    public async Task<ActionResult<NotificationResponse>> UpdateReadStatusAsync(
        Guid notificationId,
        UpdateNotificationReadStatusRequest? request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId(user);
        if (userId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        if (request?.IsRead is null)
        {
            return new BadRequestObjectResult("IsRead is required.");
        }

        var notification = await _dbContext.Notifications
            .FirstOrDefaultAsync(
                x => x.NotificationId == notificationId && x.UserId == userId.Value,
                cancellationToken);

        if (notification is null)
        {
            return new NotFoundObjectResult("Notification not found.");
        }

        notification.IsRead = request.IsRead.Value;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new OkObjectResult(MapNotification(notification));
    }

    public async Task<ActionResult<MarkNotificationsReadResponse>> MarkAllAsReadAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId(user);
        if (userId is null)
        {
            return new UnauthorizedObjectResult("Invalid token.");
        }

        var notifications = await _dbContext.Notifications
            .Where(notification => notification.UserId == userId.Value && !notification.IsRead)
            .ToListAsync(cancellationToken);

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new OkObjectResult(new MarkNotificationsReadResponse
        {
            UpdatedNotifications = notifications.Count,
            MarkedAsReadAt = DateTime.UtcNow
        });
    }

    private static NotificationResponse MapNotification(Notification notification)
    {
        return new NotificationResponse
        {
            NotificationId = notification.NotificationId,
            Title = notification.Title,
            Message = notification.Message,
            IsRead = notification.IsRead,
            Category = InferNotificationCategory(notification.Title, notification.Message),
            CreatedAt = notification.CreatedAt
        };
    }

    private static string InferNotificationCategory(string title, string? message)
    {
        var searchableText = string.Join(' ', title, message).Trim();

        if (ContainsTerm(searchableText, "appointment"))
        {
            return "Appointment";
        }

        if (ContainsTerm(searchableText, "message"))
        {
            return "Message";
        }

        if (ContainsTerm(searchableText, "imaging", "radiology", "x-ray"))
        {
            return "Imaging";
        }

        if (ContainsTerm(searchableText, "lab"))
        {
            return "Lab";
        }

        if (ContainsTerm(searchableText, "maintenance", "system"))
        {
            return "System";
        }

        if (ContainsTerm(searchableText, "record", "result"))
        {
            return "MedicalRecord";
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

    private static bool TryNormalizeReadStatus(string? status, out bool? isRead, out string error)
    {
        error = string.Empty;
        isRead = null;
        var normalizedStatus = NormalizeOptional(status)?.ToLowerInvariant() ?? "all";

        switch (normalizedStatus)
        {
            case "all":
                return true;
            case "read":
                isRead = true;
                return true;
            case "unread":
                isRead = false;
                return true;
            default:
                error = "Invalid notification status. Use all, unread, or read.";
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

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
