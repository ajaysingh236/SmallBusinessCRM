using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmallBusinessCRM.Api.Services;

namespace SmallBusinessCRM.Api.Controllers;

[ApiController]
[Route("api/calendar")]
[Authorize]
public class CalendarSyncController(
    ICalendarSyncService calendarService,
    ICurrentUserService currentUser) : ControllerBase
{
    public record ConnectCalendarRequest(string Provider, string? AccessToken, string? RefreshToken);

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        if (currentUser.UserId is null) return Unauthorized();

        var integration = await calendarService.GetUserIntegrationAsync(currentUser.UserId.Value, ct);
        if (integration is null)
        {
            return Ok(new
            {
                isConnected = false,
                provider = (string?)null,
                lastSyncedAtUtc = (DateTime?)null,
                isSyncEnabled = false
            });
        }

        return Ok(new
        {
            isConnected = true,
            provider = integration.Provider,
            lastSyncedAtUtc = integration.LastSyncedAtUtc,
            isSyncEnabled = integration.IsSyncEnabled
        });
    }

    [HttpPost("connect")]
    public async Task<IActionResult> Connect([FromBody] ConnectCalendarRequest request, CancellationToken ct)
    {
        if (currentUser.UserId is null) return Unauthorized();

        var integration = await calendarService.ConnectCalendarAsync(
            currentUser.UserId.Value,
            request.Provider,
            request.AccessToken,
            request.RefreshToken,
            ct);

        return Ok(new
        {
            message = $"Successfully connected {integration.Provider} calendar.",
            provider = integration.Provider,
            isSyncEnabled = integration.IsSyncEnabled,
            connectedAt = DateTime.UtcNow
        });
    }

    [HttpPost("sync")]
    public async Task<IActionResult> Sync(CancellationToken ct)
    {
        if (currentUser.UserId is null) return Unauthorized();

        var events = await calendarService.SyncTasksToCalendarAsync(currentUser.UserId.Value, ct);

        return Ok(new
        {
            syncedCount = events.Count,
            events,
            syncedAtUtc = DateTime.UtcNow
        });
    }
}
