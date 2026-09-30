using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SmallBusinessCRM.Api.Services;

namespace SmallBusinessCRM.Api.Data;

/// <summary>Enables `dotnet ef` without an HTTP request or running web host.</summary>
public sealed class CrmDbContextFactory : IDesignTimeDbContextFactory<CrmDbContext>
{
    public CrmDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json", optional: false).Build();
        var options = new DbContextOptionsBuilder<CrmDbContext>().UseSqlServer(configuration.GetConnectionString("CrmDatabase")).Options;
        return new CrmDbContext(options, new DesignTimeCurrentUserService());
    }

    private sealed class DesignTimeCurrentUserService : ICurrentUserService
    {
        public int? UserId => null; public IReadOnlySet<string> Roles { get; } = new HashSet<string>();
        public bool IsAuthenticated => false; public bool IsAdmin => false; public bool IsManager => false; public bool IsManagerOrAdmin => true;
    }
}
