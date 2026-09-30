using Microsoft.EntityFrameworkCore;
using SmallBusinessCRM.Api.Data;
using SmallBusinessCRM.Api.Models;

namespace SmallBusinessCRM.Api.Services;

public class ReminderWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<ReminderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = configuration.GetValue<int?>("ReminderWorker:IntervalMinutes") ?? 5;
        logger.LogInformation("ReminderWorker started with interval: {IntervalMinutes} minute(s).", intervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();

                // In background jobs, there is no active authenticated HTTP user context.
                // We must use IgnoreQueryFilters() so global multitenancy/ownership filters do not exclude overdue tasks.
                var due = await db.Tasks
                    .IgnoreQueryFilters()
                    .Where(x => !x.IsCompleted && !x.ReminderSent && x.DueDateUtc <= DateTime.UtcNow)
                    .ToListAsync(stoppingToken);

                if (due.Count > 0)
                {
                    foreach (var task in due)
                    {
                        task.ReminderSent = true;
                        db.ActivityLogs.Add(new ActivityLog
                        {
                            LeadId = task.LeadId,
                            UserId = task.AssignedUserId,
                            Type = ActivityType.System,
                            Message = $"Reminder: task '{task.Title}' is overdue.",
                            OccurredUtc = DateTime.UtcNow
                        });
                    }

                    await db.SaveChangesAsync(stoppingToken);
                    logger.LogInformation("Processed and logged reminders for {Count} overdue task(s).", due.Count);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("ReminderWorker cancellation requested. Exiting loop.");
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Reminder processing encountered an unhandled exception.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
