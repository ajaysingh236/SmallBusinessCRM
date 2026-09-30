using Microsoft.AspNetCore.Authorization;
using SmallBusinessCRM.Api.Models;
using SmallBusinessCRM.Api.Services;
using System.Security.Claims;

namespace SmallBusinessCRM.Api.Authorization;

public enum LeadOperation { Read, Edit, Delete }

public sealed class LeadAuthorizationRequirement(LeadOperation operation) : IAuthorizationRequirement
{
    public LeadOperation Operation { get; } = operation;
}

public sealed class LeadAuthorizationHandler : AuthorizationHandler<LeadAuthorizationRequirement, Lead>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, LeadAuthorizationRequirement requirement, Lead lead)
    {
        if (context.User.IsInRole(RolesCatalog.Admin) || context.User.IsInRole(RolesCatalog.Manager))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if ((requirement.Operation == LeadOperation.Read || requirement.Operation == LeadOperation.Edit) &&
            userId is not null &&
            userId == lead.AssignedUserId?.ToString())
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
