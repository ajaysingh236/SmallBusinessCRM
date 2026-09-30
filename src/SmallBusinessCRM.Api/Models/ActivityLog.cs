using System.ComponentModel.DataAnnotations;
namespace SmallBusinessCRM.Api.Models;
public class ActivityLog {
    public int Id { get; set; }
    public ActivityType Type { get; set; }
    [Required, StringLength(1000)] public string Message { get; set; } = string.Empty;
    public DateTime OccurredUtc { get; set; } = DateTime.UtcNow;
    public int LeadId { get; set; }
    public Lead Lead { get; set; } = null!;
    public int? UserId { get; set; }
    public ApplicationUser? User { get; set; }
}
