using System.ComponentModel.DataAnnotations;

namespace SmallBusinessCRM.Api.Models;

public class EmailTracking
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public int LeadId { get; set; }
    public Lead Lead { get; set; } = null!;

    public int? TaskId { get; set; }
    public CrmTask? Task { get; set; }

    [Required, StringLength(200)]
    public string Subject { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(150)]
    public string RecipientEmail { get; set; } = string.Empty;

    [StringLength(100)]
    public string StepName { get; set; } = string.Empty;

    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsOpened { get; set; }
    public DateTime? OpenedAtUtc { get; set; }

    [StringLength(255)]
    public string? UserAgent { get; set; }

    [StringLength(50)]
    public string? IpAddress { get; set; }

    public int ClickCount { get; set; }
    public DateTime? LastClickedAtUtc { get; set; }

    [StringLength(500)]
    public string? LastClickedUrl { get; set; }
}
