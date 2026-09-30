using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SmallBusinessCRM.Api.Models;
using SmallBusinessCRM.Api.Services;
using Xunit;

namespace SmallBusinessCRM.Tests;

public class JwtTokenServiceTests
{
    private const string TestKey = "ThisIsASecretKeyWithMoreThan256BitsLengthForTesting123!";

    [Fact]
    public async Task GenerateTokenAsync_GeneratesValidSignedJwtWithExpectedClaims()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Jwt:SigningKey", TestKey },
            { "Jwt:Issuer", "TestIssuer" },
            { "Jwt:Audience", "TestAudience" },
            { "Jwt:ExpirationMinutes", "60" }
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        var service = new JwtTokenService(configuration);

        var user = new ApplicationUser
        {
            Id = 55,
            UserName = "sarah@crm.local",
            Email = "sarah@crm.local",
            FullName = "Sarah Jenkins",
            TenantId = "tenant-a"
        };
        var roles = new List<string> { RolesCatalog.Manager, RolesCatalog.SalesRepresentative };

        var (tokenString, expiresAtUtc) = await service.GenerateTokenAsync(user, roles);

        Assert.NotNull(tokenString);
        Assert.True(expiresAtUtc > DateTime.UtcNow);

        // Validate token using TokenValidationParameters
        var handler = new JwtSecurityTokenHandler();
        var validationParams = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestKey)),
            ValidateIssuer = true,
            ValidIssuer = "TestIssuer",
            ValidateAudience = true,
            ValidAudience = "TestAudience",
            ValidateLifetime = true
        };

        var principal = handler.ValidateToken(tokenString, validationParams, out var securityToken);

        Assert.NotNull(principal);
        Assert.Equal("55", principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("sarah@crm.local", principal.FindFirstValue(ClaimTypes.Email));
        Assert.Equal("Sarah Jenkins", principal.FindFirstValue("fullName"));
        Assert.Equal("tenant-a", principal.FindFirstValue("tenantId"));

        var roleClaims = principal.FindAll(ClaimTypes.Role).Select(r => r.Value).ToList();
        Assert.Contains(RolesCatalog.Manager, roleClaims);
        Assert.Contains(RolesCatalog.SalesRepresentative, roleClaims);
    }
}
