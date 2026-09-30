using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmallBusinessCRM.Api.Data;
using SmallBusinessCRM.Api.Models;

namespace SmallBusinessCRM.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = "Admin,Manager,SalesRepresentative")]
public class ReportsController(CrmDbContext db) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var leads = await db.Leads.ToListAsync(ct);
        var total = leads.Count;
        var won = leads.Count(x => x.Status == LeadStatus.ClosedWon);
        var open = leads.Where(x => x.Status is not LeadStatus.ClosedWon and not LeadStatus.ClosedLost).ToList();
        var overdue = await db.Tasks.CountAsync(x => !x.IsCompleted && x.DueDateUtc < DateTime.UtcNow, ct);

        return Ok(new
        {
            totalLeads = total,
            openPipeline = open.Sum(x => x.Value),
            conversionRate = total == 0 ? 0 : Math.Round(won * 100m / total, 2),
            overdueTasks = overdue,
            workload = await db.Users.Where(x => x.IsActive).Select(u => new { user = u.FullName, leads = u.AssignedLeads.Count }).ToListAsync(ct),
            pipeline = leads.GroupBy(x => x.Status).Select(g => new { status = g.Key.ToString(), count = g.Count(), value = g.Sum(x => x.Value) }).ToList()
        });
    }

    [HttpGet("analytics")]
    public async Task<IActionResult> Analytics(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var sixMonthsAgo = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-5);

        var leads = await db.Leads.AsNoTracking().ToListAsync(ct);
        var activities = await db.ActivityLogs.AsNoTracking()
            .Where(x => x.OccurredUtc >= now.AddDays(-30))
            .ToListAsync(ct);

        // 1. Monthly deal velocity (past 6 months)
        var velocity = new List<object>();
        for (var i = 0; i < 6; i++)
        {
            var monthDate = sixMonthsAgo.AddMonths(i);
            var monthStart = new DateTime(monthDate.Year, monthDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthEnd = monthStart.AddMonths(1);

            var monthLeads = leads.Where(l => l.CreatedAt >= monthStart && l.CreatedAt < monthEnd).ToList();
            var wonMonthLeads = leads.Where(l => l.Status == LeadStatus.ClosedWon && l.LastUpdatedUtc >= monthStart && l.LastUpdatedUtc < monthEnd).ToList();

            velocity.Add(new
            {
                month = monthDate.ToString("MMM yyyy"),
                createdCount = monthLeads.Count,
                createdValue = monthLeads.Sum(l => l.Value),
                wonCount = wonMonthLeads.Count,
                wonValue = wonMonthLeads.Sum(l => l.Value)
            });
        }

        // 2. Lead conversion funnel in standard chronological order
        var funnelStages = new[]
        {
            LeadStatus.New,
            LeadStatus.Contacted,
            LeadStatus.DemoScheduled,
            LeadStatus.ProposalSent,
            LeadStatus.ClosedWon
        };

        var funnel = funnelStages.Select(stage =>
        {
            var count = leads.Count(l => l.Status == stage);
            var value = leads.Where(l => l.Status == stage).Sum(l => l.Value);
            return new
            {
                stage = stage.ToString(),
                count,
                value,
                percentage = leads.Count == 0 ? 0 : Math.Round((decimal)count * 100m / leads.Count, 1)
            };
        }).ToList();

        var lostCount = leads.Count(l => l.Status == LeadStatus.ClosedLost);
        var lostValue = leads.Where(l => l.Status == LeadStatus.ClosedLost).Sum(l => l.Value);

        // 3. Sales activity distribution (last 30 days)
        var totalActivities = activities.Count;
        var activityDistribution = Enum.GetValues<ActivityType>()
            .Select(type =>
            {
                var count = activities.Count(a => a.Type == type);
                return new
                {
                    type = type.ToString(),
                    count,
                    percentage = totalActivities == 0 ? 0 : Math.Round((decimal)count * 100m / totalActivities, 1)
                };
            })
            .Where(x => x.count > 0 || totalActivities == 0)
            .ToList();

        return Ok(new
        {
            monthlyDealVelocity = velocity,
            leadConversionFunnel = funnel,
            lostDeals = new { count = lostCount, value = lostValue },
            salesActivityDistribution = activityDistribution
        });
    }
}
