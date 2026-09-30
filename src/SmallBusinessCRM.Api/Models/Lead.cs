using System.ComponentModel.DataAnnotations;
namespace SmallBusinessCRM.Api.Models;
public class Lead {
    public int Id { get; set; }
    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(120)] public string CompanyName { get; set; } = string.Empty;
    [Required, StringLength(100)] public string ContactName { get; set; } = string.Empty;
    [EmailAddress] public string? Email { get; set; }
    public string? Phone { get; set; }
    public decimal Value { get; set; }
    public LeadStatus Status { get; set; } = LeadStatus.New;
    [Range(0,100)] public int PriorityScore { get; set; }
    public int? AssignedUserId { get; set; }
    public ApplicationUser? AssignedUser { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public int? DeletedByUserId { get; set; }
    public ApplicationUser? DeletedByUser { get; set; }
    public ICollection<CrmTask> Tasks { get; set; } = new List<CrmTask>();
    public ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();
    [Timestamp] public byte[]? RowVersion { get; set; }
}
