using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmallBusinessCRM.Api.Data;
using SmallBusinessCRM.Api.Models;
using SmallBusinessCRM.Api.Services;

namespace SmallBusinessCRM.Api.Controllers;

[ApiController, Route("api/data"), Authorize(Roles = "Admin,Manager")]
public sealed class ImportExportController(CrmDbContext db, IAuditService audit) : ControllerBase
{
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB Limit
    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text/csv",
        "text/plain",
        "application/vnd.ms-excel",
        "application/csv",
        "text/x-csv",
        "application/octet-stream"
    };

    [HttpPost("leads/import"), RequestSizeLimit(5_242_880)]
    public async Task<IActionResult> ImportLeads(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Please select a non-empty CSV file to upload." });

        if (file.Length > MaxFileSizeBytes)
            return BadRequest(new { message = $"Uploaded file exceeds the maximum allowed limit of {MaxFileSizeBytes / 1024 / 1024} MB." });

        var extension = Path.GetExtension(file.FileName);
        if (!string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only .csv files are supported." });

        if (!string.IsNullOrEmpty(file.ContentType) && !AllowedMimeTypes.Contains(file.ContentType))
            return BadRequest(new { message = $"Unsupported file MIME type: {file.ContentType}." });

        using var reader = new StreamReader(file.OpenReadStream());
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            PrepareHeaderForMatch = args => args.Header.Trim(),
            MissingFieldFound = null,
            HeaderValidated = null
        });

        var rows = csv.GetRecords<LeadImportRow>().ToList();
        var valid = rows.Where(x => !string.IsNullOrWhiteSpace(x.CompanyName) && !string.IsNullOrWhiteSpace(x.ContactName)).ToList();

        foreach (var row in valid)
        {
            var sanitizedTitle = SanitizeCsvImportString(string.IsNullOrWhiteSpace(row.Title) ? row.CompanyName : row.Title);
            var sanitizedCompany = SanitizeCsvImportString(row.CompanyName);
            var sanitizedContact = SanitizeCsvImportString(row.ContactName);
            var sanitizedEmail = SanitizeCsvImportString(row.Email);
            var sanitizedPhone = SanitizeCsvImportString(row.Phone);

            db.Leads.Add(new Lead
            {
                Title = sanitizedTitle,
                CompanyName = sanitizedCompany,
                ContactName = sanitizedContact,
                Email = string.IsNullOrWhiteSpace(sanitizedEmail) ? null : sanitizedEmail,
                Phone = string.IsNullOrWhiteSpace(sanitizedPhone) ? null : sanitizedPhone,
                Value = Math.Max(0, row.Value),
                Status = row.Status,
                CreatedAt = DateTime.UtcNow,
                LastUpdatedUtc = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("LeadCsvImported", nameof(Lead), "bulk", newValues: new { imported = valid.Count, source = file.FileName }, ct: ct);
        return Ok(new { imported = valid.Count, skipped = rows.Count - valid.Count });
    }

    [HttpGet("leads/export")]
    public async Task<IActionResult> ExportLeads([FromQuery] LeadStatus? status, CancellationToken ct)
    {
        var query = db.Leads.AsNoTracking();
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);

        var rawRecords = await query
            .OrderBy(x => x.CompanyName)
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.CompanyName,
                x.ContactName,
                x.Email,
                x.Phone,
                x.Value,
                Status = x.Status.ToString(),
                x.PriorityScore,
                CreatedAt = x.CreatedAt.ToString("u")
            })
            .ToListAsync(ct);

        // Sanitize for CSV export to protect against CSV Formula Injection / DDE
        var safeRecords = rawRecords.Select(x => new
        {
            x.Id,
            Title = SanitizeForCsvExport(x.Title),
            CompanyName = SanitizeForCsvExport(x.CompanyName),
            ContactName = SanitizeForCsvExport(x.ContactName),
            Email = SanitizeForCsvExport(x.Email),
            Phone = SanitizeForCsvExport(x.Phone),
            x.Value,
            x.Status,
            x.PriorityScore,
            x.CreatedAt
        });

        return CsvFile(safeRecords, "leads.csv");
    }

    [HttpGet("activities/export")]
    public async Task<IActionResult> ExportActivities(CancellationToken ct)
    {
        var rawRecords = await db.ActivityLogs
            .Include(x => x.Lead)
            .Include(x => x.User)
            .OrderByDescending(x => x.OccurredUtc)
            .Select(x => new
            {
                x.Id,
                Lead = x.Lead != null ? x.Lead.CompanyName : "General",
                Type = x.Type.ToString(),
                x.Message,
                OccurredUtc = x.OccurredUtc.ToString("u"),
                User = x.User == null ? "System" : x.User.FullName
            })
            .ToListAsync(ct);

        var safeRecords = rawRecords.Select(x => new
        {
            x.Id,
            Lead = SanitizeForCsvExport(x.Lead),
            x.Type,
            Message = SanitizeForCsvExport(x.Message),
            x.OccurredUtc,
            User = SanitizeForCsvExport(x.User)
        });

        return CsvFile(safeRecords, "activity-logs.csv");
    }

    private static FileContentResult CsvFile<T>(IEnumerable<T> records, string name)
    {
        using var writer = new StringWriter();
        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            csv.WriteRecords(records);
        }
        return new FileContentResult(System.Text.Encoding.UTF8.GetBytes(writer.ToString()), "text/csv")
        {
            FileDownloadName = name
        };
    }

    public static string SanitizeForCsvExport(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var trimmed = input.TrimStart(' ');
        if (trimmed.Length > 0 && (trimmed[0] == '=' || trimmed[0] == '+' || trimmed[0] == '-' || trimmed[0] == '@' || trimmed[0] == '\t' || trimmed[0] == '\r'))
        {
            return "'" + input;
        }
        return input;
    }

    public static string SanitizeCsvImportString(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var trimmed = input.TrimStart(' ');
        while (trimmed.Length > 0 && (trimmed[0] == '=' || trimmed[0] == '+' || trimmed[0] == '-' || trimmed[0] == '@' || trimmed[0] == '\t' || trimmed[0] == '\r'))
        {
            trimmed = trimmed.Substring(1).TrimStart(' ');
        }
        return trimmed.Trim();
    }
}

public sealed class LeadImportRow
{
    public string Title { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public decimal Value { get; set; }
    public LeadStatus Status { get; set; } = LeadStatus.New;
}
