using Microsoft.EntityFrameworkCore;
using SmallBusinessCRM.Api.Data;
using SmallBusinessCRM.Api.Models;

namespace SmallBusinessCRM.Api.Services;

public record CalendarEventDto(
    string Id,
    string Title,
    string Description,
    DateTime StartUtc,
    DateTime EndUtc,
    string? Location
);

public interface ICalendarSyncService
{
    Task<CalendarIntegration?> GetUserIntegrationAsync(int userId, CancellationToken ct = default);
    Task<CalendarIntegration> ConnectCalendarAsync(int userId, string provider, string? accessToken, string? refreshToken, CancellationToken ct = default);
    Task<IReadOnlyList<CalendarEventDto>> SyncTasksToCalendarAsync(int userId, CancellationToken ct = default);
}

public class CalendarSyncService(
    CrmDbContext db,
    ILogger<CalendarSyncService> logger) : ICalendarSyncService
{
    public async Task<CalendarIntegration?> GetUserIntegrationAsync(int userId, CancellationToken ct = default)
    {
        return await db.CalendarIntegrations
            .FirstOrDefaultAsync(x => x.UserId == userId, ct);
    }

    public async Task<CalendarIntegration> ConnectCalendarAsync(
        int userId,
        string provider,
        string? accessToken,
        string? refreshToken,
        CancellationToken ct = default)
    {
        var integration = await db.CalendarIntegrations
            .FirstOrDefaultAsync(x => x.UserId == userId, ct);

        if (integration is null)
        {
            integration = new CalendarIntegration
            {
                UserId = userId,
                Provider = provider,
                AccessToken = accessToken ?? "simulated_oauth_access_token_" + Guid.NewGuid().ToString("N"),
                RefreshToken = refreshToken ?? "simulated_oauth_refresh_token_" + Guid.NewGuid().ToString("N"),
                ExpiresAtUtc = DateTime.UtcNow.AddHours(24),
                CalendarId = "primary",
                IsSyncEnabled = true,
                LastSyncedAtUtc = DateTime.UtcNow
            };
            db.CalendarIntegrations.Add(integration);
        }
        else
        {
            integration.Provider = provider;
            integration.AccessToken = accessToken ?? integration.AccessToken;
            integration.RefreshToken = refreshToken ?? integration.RefreshToken;
            integration.ExpiresAtUtc = DateTime.UtcNow.AddHours(24);
            integration.IsSyncEnabled = true;
            integration.LastSyncedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Calendar integration connected for User {UserId} with provider {Provider}.", userId, provider);
        return integration;
    }

    public async Task<IReadOnlyList<CalendarEventDto>> SyncTasksToCalendarAsync(int userId, CancellationToken ct = default)
    {
        var integration = await GetUserIntegrationAsync(userId, ct);
        if (integration is null || !integration.IsSyncEnabled)
        {
            return Array.Empty<CalendarEventDto>();
        }

        var tasks = await db.Tasks
            .Include(x => x.Lead)
            .Where(x => !x.IsCompleted && x.AssignedUserId == userId)
            .ToListAsync(ct);

        var events = new List<CalendarEventDto>();
        foreach (var task in tasks)
        {
            var eventItem = new CalendarEventDto(
                Id: $"crm-task-{task.Id}",
                Title: $"CRM: {task.Title} - {task.Lead?.CompanyName ?? "Account"}",
                Description: $"Due Date: {task.DueDateUtc:u}\nLead: {task.Lead?.ContactName}\nEmail: {task.Lead?.Email}\nTemplate: {task.EmailTemplate}",
                StartUtc: task.DueDateUtc,
                EndUtc: task.DueDateUtc.AddHours(1),
                Location: "Remote Video Call / Phone"
            );
            events.Add(eventItem);
        }

        integration.LastSyncedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Synced {Count} pending tasks to {Provider} calendar for User {UserId}.", events.Count, integration.Provider, userId);
        return events;
    }
}
