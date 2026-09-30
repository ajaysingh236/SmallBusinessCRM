using System.Text;
using System.Text.Json;
using SmallBusinessCRM.Api.Models;

namespace SmallBusinessCRM.Api.Services;

public interface IWebhookNotificationService
{
    Task NotifyDealWonAsync(Lead lead, string closedByName, CancellationToken ct = default);
    Task NotifyOverdueTaskAsync(CrmTask task, string repName, string companyName, CancellationToken ct = default);
    Task<bool> SendTestNotificationAsync(string target, string message, CancellationToken ct = default);
}

public class WebhookNotificationService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IResiliencePolicyService resilienceService,
    ILogger<WebhookNotificationService> logger) : IWebhookNotificationService
{
    private string? SlackUrl => configuration["Webhooks:SlackWebhookUrl"];
    private string? TeamsUrl => configuration["Webhooks:TeamsWebhookUrl"];

    public async Task NotifyDealWonAsync(Lead lead, string closedByName, CancellationToken ct = default)
    {
        var text = $"🎉 *Deal Won!* Opportunity *'{lead.Title}'* for *{lead.CompanyName}* closed by *{closedByName}* for *${lead.Value:N0}*!";

        var slackPayload = new
        {
            text,
            blocks = new object[]
            {
                new
                {
                    type = "header",
                    text = new { type = "plain_text", text = "🎉 Deal Won Alert!" }
                },
                new
                {
                    type = "section",
                    fields = new object[]
                    {
                        new { type = "mrkdwn", text = $"*Company:*\n{lead.CompanyName}" },
                        new { type = "mrkdwn", text = $"*Value:*\n${lead.Value:N0}" },
                        new { type = "mrkdwn", text = $"*Contact:*\n{lead.ContactName}" },
                        new { type = "mrkdwn", text = $"*Closed By:*\n{closedByName}" }
                    }
                }
            }
        };

        var teamsPayload = new
        {
            type = "message",
            attachments = new object[]
            {
                new
                {
                    contentType = "application/vnd.microsoft.card.adaptive",
                    content = new
                    {
                        type = "AdaptiveCard",
                        version = "1.4",
                        body = new object[]
                        {
                            new { type = "TextBlock", text = "🎉 Deal Won Alert!", weight = "Bolder", size = "Large", color = "Good" },
                            new { type = "TextBlock", text = text, wrap = true }
                        }
                    }
                }
            }
        };

        await DispatchWebhookAsync("Slack", SlackUrl, slackPayload, ct);
        await DispatchWebhookAsync("Teams", TeamsUrl, teamsPayload, ct);
    }

    public async Task NotifyOverdueTaskAsync(CrmTask task, string repName, string companyName, CancellationToken ct = default)
    {
        var text = $"⚠️ *Overdue Task Alert!* Task *'{task.Title}'* for *{companyName}* assigned to *{repName}* was due on {task.DueDateUtc:g} UTC.";

        var slackPayload = new
        {
            text,
            blocks = new object[]
            {
                new
                {
                    type = "header",
                    text = new { type = "plain_text", text = "⚠️ Overdue Task Alert" }
                },
                new
                {
                    type = "section",
                    fields = new object[]
                    {
                        new { type = "mrkdwn", text = $"*Task:*\n{task.Title}" },
                        new { type = "mrkdwn", text = $"*Company:*\n{companyName}" },
                        new { type = "mrkdwn", text = $"*Assigned To:*\n{repName}" },
                        new { type = "mrkdwn", text = $"*Due Date:*\n{task.DueDateUtc:g} UTC" }
                    }
                }
            }
        };

        var teamsPayload = new
        {
            type = "message",
            attachments = new object[]
            {
                new
                {
                    contentType = "application/vnd.microsoft.card.adaptive",
                    content = new
                    {
                        type = "AdaptiveCard",
                        version = "1.4",
                        body = new object[]
                        {
                            new { type = "TextBlock", text = "⚠️ Overdue CRM Task Alert", weight = "Bolder", size = "Medium", color = "Attention" },
                            new { type = "TextBlock", text = text, wrap = true }
                        }
                    }
                }
            }
        };

        await DispatchWebhookAsync("Slack", SlackUrl, slackPayload, ct);
        await DispatchWebhookAsync("Teams", TeamsUrl, teamsPayload, ct);
    }

    public async Task<bool> SendTestNotificationAsync(string target, string message, CancellationToken ct = default)
    {
        var isSlack = string.Equals(target, "Slack", StringComparison.OrdinalIgnoreCase);
        var url = isSlack ? SlackUrl : TeamsUrl;
        var payload = new { text = $"[SmallBusinessCRM Test Alert] {message}" };

        return await DispatchWebhookAsync(target, url, payload, ct);
    }

    private async Task<bool> DispatchWebhookAsync(string provider, string? url, object payload, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            logger.LogInformation(
                "Webhook alert ({Provider}) simulated: {PayloadSummary}",
                provider, JsonSerializer.Serialize(payload));
            return true;
        }

        try
        {
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await resilienceService.ExecuteAsync(async token =>
            {
                var client = httpClientFactory.CreateClient("WebhooksClient");
                return await client.PostAsync(url, content, token);
            }, ct);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("Successfully delivered {Provider} webhook alert.", provider);
                return true;
            }

            logger.LogWarning("Webhook delivery to {Provider} returned HTTP status {StatusCode}.", provider, response.StatusCode);
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to deliver webhook notification to {Provider}.", provider);
            return false;
        }
    }
}
