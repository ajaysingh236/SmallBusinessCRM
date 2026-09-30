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

public interface IEmailDispatcher
{
    Task<bool> SendTaskEmailAsync(Lead lead, CrmTask task, CancellationToken ct = default);
    Task<bool> SendHtmlEmailAsync(string toEmail, string toName, string subject, string htmlBody, CancellationToken ct = default);
}

public sealed class EmailDispatcher(
    IOptions<SmtpOptions> options,
    ILogger<EmailDispatcher> logger) : IEmailDispatcher
{
    public async Task<bool> SendTaskEmailAsync(Lead lead, CrmTask task, CancellationToken ct = default)
    {
        var smtp = options.Value;
        if (!smtp.Enabled || string.IsNullOrWhiteSpace(lead.Email) || task.EmailTemplate == EmailTemplateKind.None) return false;

        var subject = task.EmailTemplate switch
        {
            EmailTemplateKind.MeetingConfirmation => "Meeting confirmation",
            EmailTemplateKind.Quotation => "Your quotation",
            _ => "Follow-up from our sales team"
        };

        var body = task.EmailTemplate switch
        {
            EmailTemplateKind.MeetingConfirmation => $"Hello {lead.ContactName},<br/><br/>Your meeting is scheduled for <b>{task.DueDateUtc.ToLocalTime():f}</b>.<br/><br/>Regards,<br/>{smtp.FromName}",
            EmailTemplateKind.Quotation => $"Hello {lead.ContactName},<br/><br/>Thank you for your interest in {lead.CompanyName}. Our team will share the requested quotation shortly.<br/><br/>Regards,<br/>{smtp.FromName}",
            _ => $"Hello {lead.ContactName},<br/><br/>We are following up regarding {lead.CompanyName}. Please let us know a convenient time to connect.<br/><br/>Regards,<br/>{smtp.FromName}"
        };

        return await SendHtmlEmailAsync(lead.Email, lead.ContactName, subject, body, ct);
    }

    public async Task<bool> SendHtmlEmailAsync(string toEmail, string toName, string subject, string htmlBody, CancellationToken ct = default)
    {
        var smtp = options.Value;
        if (string.IsNullOrWhiteSpace(toEmail)) return false;

        if (!smtp.Enabled)
        {
            logger.LogInformation("SMTP dispatch is simulated (Smtp:Enabled=false). Message to {Email} with subject '{Subject}' logged.", toEmail, subject);
            return true;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(smtp.FromName, smtp.FromAddress));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(smtp.Host, smtp.Port, SecureSocketOptions.StartTls, ct);
            if (!string.IsNullOrWhiteSpace(smtp.UserName) && !string.IsNullOrWhiteSpace(smtp.Password))
            {
                await client.AuthenticateAsync(smtp.UserName, smtp.Password, ct);
            }
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);

            logger.LogInformation("HTML email dispatched successfully to {Email} (Subject: {Subject}).", toEmail, subject);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "HTML email dispatch failed for recipient {Email} (Subject: {Subject}).", toEmail, subject);
            return false;
        }
    }
}
