using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using SmallBusinessCRM.Api.Authorization;
using SmallBusinessCRM.Api.Models;
using SmallBusinessCRM.Api.Services;
using Xunit;

namespace SmallBusinessCRM.Tests;

public class LeadAuthorizationHandlerTests
{
    private readonly LeadAuthorizationHandler _handler = new();

    private static ClaimsPrincipal CreatePrincipal(int userId, string role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, $"user{userId}@crm.test"),
            new(ClaimTypes.Role, role)
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    [Theory]
    [InlineData(RolesCatalog.Admin, LeadOperation.Read, true)]
    [InlineData(RolesCatalog.Admin, LeadOperation.Edit, true)]
    [InlineData(RolesCatalog.Admin, LeadOperation.Delete, true)]
    [InlineData(RolesCatalog.Manager, LeadOperation.Read, true)]
    [InlineData(RolesCatalog.Manager, LeadOperation.Edit, true)]
    [InlineData(RolesCatalog.Manager, LeadOperation.Delete, true)]
    public async Task AdminAndManager_HaveFullAccess(string role, LeadOperation operation, bool expectedSuccess)
    {
        var user = CreatePrincipal(1, role);
        var lead = new Lead { Id = 10, AssignedUserId = 99 };
        var requirement = new LeadAuthorizationRequirement(operation);
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, lead);

        await _handler.HandleAsync(context);

        Assert.Equal(expectedSuccess, context.HasSucceeded);
    }

    [Fact]
    public async Task SalesRepresentative_CanReadAndEdit_AssignedLead()
    {
        var repUser = CreatePrincipal(42, RolesCatalog.SalesRepresentative);
        var lead = new Lead { Id = 10, AssignedUserId = 42 };

        var readContext = new AuthorizationHandlerContext(new[] { new LeadAuthorizationRequirement(LeadOperation.Read) }, repUser, lead);
        await _handler.HandleAsync(readContext);
        Assert.True(readContext.HasSucceeded);

        var editContext = new AuthorizationHandlerContext(new[] { new LeadAuthorizationRequirement(LeadOperation.Edit) }, repUser, lead);
        await _handler.HandleAsync(editContext);
        Assert.True(editContext.HasSucceeded);
    }

    [Fact]
    public async Task SalesRepresentative_CannotReadOrEdit_UnassignedLead()
    {
        var repUser = CreatePrincipal(42, RolesCatalog.SalesRepresentative);
        var lead = new Lead { Id = 10, AssignedUserId = 99 }; // assigned to someone else

        var readContext = new AuthorizationHandlerContext(new[] { new LeadAuthorizationRequirement(LeadOperation.Read) }, repUser, lead);
        await _handler.HandleAsync(readContext);
        Assert.False(readContext.HasSucceeded);

        var editContext = new AuthorizationHandlerContext(new[] { new LeadAuthorizationRequirement(LeadOperation.Edit) }, repUser, lead);
        await _handler.HandleAsync(editContext);
        Assert.False(editContext.HasSucceeded);
    }

    [Fact]
    public async Task SalesRepresentative_CannotDelete_AnyLead()
    {
        var repUser = CreatePrincipal(42, RolesCatalog.SalesRepresentative);
        var lead = new Lead { Id = 10, AssignedUserId = 42 };

        var deleteContext = new AuthorizationHandlerContext(new[] { new LeadAuthorizationRequirement(LeadOperation.Delete) }, repUser, lead);
        await _handler.HandleAsync(deleteContext);
        Assert.False(deleteContext.HasSucceeded);
    }
}
