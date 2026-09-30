using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmallBusinessCRM.Api.Authorization;
using SmallBusinessCRM.Api.Data;
using SmallBusinessCRM.Api.Dtos;
using SmallBusinessCRM.Api.Services;

namespace SmallBusinessCRM.Api.Controllers;

[ApiController, Route("api/activityfeed"), Authorize]
public class ActivityFeedController(
    CrmDbContext db,
    IActivityService service,
    IAuthorizationService authorization,
    IRealtimeNotifier realtime) : ControllerBase
{
    [HttpGet("recent")]
    public async Task<IActionResult> Recent(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.ActivityLogs
            .Include(x => x.Lead)
            .Include(x => x.User)
            .OrderByDescending(x => x.OccurredUtc);

        var total = await query.CountAsync(ct);
        var data = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.Type,
                x.Message,
                x.OccurredUtc,
                lead = x.Lead != null ? x.Lead.CompanyName : "General",
                user = x.User == null ? "System" : x.User.FullName
            })
            .ToListAsync(ct);

        return Ok(new { page, pageSize, total, data });
    }

    [HttpGet("lead/{leadId:int}")]
    public async Task<IActionResult> ByLead(int leadId, CancellationToken ct)
    {
        var lead = await db.Leads.FirstOrDefaultAsync(x => x.Id == leadId, ct);
        if (lead is null) return NotFound();

        var authResult = await authorization.AuthorizeAsync(User, lead, new LeadAuthorizationRequirement(LeadOperation.Read));
        if (!authResult.Succeeded) return Forbid();

        var activities = await db.ActivityLogs
            .Where(x => x.LeadId == leadId)
            .Include(x => x.User)
            .OrderByDescending(x => x.OccurredUtc)
            .Select(x => new
            {
                x.Id,
                x.Type,
                x.Message,
                x.OccurredUtc,
                user = x.User == null ? "System" : x.User.FullName
            })
            .ToListAsync(ct);

        return Ok(activities);
    }

    [HttpPost("log/{leadId:int}")]
    public async Task<IActionResult> Log(int leadId, LogActivityRequest request, CancellationToken ct)
    {
        var lead = await db.Leads.FirstOrDefaultAsync(x => x.Id == leadId, ct);
        if (lead is null) return NotFound();

        var authResult = await authorization.AuthorizeAsync(User, lead, new LeadAuthorizationRequirement(LeadOperation.Edit));
        if (!authResult.Succeeded) return Forbid();

        var item = await service.LogAsync(leadId, request, ct: ct);
        if (item is null) return NotFound();

        await realtime.ActivityAsync(new
        {
            leadId = lead.Id,
            lead = lead.CompanyName,
            type = item.Type.ToString(),
            message = item.Message,
            occurredUtc = item.OccurredUtc
        }, ct);

        return Ok(item);
    }
}
