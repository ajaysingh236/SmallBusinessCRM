using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using SmallBusinessCRM.Api.Models;
using SmallBusinessCRM.Api.Services;
namespace SmallBusinessCRM.Api.Data;
public class CrmDbContext(DbContextOptions<CrmDbContext> options, ICurrentUserService currentUser) : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>(options) {
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<CrmTask> Tasks => Set<CrmTask>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<EmailTracking> EmailTrackings => Set<EmailTracking>();
    public DbSet<CalendarIntegration> CalendarIntegrations => Set<CalendarIntegration>();

    protected override void OnModelCreating(ModelBuilder b) {
        base.OnModelCreating(b);
        b.Entity<Lead>().Property(x => x.Value).HasPrecision(18,2);
        b.Entity<Lead>().Property(x => x.RowVersion).IsRowVersion();
        b.Entity<ApplicationUser>().Property(x => x.RowVersion).IsRowVersion();
        // A representative can only see leads assigned to their own identity account.
        // Captured scoped values are parameterized by EF Core per DbContext instance.
        b.Entity<Lead>().HasQueryFilter(lead => !lead.IsDeleted && (currentUser.IsManagerOrAdmin ||
            (currentUser.IsAuthenticated && lead.AssignedUserId == currentUser.UserId)));
        b.Entity<CrmTask>().HasQueryFilter(task => currentUser.IsManagerOrAdmin ||
            (currentUser.IsAuthenticated && task.Lead.AssignedUserId == currentUser.UserId));
        b.Entity<ActivityLog>().HasQueryFilter(activity => currentUser.IsManagerOrAdmin ||
            (currentUser.IsAuthenticated && activity.Lead.AssignedUserId == currentUser.UserId));
        b.Entity<EmailTracking>().HasQueryFilter(email => currentUser.IsManagerOrAdmin ||
            (currentUser.IsAuthenticated && email.Lead.AssignedUserId == currentUser.UserId));
        b.Entity<CalendarIntegration>().HasQueryFilter(cal => currentUser.IsManagerOrAdmin ||
            (currentUser.IsAuthenticated && cal.UserId == currentUser.UserId));

        b.Entity<Lead>().HasOne(x => x.AssignedUser).WithMany(x => x.AssignedLeads).HasForeignKey(x => x.AssignedUserId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<Lead>().HasOne(x => x.DeletedByUser).WithMany().HasForeignKey(x => x.DeletedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<CrmTask>().HasOne(x => x.Lead).WithMany(x => x.Tasks).HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<CrmTask>().HasOne(x => x.AssignedUser).WithMany().HasForeignKey(x => x.AssignedUserId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<ActivityLog>().HasOne(x => x.Lead).WithMany(x => x.ActivityLogs).HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ActivityLog>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<AuditLog>().HasOne(x => x.ActorUser).WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<EmailTracking>().HasOne(x => x.Lead).WithMany(x => x.EmailTrackings).HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<EmailTracking>().HasOne(x => x.Task).WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<CalendarIntegration>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
