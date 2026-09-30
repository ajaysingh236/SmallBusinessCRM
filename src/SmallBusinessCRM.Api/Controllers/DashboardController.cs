using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SmallBusinessCRM.Api.Controllers;

[ApiController]
public sealed class DashboardController(IWebHostEnvironment environment) : ControllerBase
{
    [Authorize(Roles = "Admin"), HttpGet("/admin")]
    public IActionResult Admin() => PhysicalFile(Path.Combine(environment.WebRootPath, "admin.html"), "text/html");

    [Authorize(Roles = "Admin,Manager"), HttpGet("/manager")]
    public IActionResult Manager() => PhysicalFile(Path.Combine(environment.WebRootPath, "pipeline.html"), "text/html");

    [Authorize(Roles = "Admin,Manager,SalesRepresentative"), HttpGet("/sales")]
    public IActionResult Sales() => PhysicalFile(Path.Combine(environment.WebRootPath, "pipeline.html"), "text/html");
}
