using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmallBusinessCRM.Api.Data;
using SmallBusinessCRM.Api.Models;
using SmallBusinessCRM.Api.Services;
using Xunit;

namespace SmallBusinessCRM.Tests;

public class ReminderWorkerQueryFilterTests
{
    private class AnonymousCurrentUserService : ICurrentUserService
    {
        public int? UserId => null;
        public IReadOnlySet<string> Roles => new HashSet<string>();
        public bool IsAuthenticated => false;
        public bool IsAdmin => false;
        public bool IsManager => false;
        public bool IsManagerOrAdmin => false;
    }

    [Fact]
    public async Task OverdueTasks_AreExcludedByStandardQuery_ButIncludedByIgnoreQueryFilters()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseSqlite(connection)
            .Options;

        var anonymousUser = new AnonymousCurrentUserService();

        using (var db = new CrmDbContext(options, anonymousUser))
        {
            await db.Database.EnsureCreatedAsync();

            var user = new ApplicationUser
            {
                Id = 100,
                UserName = "rep@crm.test",
                Email = "rep@crm.test",
                FullName = "Sales Rep"
            };
            db.Users.Add(user);

            var lead = new Lead
            {
                Id = 1,
                Title = "Test Lead",
                CompanyName = "Test Co",
                ContactName = "John Doe",
                AssignedUserId = 100
            };
            db.Leads.Add(lead);

            var task = new CrmTask
            {
                Id = 1,
                LeadId = 1,
                Title = "Follow up",
                DueDateUtc = DateTime.UtcNow.AddMinutes(-30),
                AssignedUserId = 100,
                IsCompleted = false,
                ReminderSent = false
            };
            db.Tasks.Add(task);
            await db.SaveChangesAsync();
        }

        using (var db = new CrmDbContext(options, anonymousUser))
        {
            // Standard query with tenant/ownership query filter returns 0 because currentUser is unauthenticated in background service
            var filteredResult = await db.Tasks
                .Where(x => !x.IsCompleted && !x.ReminderSent && x.DueDateUtc <= DateTime.UtcNow)
                .ToListAsync();
            Assert.Empty(filteredResult);

            // With IgnoreQueryFilters (fixed implementation in ReminderWorker), it successfully finds the overdue task
            var backgroundWorkerResult = await db.Tasks
                .IgnoreQueryFilters()
                .Where(x => !x.IsCompleted && !x.ReminderSent && x.DueDateUtc <= DateTime.UtcNow)
                .ToListAsync();
            Assert.Single(backgroundWorkerResult);
            Assert.Equal("Follow up", backgroundWorkerResult[0].Title);
        }
    }
}
