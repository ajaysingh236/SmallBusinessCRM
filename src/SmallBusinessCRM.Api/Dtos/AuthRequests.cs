using System.ComponentModel.DataAnnotations;

namespace SmallBusinessCRM.Api.Dtos;

public sealed class LoginRequest
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
}

public sealed record UpdateLeadRequest(
    [Required, StringLength(200)] string Title,
    [Range(0, 100_000_000)] decimal Value,
    int? AssignedUserId
);

public sealed class JwtLoginResponse
{
    public string Token { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public IList<string> Roles { get; set; } = new List<string>();
    public DateTime ExpiresAtUtc { get; set; }
}
