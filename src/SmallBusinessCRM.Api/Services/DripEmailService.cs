using Microsoft.EntityFrameworkCore;
using SmallBusinessCRM.Api.Data;
using SmallBusinessCRM.Api.Models;

namespace SmallBusinessCRM.Api.Services;

public interface IDripEmailService
{
    Task<int> ProcessPendingDripEmailsAsync(CancellationToken ct = default);
    Task<EmailTracking?> TriggerNextDripStepAsync(int leadId, CancellationToken ct = default);
}

public class DripEmailService(
    CrmDbContext db,
    IEmailDispatcher emailDispatcher,
    IConfiguration configuration,
    ILogger<DripEmailService> logger) : IDripEmailService
{
    private string GetBaseUrl() =>
        configuration["App:BaseUrl"] ?? "http://localhost:5000";

    public async Task<int> ProcessPendingDripEmailsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        // Query leads in New or Contacted status that have not opted out / paused drip
        var leads = await db.Leads
            .IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && !x.IsDripPaused && (x.Status == LeadStatus.New || x.Status == LeadStatus.Contacted))
            .Where(x => x.Email != null && x.Email != "")
            .ToListAsync(ct);

        int sentCount = 0;
        foreach (var lead in leads)
        {
            bool shouldSend = false;

            // Step 0 -> Step 1 (Welcome): Send if never sent
            if (lead.DripCampaignStep == 0)
            {
                shouldSend = true;
            }
            // Step 1 -> Step 2 (Product Walkthrough): Send after 2 days
            else if (lead.DripCampaignStep == 1 && lead.LastDripEmailSentUtc.HasValue && lead.LastDripEmailSentUtc.Value.AddDays(2) <= now)
            {
                shouldSend = true;
            }
            // Step 2 -> Step 3 (Case Study & ROI): Send after 5 days
            else if (lead.DripCampaignStep == 2 && lead.LastDripEmailSentUtc.HasValue && lead.LastDripEmailSentUtc.Value.AddDays(5) <= now)
            {
                shouldSend = true;
            }

            if (shouldSend)
            {
                var result = await TriggerNextDripStepAsync(lead.Id, ct);
                if (result is not null) sentCount++;
            }
        }

        if (sentCount > 0)
        {
            logger.LogInformation("DripEmailService executed: {SentCount} automated drip emails dispatched.", sentCount);
        }

        return sentCount;
    }

    public async Task<EmailTracking?> TriggerNextDripStepAsync(int leadId, CancellationToken ct = default)
    {
        var lead = await db.Leads
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == leadId, ct);

        if (lead is null || string.IsNullOrWhiteSpace(lead.Email)) return null;

        int nextStep = lead.DripCampaignStep + 1;
        if (nextStep > 3) return null; // All 3 sequence steps completed

        var baseUrl = GetBaseUrl();
        var trackingId = Guid.NewGuid();

        var (subject, stepName, bodyHtml) = BuildDripTemplate(lead, nextStep, baseUrl, trackingId);

        var tracking = new EmailTracking
        {
            Id = trackingId,
            LeadId = lead.Id,
            Subject = subject,
            RecipientEmail = lead.Email,
            StepName = stepName,
            SentAtUtc = DateTime.UtcNow,
            IsOpened = false
        };

        db.EmailTrackings.Add(tracking);

        lead.DripCampaignStep = nextStep;
        lead.LastDripEmailSentUtc = DateTime.UtcNow;

        db.ActivityLogs.Add(new ActivityLog
        {
            LeadId = lead.Id,
            Type = ActivityType.Email,
            Message = $"Automated Drip Sequence Step {nextStep} ('{stepName}') queued and sent to {lead.Email}.",
            OccurredUtc = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);

        // Dispatch email via dispatcher
        await emailDispatcher.SendHtmlEmailAsync(lead.Email, lead.ContactName, subject, bodyHtml, ct);

        return tracking;
    }

    private static (string Subject, string StepName, string BodyHtml) BuildDripTemplate(Lead lead, int step, string baseUrl, Guid trackingId)
    {
        string openPixelUrl = $"{baseUrl}/api/email/track/open/{trackingId}";
        string clickDemoUrl = $"{baseUrl}/api/email/track/click/{trackingId}?target={Uri.EscapeDataString(baseUrl + "/schedule-demo")}";
        string clickCaseStudyUrl = $"{baseUrl}/api/email/track/click/{trackingId}?target={Uri.EscapeDataString(baseUrl + "/resources/case-study")}";

        string pixelImg = $"<img src=\"{openPixelUrl}\" width=\"1\" height=\"1\" style=\"display:none;\" alt=\"\" />";

        return step switch
        {
            1 => (
                Subject: $"Welcome to our CRM Platform, {lead.ContactName}!",
                StepName: "1. Welcome & Introduction",
                BodyHtml: $@"
<div style=""font-family: Arial, sans-serif; line-height: 1.6; color: #1e293b; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e2e8f0; border-radius: 8px;"">
    <h2 style=""color: #4f46e5; margin-top: 0;"">Welcome to SmallBusinessCRM</h2>
    <p>Hi <b>{lead.ContactName}</b>,</p>
    <p>Thank you for expressing interest in partnering with us at <b>{lead.CompanyName}</b>. We empower modern teams to accelerate sales cycles, automate lead follow-ups, and eliminate deal drop-offs.</p>
    <p>We are excited to learn more about your goals and how we can support your growth.</p>
    <p style=""margin: 30px 0;"">
        <a href=""{clickDemoUrl}"" style=""background-color: #4f46e5; color: #ffffff; padding: 12px 24px; text-decoration: none; border-radius: 6px; font-weight: bold; display: inline-block;"">Explore Features & Book a Call</a>
    </p>
    <p style=""color: #64748b; font-size: 13px;"">Best regards,<br/>The SmallBusinessCRM Team</p>
    {pixelImg}
</div>"
            ),
            2 => (
                Subject: $"Quick 15-minute walkthrough for {lead.CompanyName}",
                StepName: "2. Product Walkthrough Invite",
                BodyHtml: $@"
<div style=""font-family: Arial, sans-serif; line-height: 1.6; color: #1e293b; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e2e8f0; border-radius: 8px;"">
    <h2 style=""color: #4f46e5; margin-top: 0;"">Let's connect this week</h2>
    <p>Hi <b>{lead.ContactName}</b>,</p>
    <p>Following up on my previous note. Most companies in your industry see an immediate 35% increase in pipeline conversion once they streamline their task workflows and AI lead prioritization.</p>
    <p>Would you have 15 minutes this Thursday or Friday for a personalized walkthrough tailored to {lead.CompanyName}?</p>
    <p style=""margin: 30px 0;"">
        <a href=""{clickDemoUrl}"" style=""background-color: #4f46e5; color: #ffffff; padding: 12px 24px; text-decoration: none; border-radius: 6px; font-weight: bold; display: inline-block;"">Select a Convenient Time Slot</a>
    </p>
    <p style=""color: #64748b; font-size: 13px;"">Best regards,<br/>The SmallBusinessCRM Team</p>
    {pixelImg}
</div>"
            ),
            _ => (
                Subject: $"How similar teams scaled revenue with SmallBusinessCRM",
                StepName: "3. Case Study & ROI",
                BodyHtml: $@"
<div style=""font-family: Arial, sans-serif; line-height: 1.6; color: #1e293b; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e2e8f0; border-radius: 8px;"">
    <h2 style=""color: #4f46e5; margin-top: 0;"">Proven Results & Customer ROI</h2>
    <p>Hi <b>{lead.ContactName}</b>,</p>
    <p>I wanted to share a quick case study showing how teams similar to <b>{lead.CompanyName}</b> achieved 3x deal velocity and automated 80% of repetitive task reminders.</p>
    <p style=""margin: 30px 0;"">
        <a href=""{clickCaseStudyUrl}"" style=""background-color: #059669; color: #ffffff; padding: 12px 24px; text-decoration: none; border-radius: 6px; font-weight: bold; display: inline-block;"">Read Customer Case Study</a>
    </p>
    <p>Whenever you are ready to review your team's setup, just reply to this email.</p>
    <p style=""color: #64748b; font-size: 13px;"">Best regards,<br/>The SmallBusinessCRM Team</p>
    {pixelImg}
</div>"
            )
        };
    }
}
