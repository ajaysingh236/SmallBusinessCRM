using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using SmallBusinessCRM.Api.Services;
namespace SmallBusinessCRM.Api.Controllers;
[ApiController,Route("api/leadscoring"),Authorize(Roles="Admin,Manager")] public class LeadScoringController(ILeadScoringService service):ControllerBase {
 [HttpPost("recalculate/{leadId:int}")] public async Task<IActionResult> Recalculate(int leadId,CancellationToken ct){var score=await service.RecalculateAsync(leadId,ct);return score is null?NotFound():Ok(new{leadId,priorityScore=score});}
 [HttpPost("recalculate-all")] public async Task<IActionResult> RecalculateAll(CancellationToken ct)=>Ok(new{processed=await service.RecalculateAllAsync(ct)});
}
