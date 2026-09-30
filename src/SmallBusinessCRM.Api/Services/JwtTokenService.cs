using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SmallBusinessCRM.Api.Models;

namespace SmallBusinessCRM.Api.Services;

public interface IJwtTokenService
{
    Task<(string Token, DateTime ExpiresAtUtc)> GenerateTokenAsync(ApplicationUser user, IList<string> roles);
}

public class JwtTokenService(IConfiguration configuration) : IJwtTokenService
{
    public Task<(string Token, DateTime ExpiresAtUtc)> GenerateTokenAsync(ApplicationUser user, IList<string> roles)
    {
        var signingKey = configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");
        var issuer = configuration["Jwt:Issuer"] ?? "SmallBusinessCRM";
        var audience = configuration["Jwt:Audience"] ?? "SmallBusinessCRMClient";
        var expirationMinutes = configuration.GetValue<int?>("Jwt:ExpirationMinutes") ?? 120;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? user.Email ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new("fullName", user.FullName ?? string.Empty),
            new("tenantId", user.TenantId ?? "default")
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var expires = DateTime.UtcNow.AddMinutes(expirationMinutes);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expires,
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = creds
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(tokenDescriptor);
        return Task.FromResult((handler.WriteToken(token), expires));
    }
}
