using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;

namespace SmallBusinessCRM.Api.Services;

public interface ICurrentUserService
{
    int? UserId { get; }
    IReadOnlySet<string> Roles { get; }
    bool IsAuthenticated { get; }
    bool IsAdmin { get; }
    bool IsManager { get; }
    bool IsManagerOrAdmin { get; }
}

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;
    public int? UserId => int.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    public IReadOnlySet<string> Roles => Principal?.FindAll(ClaimTypes.Role).Select(x => x.Value).ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
    public bool IsAdmin => Roles.Contains(RolesCatalog.Admin);
    public bool IsManager => Roles.Contains(RolesCatalog.Manager);
    public bool IsManagerOrAdmin => IsAdmin || IsManager;
}

public static class RolesCatalog
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string SalesRepresentative = "SalesRepresentative";
}
