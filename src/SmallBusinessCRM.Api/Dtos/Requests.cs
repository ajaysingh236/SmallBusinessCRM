using System.ComponentModel.DataAnnotations;
using SmallBusinessCRM.Api.Models;

namespace SmallBusinessCRM.Api.Dtos;

public record CreateLeadRequest(
    [Required, StringLength(120)] string CompanyName,
    [Required, StringLength(100)] string ContactName,
    [EmailAddress, StringLength(150)] string? Email,
    [Phone, StringLength(30)] string? Phone,
    [Range(0, 100_000_000)] decimal Value,
    int? AssignedUserId,
    [StringLength(200)] string? Title = null
);

public record CreateTaskRequest(
    [Required, StringLength(200)] string Title,
    DateTime DueDateUtc,
    int? AssignedUserId,
    EmailTemplateKind EmailTemplate = EmailTemplateKind.None
);

public record LogActivityRequest(
    [Required] ActivityType Type,
    [Required, StringLength(1000)] string Message
);

public record UpdateLeadStatusRequest(
    [Required] LeadStatus Status
);

public record AssignLeadRequest(
    [Required] int AssignedUserId
);
