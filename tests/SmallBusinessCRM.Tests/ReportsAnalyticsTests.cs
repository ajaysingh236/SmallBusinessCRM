using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmallBusinessCRM.Api.Controllers;
using SmallBusinessCRM.Api.Data;
using SmallBusinessCRM.Api.Models;
using SmallBusinessCRM.Api.Services;
using Xunit;

namespace SmallBusinessCRM.Tests;

public class ReportsAnalyticsTests
{
    private class AdminCurrentUserService : ICurrentUserService
    {
        public int? UserId => 1;
        public IReadOnlySet<string> Roles => new HashSet<string> { RolesCatalog.Admin };
        public bool IsAuthenticated => true;
        public bool IsAdmin => true;
        public bool IsManager => true;
        public bool IsManagerOrAdmin => true;
    }

    private class PassThroughCacheService : ICrmCacheService
    {
        public Task<T?> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? ttl = null, CancellationToken ct = default) => factory()!;
        public Task RemoveAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
        public Task InvalidateReportsCacheAsync(string tenantId = "default", CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task Analytics_ReturnsExpectedAggregates()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseSqlite(connection)
            .Options;

        var adminUser = new AdminCurrentUserService();

        using (var db = new CrmDbContext(options, adminUser))
        {
            await db.Database.EnsureCreatedAsync();

            var rep = new ApplicationUser
            {
                Id = 1,
                UserName = "rep@crm.test",
                Email = "rep@crm.test",
                FullName = "Sales Rep"
            };
            db.Users.Add(rep);

            db.Leads.AddRange(
                new Lead
                {
                    Id = 1,
                    Title = "Deal A",
                    CompanyName = "Alpha Co",
                    ContactName = "Alice",
                    Value = 15000,
                    Status = LeadStatus.New,
                    AssignedUserId = 1,
                    CreatedAt = DateTime.UtcNow
                },
                new Lead
                {
                    Id = 2,
                    Title = "Deal B",
                    CompanyName = "Beta Co",
                    ContactName = "Bob",
                    Value = 50000,
                    Status = LeadStatus.ClosedWon,
                    AssignedUserId = 1,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdatedUtc = DateTime.UtcNow
                }
            );

            db.ActivityLogs.Add(new ActivityLog
            {
                Id = 1,
                LeadId = 1,
                Type = ActivityType.Call,
                Message = "Called Alice",
                OccurredUtc = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        }

        using (var db = new CrmDbContext(options, adminUser))
        {
            var cache = new PassThroughCacheService();
            var controller = new ReportsController(db, cache);
            var result = await controller.Analytics(CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);

            var json = JsonSerializer.Serialize(okResult.Value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("leadConversionFunnel", out var funnel));
            Assert.Equal(5, funnel.GetArrayLength());

            Assert.True(root.TryGetProperty("salesActivityDistribution", out var activities));
            Assert.True(activities.GetArrayLength() > 0);

            Assert.True(root.TryGetProperty("monthlyDealVelocity", out var velocity));
            Assert.Equal(6, velocity.GetArrayLength());
        }
    }
}
