using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmallBusinessCRM.Api.Data;
using SmallBusinessCRM.Api.Models;
using SmallBusinessCRM.Api.Services;

namespace SmallBusinessCRM.Api.Controllers;

[ApiController]
public class EmailTrackingController(
    CrmDbContext db,
    IDripEmailService dripEmailService,
    ILogger<EmailTrackingController> logger) : ControllerBase
{
    // Standard 1x1 transparent GIF image bytes (43 bytes)
    private static readonly byte[] TransparentGifBytes = new byte[]
    {
        0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x01, 0x00,
        0x01, 0x00, 0x80, 0x00, 0x00, 0xFF, 0xFF, 0xFF,
        0x00, 0x00, 0x00, 0x21, 0xF9, 0x04, 0x01, 0x00,
        0x00, 0x00, 0x00, 0x2C, 0x00, 0x00, 0x00, 0x00,
        0x01, 0x00, 0x01, 0x00, 0x00, 0x02, 0x02, 0x44,
        0x01, 0x00, 0x3B
    };

    /// <summary>
    /// Invisible 1x1 GIF tracking pixel loaded by email clients upon opening.
    /// </summary>
    [HttpGet("api/email/track/open/{id:guid}")]
    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> TrackEmailOpen(Guid id, CancellationToken ct)
    {
        try
        {
            var tracking = await db.EmailTrackings
                .IgnoreQueryFilters()
                .Include(x => x.Lead)
                .FirstOrDefaultAsync(x => x.Id == id, ct);

            if (tracking is not null)
            {
                var now = DateTime.UtcNow;
                if (!tracking.IsOpened)
                {
                    tracking.IsOpened = true;
                    tracking.OpenedAtUtc = now;
                    tracking.UserAgent = Request.Headers.UserAgent.ToString();
                    tracking.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

                    db.ActivityLogs.Add(new ActivityLog
                    {
                        LeadId = tracking.LeadId,
                        Type = ActivityType.Email,
                        Message = $"Email opened by recipient: '{tracking.Subject}' ({tracking.StepName}).",
                        OccurredUtc = now
                    });

                    await db.SaveChangesAsync(ct);
                    logger.LogInformation("Email open recorded for tracking ID {TrackingId}, Lead {LeadId}", id, tracking.LeadId);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to record email open for tracking ID {TrackingId}", id);
        }

        // Always return transparent 1x1 GIF with zero-cache headers
        Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["Expires"] = "0";

        return File(TransparentGifBytes, "image/gif");
    }

    /// <summary>
    /// Tracks outbound email link clicks and safely redirects the user.
    /// </summary>
    [HttpGet("api/email/track/click/{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> TrackEmailClick(Guid id, [FromQuery] string? target, CancellationToken ct)
    {
        try
        {
            var tracking = await db.EmailTrackings
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == id, ct);

            if (tracking is not null)
            {
                tracking.ClickCount++;
                tracking.LastClickedAtUtc = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(target))
                {
                    tracking.LastClickedUrl = target;
                }

                db.ActivityLogs.Add(new ActivityLog
                {
                    LeadId = tracking.LeadId,
                    Type = ActivityType.Email,
                    Message = $"Recipient clicked call-to-action link in email '{tracking.Subject}'.",
                    OccurredUtc = DateTime.UtcNow
                });

                await db.SaveChangesAsync(ct);
                logger.LogInformation("Email click recorded for tracking ID {TrackingId}, target: {Target}", id, target);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to record email click for tracking ID {TrackingId}", id);
        }

        // Open redirect protection: Only allow valid HTTP/HTTPS URLs
        if (!string.IsNullOrWhiteSpace(target) && Uri.TryCreate(target, UriKind.Absolute, out var uriResult) &&
            (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
        {
            return Redirect(target);
        }

        return Redirect("/");
    }

    /// <summary>
    /// Retrieves email delivery, open, and click history for a specific lead.
    /// </summary>
    [HttpGet("api/email/tracking/{leadId:int}")]
    [Authorize]
    public async Task<IActionResult> GetLeadEmailTracking(int leadId, CancellationToken ct)
    {
        var trackings = await db.EmailTrackings
            .Where(x => x.LeadId == leadId)
            .OrderByDescending(x => x.SentAtUtc)
            .Select(x => new
            {
                x.Id,
                x.Subject,
                x.RecipientEmail,
                x.StepName,
                x.SentAtUtc,
                x.IsOpened,
                x.OpenedAtUtc,
                x.ClickCount,
                x.LastClickedAtUtc,
                x.LastClickedUrl
            })
            .ToListAsync(ct);

        return Ok(trackings);
    }

    /// <summary>
    /// Manually triggers processing of all pending automated drip sequence emails.
    /// </summary>
    [HttpPost("api/email/drip/trigger")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> TriggerBatchDrip(CancellationToken ct)
    {
        var count = await dripEmailService.ProcessPendingDripEmailsAsync(ct);
        return Ok(new { dispatched = count });
    }

    /// <summary>
    /// Manually triggers the next automated drip sequence step for a specific lead.
    /// </summary>
    [HttpPost("api/email/drip/trigger/{leadId:int}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> TriggerLeadDrip(int leadId, CancellationToken ct)
    {
        var tracking = await dripEmailService.TriggerNextDripStepAsync(leadId, ct);
        if (tracking is null)
        {
            return BadRequest(new { message = "Lead is not eligible for a next drip email step (already completed, paused, or missing email)." });
        }

        return Ok(new
        {
            trackingId = tracking.Id,
            stepName = tracking.StepName,
            subject = tracking.Subject,
            recipient = tracking.RecipientEmail
        });
    }
}
