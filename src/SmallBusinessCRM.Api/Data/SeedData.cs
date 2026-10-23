using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmallBusinessCRM.Api.Models;
using SmallBusinessCRM.Api.Services;

namespace SmallBusinessCRM.Api.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SeedData");
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();

        
var configuration = scope.ServiceProvider
    .GetRequiredService<IConfiguration>();

var dbProvider = configuration["DatabaseProvider"] ?? "SqlServer";

if (string.Equals(
    dbProvider,
    "Sqlite",
    StringComparison.OrdinalIgnoreCase))
{
    // SQLite: create tables from the current EF Core model.
    await db.Database.EnsureCreatedAsync();
}
else
{
    // SQL Server: continue using the existing migrations.
    await db.Database.MigrateAsync();
}


        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var role in new[] { RolesCatalog.Admin, RolesCatalog.Manager, RolesCatalog.SalesRepresentative })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole<int>(role));
        }

        var seeded = new[]
        {
            (Email: "team@paruluniversity.ac.in", Name: "Parul CRM Team", Role: RolesCatalog.Admin, Password: "Atul6001@"),
            (Email: "manager@crm.local", Name: "Sales Manager", Role: RolesCatalog.Manager, Password: "ChangeMe123!"),
            (Email: "atul@crm.local", Name: "Atul Anand", Role: RolesCatalog.SalesRepresentative, Password: "ChangeMe123!")
        };

        foreach (var item in seeded)
        {
           foreach (var item in seeded)
{
    var user = await userManager.FindByEmailAsync(item.Email);

    if (user is null)
    {
        user = new ApplicationUser
        {
            UserName = item.Email,
            Email = item.Email,
            EmailConfirmed = true,
            FullName = item.Name,
            TenantId = "default",
            IsActive = true
        };

        var result = await userManager.CreateAsync(user, item.Password);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join("; ", result.Errors.Select(x => x.Description)));
        }
    }
    else if (!user.IsActive)
    {
        user.IsActive = true;
        var result = await userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join("; ", result.Errors.Select(x => x.Description)));
        }
    }

    if (!await userManager.IsInRoleAsync(user, item.Role))
    {
        var roleResult = await userManager.AddToRoleAsync(user, item.Role);

        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join("; ", roleResult.Errors.Select(x => x.Description)));
        }
    }
}
        }

        foreach (var legacyEmail in new[] { "admin@crm.local", "group@paruluniversity.ac.in" })
        {
            var legacyUser = await userManager.FindByEmailAsync(legacyEmail);
            if (legacyUser is not null && legacyUser.IsActive)
            {
                legacyUser.IsActive = false;
                await userManager.UpdateAsync(legacyUser);
            }
        }

        if (await db.Leads.IgnoreQueryFilters().AnyAsync()) return;

        var rep = await userManager.FindByEmailAsync("atul@crm.local")
            ?? throw new InvalidOperationException("Representative seed failed.");

        db.Leads.AddRange(
            new Lead
            {
                Title = "Apex Traders enterprise deal",
                CompanyName = "Apex Traders",
                ContactName = "Riya Shah",
                Email = "riya@apex.test",
                Value = 65000,
                Status = LeadStatus.DemoScheduled,
                AssignedUserId = rep.Id
            },
            new Lead
            {
                Title = "Nova Retail expansion",
                CompanyName = "Nova Retail",
                ContactName = "Karan Patel",
                Email = "karan@nova.test",
                Value = 12000,
                Status = LeadStatus.Contacted,
                AssignedUserId = rep.Id
            }
        );
        await db.SaveChangesAsync();
    }
}
