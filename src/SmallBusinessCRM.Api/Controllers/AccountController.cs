using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmallBusinessCRM.Api.Dtos;
using SmallBusinessCRM.Api.Models;
using SmallBusinessCRM.Api.Services;

namespace SmallBusinessCRM.Api.Controllers;

[ApiController, Route("account")]
public sealed class AccountController(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    IJwtTokenService jwtTokenService) : Controller
{
    [AllowAnonymous, HttpPost("login")]
    public async Task<IActionResult> Login([FromForm] LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !user.IsActive) return Unauthorized(new { message = "Invalid credentials." });

        var result = await signInManager.PasswordSignInAsync(user.UserName!, request.Password, request.RememberMe, lockoutOnFailure: true);
        if (!result.Succeeded) return Unauthorized(new { message = "Invalid credentials." });

        // Order is intentional: an admin may also be granted manager capabilities.
        if (await userManager.IsInRoleAsync(user, RolesCatalog.Admin)) return LocalRedirect("/admin");
        if (await userManager.IsInRoleAsync(user, RolesCatalog.Manager)) return LocalRedirect("/manager");
        if (await userManager.IsInRoleAsync(user, RolesCatalog.SalesRepresentative)) return LocalRedirect("/sales");

        await signInManager.SignOutAsync();
        return Forbid();
    }

    [AllowAnonymous, HttpPost("token")]
    public async Task<IActionResult> Token([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !user.IsActive)
            return Unauthorized(new { message = "Invalid credentials." });

        var passwordValid = await userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
            return Unauthorized(new { message = "Invalid credentials." });

        var roles = await userManager.GetRolesAsync(user);
        var (token, expiresAtUtc) = await jwtTokenService.GenerateTokenAsync(user, roles);

        return Ok(new JwtLoginResponse
        {
            Token = token,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName,
            Roles = roles,
            ExpiresAtUtc = expiresAtUtc
        });
    }

    [Authorize, HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return Ok();
    }

    [AllowAnonymous, HttpGet("denied")]
    public IActionResult Denied() => StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not have access to this resource." });
}
