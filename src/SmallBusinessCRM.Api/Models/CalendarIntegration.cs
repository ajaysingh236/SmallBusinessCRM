using System.ComponentModel.DataAnnotations;

namespace SmallBusinessCRM.Api.Models;

public class CalendarIntegration
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    [Required, StringLength(50)]
    public string Provider { get; set; } = "Google"; // "Google" or "Outlook"

    [StringLength(1000)]
    public string? AccessToken { get; set; }

    [StringLength(1000)]
    public string? RefreshToken { get; set; }

    public DateTime? ExpiresAtUtc { get; set; }

    [StringLength(150)]
    public string? CalendarId { get; set; }

    public bool IsSyncEnabled { get; set; } = true;

    public DateTime? LastSyncedAtUtc { get; set; }
}
