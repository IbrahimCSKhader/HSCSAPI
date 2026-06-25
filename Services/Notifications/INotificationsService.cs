using System.Security.Claims;
using HSCSAPI.DTOs.Notifications;
using Microsoft.AspNetCore.Mvc;

namespace HSCSAPI.Services.Notifications;

public interface INotificationsService
{
    Task<ActionResult<NotificationListResponse>> GetMyNotificationsAsync(
        string? status,
        int page,
        int pageSize,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<NotificationResponse>> UpdateReadStatusAsync(
        Guid notificationId,
        UpdateNotificationReadStatusRequest? request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<MarkNotificationsReadResponse>> MarkAllAsReadAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);
}
