using HSCSAPI.DTOs.Reminders;
using HSCSAPI.Services.Reminders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSCSAPI.Controllers;

[ApiController]
[Route("api/reminders")]
[Authorize]
public class RemindersController : ControllerBase
{
    private readonly IRemindersService _remindersService;

    public RemindersController(IRemindersService remindersService)
    {
        _remindersService = remindersService;
    }

    [HttpGet]
    public async Task<ActionResult<ReminderListResponse>> GetMyReminders(
        [FromQuery] string? status,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken)
    {
        return await _remindersService.GetMyRemindersAsync(status, page, pageSize, User, cancellationToken);
    }

    [HttpPost]
    public async Task<ActionResult<ReminderResponse>> CreateReminder(
        [FromBody] CreateReminderRequest request,
        CancellationToken cancellationToken)
    {
        return await _remindersService.CreateReminderAsync(request, User, cancellationToken);
    }

    [HttpGet("{reminderId:guid}")]
    public async Task<ActionResult<ReminderResponse>> GetReminder(
        Guid reminderId,
        CancellationToken cancellationToken)
    {
        return await _remindersService.GetReminderAsync(reminderId, User, cancellationToken);
    }

    [HttpPut("{reminderId:guid}")]
    public async Task<ActionResult<ReminderResponse>> UpdateReminder(
        Guid reminderId,
        [FromBody] UpdateReminderRequest request,
        CancellationToken cancellationToken)
    {
        return await _remindersService.UpdateReminderAsync(reminderId, request, User, cancellationToken);
    }

    [HttpPatch("{reminderId:guid}/dismiss")]
    public async Task<ActionResult<ReminderResponse>> DismissReminder(
        Guid reminderId,
        CancellationToken cancellationToken)
    {
        return await _remindersService.DismissReminderAsync(reminderId, User, cancellationToken);
    }

    // last end point added - already-added
    [HttpGet("preferences")]
    public async Task<ActionResult<ReminderPreferencesResponse>> GetMyPreferences(CancellationToken cancellationToken)
    {
        return await _remindersService.GetMyPreferencesAsync(User, cancellationToken);
    }

    // last end point added - already-added
    [HttpPut("preferences")]
    public async Task<ActionResult<ReminderPreferencesResponse>> UpdateMyPreferences(
        [FromBody] UpdateReminderPreferencesRequest request,
        CancellationToken cancellationToken)
    {
        return await _remindersService.UpdateMyPreferencesAsync(request, User, cancellationToken);
    }
}
