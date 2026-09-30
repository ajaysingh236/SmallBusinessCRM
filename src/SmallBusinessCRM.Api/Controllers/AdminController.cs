using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmallBusinessCRM.Api.Data;
using SmallBusinessCRM.Api.Models;
using SmallBusinessCRM.Api.Services;

namespace SmallBusinessCRM.Api.Controllers;

[ApiController, Route("api/admin"), Authorize(Roles = "Admin")]
public sealed class AdminController(CrmDbContext db, UserManager<ApplicationUser> userManager, IAuditService audit) : ControllerBase
{
    [HttpGet("users")]
    public async Task<IActionResult> Users(CancellationToken ct) => Ok(await db.Users.OrderBy(x => x.FullName).Select(x => new { x.Id, x.FullName, x.Email, x.TenantId, x.IsActive }).ToListAsync(ct));

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser(CreateUserRequest request, CancellationToken ct)
    {
        if (!new[] { RolesCatalog.Admin, RolesCatalog.Manager, RolesCatalog.SalesRepresentative }.Contains(request.Role, StringComparer.OrdinalIgnoreCase)) return BadRequest(new { message = "Invalid role." });
        var user = new ApplicationUser { UserName = request.Email, Email = request.Email, EmailConfirmed = true, FullName = request.FullName, TenantId = request.TenantId };
        var result = await userManager.CreateAsync(user, request.Password); if (!result.Succeeded) return BadRequest(result.Errors);
        await userManager.AddToRoleAsync(user, request.Role); await audit.WriteAsync("UserCreated", nameof(ApplicationUser), user.Id, newValues: new { user.Email, request.Role }, ct: ct);
        return Created($"api/admin/users/{user.Id}", new { user.Id, user.FullName, user.Email });
    }

    [HttpGet("audit-logs")]
    public async Task<IActionResult> AuditLogs([FromQuery] int take = 100, CancellationToken ct = default) => Ok(await db.AuditLogs.Include(x => x.ActorUser).OrderByDescending(x => x.OccurredUtc).Take(Math.Clamp(take, 1, 500)).Select(x => new { x.Id, x.Action, x.EntityName, x.EntityId, x.OccurredUtc, actor = x.ActorUser == null ? "System" : x.ActorUser.FullName, x.OldValuesJson, x.NewValuesJson }).ToListAsync(ct));

    [HttpGet("trash")]
    public async Task<IActionResult> Trash(CancellationToken ct) => Ok(await db.Leads.IgnoreQueryFilters().Where(x => x.IsDeleted).OrderByDescending(x => x.DeletedAtUtc).ToListAsync(ct));
}

public sealed record CreateUserRequest(string FullName, string Email, string Password, string Role, string TenantId = "default");
