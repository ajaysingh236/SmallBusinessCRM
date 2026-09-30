using System.ComponentModel.DataAnnotations;
namespace SmallBusinessCRM.Api.Models;
public class CrmTask {
    public int Id { get; set; }
    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    public DateTime DueDateUtc { get; set; }
    public bool IsCompleted { get; set; }
    public bool ReminderSent { get; set; }
    public EmailTemplateKind EmailTemplate { get; set; }
    public bool EmailSent { get; set; }
    public int LeadId { get; set; }
    public Lead Lead { get; set; } = null!;
    public int? AssignedUserId { get; set; }
    public ApplicationUser? AssignedUser { get; set; }
}
