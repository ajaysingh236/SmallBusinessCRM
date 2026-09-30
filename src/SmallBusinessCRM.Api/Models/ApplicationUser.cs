using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace SmallBusinessCRM.Api.Models;

/// <summary>Identity account with CRM-specific tenant and display data.</summary>
public class ApplicationUser : IdentityUser<int>
{
    [Required, StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required, StringLength(64)]
    public string TenantId { get; set; } = "default";

    public bool IsActive { get; set; } = true;
    public ICollection<Lead> AssignedLeads { get; set; } = new List<Lead>();
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
