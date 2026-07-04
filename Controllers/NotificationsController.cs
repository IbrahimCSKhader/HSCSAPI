using HSCSAPI.DTOs.Notifications;
using HSCSAPI.Services.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSCSAPI.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationsService _notificationsService;

    public NotificationsController(INotificationsService notificationsService)
    {
        _notificationsService = notificationsService;
    }

    // last end point added - already-added
    [HttpGet]
    public async Task<ActionResult<NotificationListResponse>> GetMyNotifications(
        [FromQuery] string? status,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken)
    {
        return await _notificationsService.GetMyNotificationsAsync(status, page, pageSize, User, cancellationToken);
    }

    [HttpPatch("{notificationId:guid}/read-status")]
    public async Task<ActionResult<NotificationResponse>> UpdateReadStatus(
        Guid notificationId,
        [FromBody] UpdateNotificationReadStatusRequest request,
        CancellationToken cancellationToken)
    {
        return await _notificationsService.UpdateReadStatusAsync(notificationId, request, User, cancellationToken);
    }

    [HttpPatch("read-all")]
    public async Task<ActionResult<MarkNotificationsReadResponse>> MarkAllAsRead(CancellationToken cancellationToken)
    {
        return await _notificationsService.MarkAllAsReadAsync(User, cancellationToken);
    }

    // last end point added
    [HttpDelete("{notificationId:guid}")]
    public async Task<IActionResult> Delete(Guid notificationId, CancellationToken cancellationToken)
    {
        return await _notificationsService.DeleteAsync(notificationId, User, cancellationToken);
    }
}
