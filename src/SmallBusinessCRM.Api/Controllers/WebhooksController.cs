using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmallBusinessCRM.Api.Services;

namespace SmallBusinessCRM.Api.Controllers;

[ApiController]
[Route("api/webhooks")]
[Authorize(Roles = "Admin,Manager")]
public class WebhooksController(
    IWebhookNotificationService webhookService,
    IConfiguration configuration) : ControllerBase
{
    public record TestWebhookRequest(string Target, string Message);

    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        var slackConfigured = !string.IsNullOrWhiteSpace(configuration["Webhooks:SlackWebhookUrl"]);
        var teamsConfigured = !string.IsNullOrWhiteSpace(configuration["Webhooks:TeamsWebhookUrl"]);

        return Ok(new
        {
            slackConfigured,
            teamsConfigured
        });
    }

    [HttpPost("test")]
    public async Task<IActionResult> TestWebhook([FromBody] TestWebhookRequest request, CancellationToken ct)
    {
        var success = await webhookService.SendTestNotificationAsync(request.Target, request.Message, ct);
        return Ok(new
        {
            target = request.Target,
            dispatched = success,
            timestampUtc = DateTime.UtcNow
        });
    }
}
