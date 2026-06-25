using System.Security.Claims;
using HSCSAPI.DTOs.Reminders;
using Microsoft.AspNetCore.Mvc;

namespace HSCSAPI.Services.Reminders;

public interface IRemindersService
{
    Task<ActionResult<ReminderListResponse>> GetMyRemindersAsync(
        string? status,
        int page,
        int pageSize,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<ReminderResponse>> CreateReminderAsync(
        CreateReminderRequest? request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<ReminderResponse>> GetReminderAsync(
        Guid reminderId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<ReminderResponse>> UpdateReminderAsync(
        Guid reminderId,
        UpdateReminderRequest? request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<ReminderResponse>> DismissReminderAsync(
        Guid reminderId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<ReminderPreferencesResponse>> GetMyPreferencesAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<ActionResult<ReminderPreferencesResponse>> UpdateMyPreferencesAsync(
        UpdateReminderPreferencesRequest? request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);
}
