using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmallBusinessCRM.Api.Services;

namespace SmallBusinessCRM.Api.Controllers;

[ApiController]
[Route("api/leadscoring")]
[Authorize(Roles = "Admin,Manager")]
public class LeadScoringController(ILeadScoringService service) : ControllerBase
{
    [HttpPost("recalculate/{leadId:int}")]
    public async Task<IActionResult> Recalculate(int leadId, [FromQuery] string? strategy = null, CancellationToken ct = default)
    {
        var result = await service.ScoreLeadAsync(leadId, strategy, ct);
        if (result is null) return NotFound();

        return Ok(new
        {
            leadId,
            priorityScore = result.PriorityScore,
            conversionProbability = result.ConversionProbability,
            predictedCloseDays = result.PredictedCloseDays,
            factors = result.Factors,
            strategy = result.StrategyName
        });
    }

    [HttpGet("prediction/{leadId:int}")]
    public async Task<IActionResult> GetPrediction(int leadId, [FromQuery] string? strategy = null, CancellationToken ct = default)
    {
        var result = await service.ScoreLeadAsync(leadId, strategy, ct);
        if (result is null) return NotFound();

        return Ok(result);
    }

    [HttpPost("recalculate-all")]
    public async Task<IActionResult> RecalculateAll(CancellationToken ct = default)
    {
        var count = await service.RecalculateAllAsync(ct);
        return Ok(new { processed = count });
    }
}
