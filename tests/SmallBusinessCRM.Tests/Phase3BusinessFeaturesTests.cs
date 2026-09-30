using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SmallBusinessCRM.Api.Controllers;
using SmallBusinessCRM.Api.Data;
using SmallBusinessCRM.Api.Models;
using SmallBusinessCRM.Api.Services;
using SmallBusinessCRM.Api.Services.LeadScoring;
using Xunit;

namespace SmallBusinessCRM.Tests;

public class Phase3BusinessFeaturesTests
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

    private class TestRealtimeNotifier : IRealtimeNotifier
    {
        public int LeadScoredCalls { get; private set; }
        public Task ActivityAsync(object payload, CancellationToken ct = default) => Task.CompletedTask;
        public Task LeadAssignedAsync(int userId, object payload, CancellationToken ct = default) => Task.CompletedTask;
        public Task LeadStageChangedAsync(object payload, CancellationToken ct = default) => Task.CompletedTask;
        public Task LeadScoredAsync(object payload, CancellationToken ct = default)
        {
            LeadScoredCalls++;
            return Task.CompletedTask;
        }
    }

    private class TestHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new HttpClient();
    }

    private class DummyEmailDispatcher : IEmailDispatcher
    {
        public Task<bool> SendTaskEmailAsync(Lead lead, CrmTask task, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> SendHtmlEmailAsync(string toEmail, string toName, string subject, string htmlBody, CancellationToken ct = default) => Task.FromResult(true);
    }

    private static (SqliteConnection connection, DbContextOptions<CrmDbContext> options) CreateSqliteDb()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseSqlite(connection)
            .Options;

        using var db = new CrmDbContext(options, new AdminCurrentUserService());
        db.Database.EnsureCreated();

        return (connection, options);
    }

    [Fact]
    public async Task MachineLearningScoringStrategy_CalculatesCalibratedProbabilityAndExplanationFactors()
    {
        // Arrange
        var strategy = new MachineLearningLeadScoringStrategy();
        var lead = new Lead
        {
            Id = 1,
            Title = "Enterprise Cloud Expansion",
            CompanyName = "Global Logistics Corp",
            ContactName = "Sarah Connor",
            Email = "sarah@globallogistics.com",
            Phone = "+1-555-0199",
            Value = 65000,
            Status = LeadStatus.ProposalSent,
            Tasks = new List<CrmTask>
            {
                new() { Title = "Review security specs", IsCompleted = true },
                new() { Title = "Send updated pricing proposal", IsCompleted = false, DueDateUtc = DateTime.UtcNow.AddDays(2) }
            },
            ActivityLogs = new List<ActivityLog>
            {
                new() { Type = ActivityType.Meeting, Message = "Proposal walkthrough meeting", OccurredUtc = DateTime.UtcNow.AddDays(-2) },
                new() { Type = ActivityType.Call, Message = "Budget review call", OccurredUtc = DateTime.UtcNow.AddDays(-5) }
            }
        };

        // Act
        var result = await strategy.ScoreLeadAsync(lead);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("LogisticRegressionML", result.StrategyName);
        Assert.True(result.ConversionProbability > 0.60, $"Expected high probability for mature proposal, got {result.ConversionProbability}");
        Assert.True(result.PriorityScore >= 60, $"Expected high priority score, got {result.PriorityScore}");
        Assert.NotNull(result.PredictedCloseDays);
        Assert.NotEmpty(result.Factors);
        Assert.Contains(result.Factors, f => f.Category == "Financial");
        Assert.Contains(result.Factors, f => f.Category == "FunnelStage");
    }

    [Fact]
    public async Task LeadScoringService_PersistsProbabilityAndAiExplanation_ToDatabase()
    {
        var (connection, options) = CreateSqliteDb();
        using (connection)
        {
            using var db = new CrmDbContext(options, new AdminCurrentUserService());
            var lead = new Lead
            {
                Title = "Acme Corp Deal",
                CompanyName = "Acme Corp",
                ContactName = "Wile E. Coyote",
                Email = "coyote@acme.com",
                Value = 40000,
                Status = LeadStatus.DemoScheduled
            };
            db.Leads.Add(lead);
            await db.SaveChangesAsync();

            var notifier = new TestRealtimeNotifier();
            var mlStrategy = new MachineLearningLeadScoringStrategy();
            var heuristicStrategy = new HeuristicLeadScoringStrategy();

            var service = new LeadScoringService(
                db,
                new ILeadScoringStrategy[] { mlStrategy, heuristicStrategy },
                notifier,
                NullLogger<LeadScoringService>.Instance);

            // Act
            var result = await service.ScoreLeadAsync(lead.Id);

            // Assert
            Assert.NotNull(result);
            var updated = await db.Leads.FindAsync(lead.Id);
            Assert.NotNull(updated);
            Assert.NotNull(updated.ConversionProbability);
            Assert.NotNull(updated.AiScoreExplanation);
            Assert.True(updated.PriorityScore > 0);
            Assert.Equal(1, notifier.LeadScoredCalls);
        }
    }

    [Fact]
    public async Task EmailTrackingController_OpenPixel_RecordsOpenAndReturnsTransparentGif()
    {
        var (connection, options) = CreateSqliteDb();
        using (connection)
        {
            using var db = new CrmDbContext(options, new AdminCurrentUserService());
            var lead = new Lead
            {
                Title = "Prospect",
                CompanyName = "Apex Industries",
                ContactName = "Jane Doe",
                Email = "jane@apex.com"
            };
            db.Leads.Add(lead);
            await db.SaveChangesAsync();

            var tracking = new EmailTracking
            {
                Id = Guid.NewGuid(),
                LeadId = lead.Id,
                Subject = "Welcome to our CRM Platform",
                RecipientEmail = "jane@apex.com",
                StepName = "1. Welcome",
                SentAtUtc = DateTime.UtcNow,
                IsOpened = false
            };
            db.EmailTrackings.Add(tracking);
            await db.SaveChangesAsync();

            var dripService = new DripEmailService(
                db,
                new DummyEmailDispatcher(),
                new ConfigurationBuilder().Build(),
                NullLogger<DripEmailService>.Instance);

            var controller = new EmailTrackingController(db, dripService, NullLogger<EmailTrackingController>.Instance)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };
            controller.HttpContext.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
            controller.HttpContext.Request.Headers.UserAgent = "TestMailClient/1.0";

            // Act
            var response = await controller.TrackEmailOpen(tracking.Id, CancellationToken.None);

            // Assert
            var fileResult = Assert.IsType<FileContentResult>(response);
            Assert.Equal("image/gif", fileResult.ContentType);
            Assert.Equal(43, fileResult.FileContents.Length); // 43 byte 1x1 transparent GIF

            var updatedTracking = await db.EmailTrackings.FindAsync(tracking.Id);
            Assert.NotNull(updatedTracking);
            Assert.True(updatedTracking.IsOpened);
            Assert.NotNull(updatedTracking.OpenedAtUtc);
            Assert.Equal("127.0.0.1", updatedTracking.IpAddress);
        }
    }

    [Fact]
    public async Task EmailTrackingController_ClickRedirect_IncrementsCountAndSafelyRedirects()
    {
        var (connection, options) = CreateSqliteDb();
        using (connection)
        {
            using var db = new CrmDbContext(options, new AdminCurrentUserService());
            var lead = new Lead
            {
                Title = "Lead",
                CompanyName = "FinTech Labs",
                ContactName = "John Smith",
                Email = "john@fintech.com"
            };
            db.Leads.Add(lead);
            await db.SaveChangesAsync();

            var tracking = new EmailTracking
            {
                Id = Guid.NewGuid(),
                LeadId = lead.Id,
                Subject = "Demo Walkthrough",
                RecipientEmail = "john@fintech.com",
                StepName = "2. Demo Walkthrough",
                ClickCount = 0
            };
            db.EmailTrackings.Add(tracking);
            await db.SaveChangesAsync();

            var dripService = new DripEmailService(
                db,
                new DummyEmailDispatcher(),
                new ConfigurationBuilder().Build(),
                NullLogger<DripEmailService>.Instance);

            var controller = new EmailTrackingController(db, dripService, NullLogger<EmailTrackingController>.Instance)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };

            var targetUrl = "https://example.com/schedule-meeting";

            // Act
            var response = await controller.TrackEmailClick(tracking.Id, targetUrl, CancellationToken.None);

            // Assert
            var redirectResult = Assert.IsType<RedirectResult>(response);
            Assert.Equal(targetUrl, redirectResult.Url);

            var updatedTracking = await db.EmailTrackings.FindAsync(tracking.Id);
            Assert.NotNull(updatedTracking);
            Assert.Equal(1, updatedTracking.ClickCount);
            Assert.NotNull(updatedTracking.LastClickedAtUtc);
            Assert.Equal(targetUrl, updatedTracking.LastClickedUrl);
        }
    }

    [Fact]
    public async Task CalendarSyncService_ConnectsAndSyncsTasksToCalendarEvents()
    {
        var (connection, options) = CreateSqliteDb();
        using (connection)
        {
            using var db = new CrmDbContext(options, new AdminCurrentUserService());
            var user = new ApplicationUser
            {
                Id = 1,
                UserName = "rep@test.com",
                Email = "rep@test.com",
                FullName = "Test Rep"
            };
            db.Users.Add(user);

            var lead = new Lead
            {
                Id = 10,
                CompanyName = "Summit Cloud",
                ContactName = "Alex Rivera",
                Email = "alex@summit.io",
                Value = 50000
            };
            db.Leads.Add(lead);

            var task = new CrmTask
            {
                Id = 20,
                Title = "Product Architecture Deep Dive",
                DueDateUtc = DateTime.UtcNow.AddDays(1),
                IsCompleted = false,
                AssignedUserId = 1,
                LeadId = lead.Id,
                Lead = lead
            };
            db.Tasks.Add(task);
            await db.SaveChangesAsync();

            var service = new CalendarSyncService(db, NullLogger<CalendarSyncService>.Instance);

            // Act 1: Connect
            var integration = await service.ConnectCalendarAsync(1, "Google", "mock_access", "mock_refresh");
            Assert.NotNull(integration);
            Assert.Equal("Google", integration.Provider);
            Assert.True(integration.IsSyncEnabled);

            // Act 2: Sync
            var events = await service.SyncTasksToCalendarAsync(1);

            // Assert
            Assert.Single(events);
            Assert.Equal("CRM: Product Architecture Deep Dive - Summit Cloud", events[0].Title);
            Assert.Equal(task.DueDateUtc, events[0].StartUtc);
            Assert.Equal(task.DueDateUtc.AddHours(1), events[0].EndUtc);
        }
    }

    [Fact]
    public async Task WebhookNotificationService_SimulatesSlackAndTeamsPayloadsGracefully()
    {
        // Arrange
        var inMemoryConfig = new Dictionary<string, string?>
        {
            { "Webhooks:SlackWebhookUrl", "" }, // Simulated/empty
            { "Webhooks:TeamsWebhookUrl", "" }
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();
        var resilience = new ResiliencePolicyService(NullLogger<ResiliencePolicyService>.Instance);

        var webhookService = new WebhookNotificationService(
            new TestHttpClientFactory(),
            configuration,
            resilience,
            NullLogger<WebhookNotificationService>.Instance);

        var lead = new Lead
        {
            Id = 5,
            Title = "Global Expansion Contract",
            CompanyName = "Acme Enterprises",
            ContactName = "CEO John",
            Value = 120000
        };

        // Act & Assert - Should not throw exception when URLs are unconfigured/simulated
        await webhookService.NotifyDealWonAsync(lead, "Top Sales Rep");
        var testResult = await webhookService.SendTestNotificationAsync("Slack", "Hello test notification");
        Assert.True(testResult);
    }
}
