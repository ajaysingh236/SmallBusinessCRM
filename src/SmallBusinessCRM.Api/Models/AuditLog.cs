using System.ComponentModel.DataAnnotations;

namespace SmallBusinessCRM.Api.Models;

public class AuditLog
{
    public long Id { get; set; }
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    [Required, StringLength(100)] public string EntityName { get; set; } = string.Empty;
    [Required, StringLength(100)] public string EntityId { get; set; } = string.Empty;
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
    public DateTime OccurredUtc { get; set; } = DateTime.UtcNow;
    public int? ActorUserId { get; set; }
    public ApplicationUser? ActorUser { get; set; }
}
