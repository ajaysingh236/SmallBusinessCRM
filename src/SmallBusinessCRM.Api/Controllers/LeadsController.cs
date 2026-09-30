using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmallBusinessCRM.Api.Authorization;
using SmallBusinessCRM.Api.Data;
using SmallBusinessCRM.Api.Dtos;
using SmallBusinessCRM.Api.Models;
using SmallBusinessCRM.Api.Services;

namespace SmallBusinessCRM.Api.Controllers;

[ApiController, Route("api/leads"), Authorize]
public sealed class LeadsController(
    CrmDbContext db,
    IAuthorizationService authorization,
    IAuditService audit,
    IRealtimeNotifier realtime,
    IEmailDispatcher email,
    ICurrentUserService currentUser,
    ICrmCacheService cache,
    IWebhookNotificationService webhooks) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await db.Leads
            .Include(x => x.AssignedUser)
            .Include(x => x.Tasks)
            .OrderByDescending(x => x.PriorityScore)
            .ToListAsync(ct));

    [HttpPost, Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Create(CreateLeadRequest request, CancellationToken ct)
    {
        var lead = new Lead
        {
            Title = string.IsNullOrWhiteSpace(request.Title) ? request.CompanyName : request.Title,
            CompanyName = request.CompanyName,
            ContactName = request.ContactName,
            Email = request.Email,
            Phone = request.Phone,
            Value = request.Value,
            AssignedUserId = request.AssignedUserId,
            CreatedAt = DateTime.UtcNow,
            LastUpdatedUtc = DateTime.UtcNow
        };

        db.Leads.Add(lead);
        await db.SaveChangesAsync(ct);

        await audit.WriteAsync("LeadCreated", nameof(Lead), lead.Id, newValues: new { lead.Title, lead.Value, lead.AssignedUserId }, ct: ct);
        await cache.InvalidateReportsCacheAsync("default", ct);
        return CreatedAtAction(nameof(Get), new { id = lead.Id }, lead);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var lead = await db.Leads
            .Include(x => x.AssignedUser)
            .Include(x => x.Tasks)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (lead is null) return NotFound();

        var authResult = await authorization.AuthorizeAsync(User, lead, new LeadAuthorizationRequirement(LeadOperation.Read));
        if (!authResult.Succeeded) return Forbid();

        return Ok(lead);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateLeadRequest request, CancellationToken ct)
    {
        var lead = await db.Leads.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (lead is null) return NotFound();

        if (!(await authorization.AuthorizeAsync(User, lead, new LeadAuthorizationRequirement(LeadOperation.Edit))).Succeeded)
            return Forbid();

        if (!currentUser.IsManagerOrAdmin && request.AssignedUserId != lead.AssignedUserId)
            return Forbid();

        var previous = new { lead.Title, lead.Value, lead.AssignedUserId };
        lead.Title = request.Title;
        lead.Value = request.Value;
        lead.AssignedUserId = request.AssignedUserId;
        lead.LastUpdatedUtc = DateTime.UtcNow;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "This lead was modified by another user. Please refresh and retry." });
        }

        await audit.WriteAsync("LeadUpdated", nameof(Lead), lead.Id, previous, new { lead.Title, lead.Value, lead.AssignedUserId }, ct);
        await cache.InvalidateReportsCacheAsync("default", ct);
        return Ok(lead);
    }

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, UpdateLeadStatusRequest request, CancellationToken ct)
    {
        var lead = await db.Leads.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (lead is null) return NotFound();

        if (!(await authorization.AuthorizeAsync(User, lead, new LeadAuthorizationRequirement(LeadOperation.Edit))).Succeeded)
            return Forbid();

        var previous = lead.Status;
        lead.Status = request.Status;
        lead.LastUpdatedUtc = DateTime.UtcNow;

        db.ActivityLogs.Add(new ActivityLog
        {
            LeadId = lead.Id,
            Type = ActivityType.StatusChanged,
            Message = $"Stage changed from {previous} to {lead.Status}."
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "Lead stage was modified concurrently. Please reload the pipeline." });
        }

        await audit.WriteAsync("LeadStageChanged", nameof(Lead), lead.Id, new { status = previous }, new { status = lead.Status }, ct);
        await realtime.ActivityAsync(new
        {
            leadId = lead.Id,
            lead = lead.CompanyName,
            type = "StatusChanged",
            message = $"Stage changed from {previous} to {lead.Status}.",
            occurredUtc = DateTime.UtcNow
        }, ct);

        await realtime.LeadStageChangedAsync(new
        {
            leadId = lead.Id,
            companyName = lead.CompanyName,
            previousStatus = previous.ToString(),
            newStatus = lead.Status.ToString(),
            priorityScore = lead.PriorityScore,
            updatedBy = User.Identity?.Name ?? "User"
        }, ct);

        if (lead.Status == LeadStatus.ClosedWon && previous != LeadStatus.ClosedWon)
        {
            await webhooks.NotifyDealWonAsync(lead, User.Identity?.Name ?? "Representative", ct);
        }

        await cache.InvalidateReportsCacheAsync("default", ct);
        return Ok(new { lead.Id, lead.Status });
    }

    [HttpPatch("{id:int}/assignment"), Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Assign(int id, AssignLeadRequest request, CancellationToken ct)
    {
        var lead = await db.Leads.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (lead is null) return NotFound();

        var assignee = await db.Users.FindAsync(new object?[] { request.AssignedUserId }, ct);
        if (assignee is null || !assignee.IsActive)
            return BadRequest(new { message = "Assignee is not active." });

        var previousId = lead.AssignedUserId;
        lead.AssignedUserId = request.AssignedUserId;
        lead.LastUpdatedUtc = DateTime.UtcNow;

        db.ActivityLogs.Add(new ActivityLog
        {
            LeadId = id,
            Type = ActivityType.System,
            Message = $"Lead assigned to {assignee.FullName}."
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "Lead assignment conflicted with another concurrent change. Please refresh." });
        }

        await audit.WriteAsync("LeadAssigned", nameof(Lead), lead.Id, new { assignedUserId = previousId }, new { lead.AssignedUserId }, ct);
        await realtime.LeadAssignedAsync(assignee.Id, new { leadId = lead.Id, title = lead.Title, message = $"You were assigned {lead.Title}." }, ct);
        await realtime.ActivityAsync(new { leadId = lead.Id, lead = lead.CompanyName, type = "Assignment", message = $"Lead assigned to {assignee.FullName}.", occurredUtc = DateTime.UtcNow }, ct);
        await cache.InvalidateReportsCacheAsync("default", ct);

        return Ok(lead);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var lead = await db.Leads.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (lead is null) return NotFound();

        if (!(await authorization.AuthorizeAsync(User, lead, new LeadAuthorizationRequirement(LeadOperation.Delete))).Succeeded)
            return Forbid();

        lead.IsDeleted = true;
        lead.DeletedAtUtc = DateTime.UtcNow;
        lead.DeletedByUserId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "Lead delete conflicted with another concurrent change." });
        }

        await audit.WriteAsync("LeadSoftDeleted", nameof(Lead), lead.Id, new { lead.Title }, ct: ct);
        await cache.InvalidateReportsCacheAsync("default", ct);
        return NoContent();
    }

    [HttpPost("{id:int}/restore"), Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Restore(int id, CancellationToken ct)
    {
        var lead = await db.Leads.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (lead is null || !lead.IsDeleted) return NotFound();

        lead.IsDeleted = false;
        lead.DeletedAtUtc = null;
        lead.DeletedByUserId = null;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "Lead restore conflicted with another concurrent change." });
        }

        await audit.WriteAsync("LeadRestored", nameof(Lead), lead.Id, ct: ct);
        await cache.InvalidateReportsCacheAsync("default", ct);
        return Ok(lead);
    }

    [HttpPost("{id:int}/tasks")]
    public async Task<IActionResult> AddTask(int id, CreateTaskRequest request, CancellationToken ct)
    {
        var lead = await db.Leads.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (lead is null) return NotFound();

        if (!(await authorization.AuthorizeAsync(User, lead, new LeadAuthorizationRequirement(LeadOperation.Edit))).Succeeded)
            return Forbid();

        var task = new CrmTask
        {
            LeadId = id,
            Title = request.Title,
            DueDateUtc = request.DueDateUtc,
            AssignedUserId = request.AssignedUserId,
            EmailTemplate = request.EmailTemplate
        };

        db.Tasks.Add(task);
        await db.SaveChangesAsync(ct);

        if (request.EmailTemplate != EmailTemplateKind.None)
        {
            task.EmailSent = await email.SendTaskEmailAsync(lead, task, ct);
            await db.SaveChangesAsync(ct);
        }

        await audit.WriteAsync("TaskCreated", nameof(CrmTask), task.Id, newValues: new { task.Title, task.DueDateUtc, task.EmailTemplate }, ct: ct);
        return Ok(task);
    }

    [HttpGet("audit"), Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Audit(CancellationToken ct) =>
        Ok(await db.Leads.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct));
}
