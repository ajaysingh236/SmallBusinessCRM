using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmallBusinessCRM.Api.Data;
using SmallBusinessCRM.Api.Dtos;
using SmallBusinessCRM.Api.Models;
using SmallBusinessCRM.Api.Services;

namespace SmallBusinessCRM.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = "Admin,Manager,SalesRepresentative")]
public class ReportsController(CrmDbContext db, ICrmCacheService cache) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        const string cacheKey = "crm:default:reports:dashboard";

        var result = await cache.GetOrSetAsync(cacheKey, async () =>
        {
            var leads = await db.Leads.ToListAsync(ct);
            var total = leads.Count;
            var won = leads.Count(x => x.Status == LeadStatus.ClosedWon);
            var open = leads.Where(x => x.Status is not LeadStatus.ClosedWon and not LeadStatus.ClosedLost).ToList();
            var overdue = await db.Tasks.CountAsync(x => !x.IsCompleted && x.DueDateUtc < DateTime.UtcNow, ct);

            return new DashboardReportDto
            {
                TotalLeads = total,
                OpenPipeline = open.Sum(x => x.Value),
                ConversionRate = total == 0 ? 0 : Math.Round(won * 100m / total, 2),
                OverdueTasks = overdue,
                Workload = await db.Users.Where(x => x.IsActive)
                    .Select(u => new WorkloadDto { User = u.FullName, Leads = u.AssignedLeads.Count })
                    .ToListAsync(ct),
                Pipeline = leads.GroupBy(x => x.Status)
                    .Select(g => new PipelineStageDto { Status = g.Key.ToString(), Count = g.Count(), Value = g.Sum(x => x.Value) })
                    .ToList()
            };
        }, TimeSpan.FromMinutes(5), ct);

        return Ok(result);
    }

    [HttpGet("analytics")]
    public async Task<IActionResult> Analytics(CancellationToken ct)
    {
        const string cacheKey = "crm:default:reports:analytics";

        var result = await cache.GetOrSetAsync(cacheKey, async () =>
        {
            var now = DateTime.UtcNow;
            var sixMonthsAgo = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-5);

            var leads = await db.Leads.AsNoTracking().ToListAsync(ct);
            var activities = await db.ActivityLogs.AsNoTracking()
                .Where(x => x.OccurredUtc >= now.AddDays(-30))
                .ToListAsync(ct);

            // 1. Monthly deal velocity (past 6 months)
            var velocity = new List<DealVelocityDto>();
            for (var i = 0; i < 6; i++)
            {
                var monthDate = sixMonthsAgo.AddMonths(i);
                var monthStart = new DateTime(monthDate.Year, monthDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var monthEnd = monthStart.AddMonths(1);

                var monthLeads = leads.Where(l => l.CreatedAt >= monthStart && l.CreatedAt < monthEnd).ToList();
                var wonMonthLeads = leads.Where(l => l.Status == LeadStatus.ClosedWon && l.LastUpdatedUtc >= monthStart && l.LastUpdatedUtc < monthEnd).ToList();

                velocity.Add(new DealVelocityDto
                {
                    Month = monthDate.ToString("MMM yyyy"),
                    CreatedCount = monthLeads.Count,
                    CreatedValue = monthLeads.Sum(l => l.Value),
                    WonCount = wonMonthLeads.Count,
                    WonValue = wonMonthLeads.Sum(l => l.Value)
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
                return new FunnelStageDto
                {
                    Stage = stage.ToString(),
                    Count = count,
                    Value = value,
                    Percentage = leads.Count == 0 ? 0 : Math.Round((decimal)count * 100m / leads.Count, 1)
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
                    return new ActivityDistributionDto
                    {
                        Type = type.ToString(),
                        Count = count,
                        Percentage = totalActivities == 0 ? 0 : Math.Round((decimal)count * 100m / totalActivities, 1)
                    };
                })
                .Where(x => x.Count > 0 || totalActivities == 0)
                .ToList();

            return new AnalyticsReportDto
            {
                MonthlyDealVelocity = velocity,
                LeadConversionFunnel = funnel,
                LostDeals = new LostDealsDto { Count = lostCount, Value = lostValue },
                SalesActivityDistribution = activityDistribution
            };
        }, TimeSpan.FromMinutes(5), ct);

        return Ok(result);
    }
}
