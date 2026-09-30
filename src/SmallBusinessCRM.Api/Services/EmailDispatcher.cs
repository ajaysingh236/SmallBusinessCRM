using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using SmallBusinessCRM.Api.Models;

namespace SmallBusinessCRM.Api.Services;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public string UserName { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string FromAddress { get; init; } = string.Empty;
    public string FromName { get; init; } = "Small Business CRM";
    public bool Enabled { get; init; }
}

public interface IEmailDispatcher { Task<bool> SendTaskEmailAsync(Lead lead, CrmTask task, CancellationToken ct = default); }

public sealed class EmailDispatcher(IOptions<SmtpOptions> options, ILogger<EmailDispatcher> logger) : IEmailDispatcher
{
    public async Task<bool> SendTaskEmailAsync(Lead lead, CrmTask task, CancellationToken ct = default)
    {
        var smtp = options.Value;
        if (!smtp.Enabled || string.IsNullOrWhiteSpace(lead.Email) || task.EmailTemplate == EmailTemplateKind.None) return false;
        var subject = task.EmailTemplate switch { EmailTemplateKind.MeetingConfirmation => "Meeting confirmation", EmailTemplateKind.Quotation => "Your quotation", _ => "Follow-up from our sales team" };
        var body = task.EmailTemplate switch
        {
            EmailTemplateKind.MeetingConfirmation => $"Hello {lead.ContactName},<br/><br/>Your meeting is scheduled for <b>{task.DueDateUtc.ToLocalTime():f}</b>.<br/><br/>Regards,<br/>{smtp.FromName}",
            EmailTemplateKind.Quotation => $"Hello {lead.ContactName},<br/><br/>Thank you for your interest in {lead.CompanyName}. Our team will share the requested quotation shortly.<br/><br/>Regards,<br/>{smtp.FromName}",
            _ => $"Hello {lead.ContactName},<br/><br/>We are following up regarding {lead.CompanyName}. Please let us know a convenient time to connect.<br/><br/>Regards,<br/>{smtp.FromName}"
        };
        var message = new MimeMessage(); message.From.Add(new MailboxAddress(smtp.FromName, smtp.FromAddress)); message.To.Add(MailboxAddress.Parse(lead.Email)); message.Subject = subject; message.Body = new BodyBuilder { HtmlBody = body }.ToMessageBody();
        try
        {
            using var client = new SmtpClient(); await client.ConnectAsync(smtp.Host, smtp.Port, SecureSocketOptions.StartTls, ct); await client.AuthenticateAsync(smtp.UserName, smtp.Password, ct); await client.SendAsync(message, ct); await client.DisconnectAsync(true, ct);
            logger.LogInformation("Task email sent for lead {LeadId}, task {TaskId}", lead.Id, task.Id);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Task email dispatch failed for lead {LeadId}, task {TaskId}", lead.Id, task.Id);
            return false;
        }
    }
}
