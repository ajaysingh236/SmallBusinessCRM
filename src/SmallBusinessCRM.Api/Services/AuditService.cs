using System.Text.Json;
using SmallBusinessCRM.Api.Data;
using SmallBusinessCRM.Api.Models;

namespace SmallBusinessCRM.Api.Services;

public interface IAuditService
{
    Task WriteAsync(string action, string entityName, object entityId, object? oldValues = null, object? newValues = null, CancellationToken ct = default);
}

public sealed class AuditService(CrmDbContext db, ICurrentUserService currentUser) : IAuditService
{
    public async Task WriteAsync(string action, string entityName, object entityId, object? oldValues = null, object? newValues = null, CancellationToken ct = default)
    {
        db.AuditLogs.Add(new AuditLog { Action = action, EntityName = entityName, EntityId = entityId.ToString()!, ActorUserId = currentUser.UserId, OldValuesJson = oldValues is null ? null : JsonSerializer.Serialize(oldValues), NewValuesJson = newValues is null ? null : JsonSerializer.Serialize(newValues) });
        await db.SaveChangesAsync(ct);
    }
}
