using System.Net.Mail;
using System.Net.Mime;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PWA.PermitsApi.Application.Configuration;
using PWA.PermitsApi.Application.Interfaces.Integration;
using PWA.PermitsApi.Application.Notifications;

namespace PWA.PermitsApi.Infrastructure.Services;

/// <summary>
/// SMTP email sender driven by <see cref="EmailOptions"/> and the per-project routing in
/// <see cref="EmailRoutingOptions"/>. Falls back to a no-op mock (log only, no mail sent) when
/// <see cref="EmailOptions.UseMock"/> is set or the SMTP server is absent, so tests never send mail.
/// </summary>
public sealed class EmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly EmailRoutingOptions _routing;
    private readonly ILogger<EmailService> _logger;

    // The agency logo, embedded once from the assembly and referenced inline via cid:pwaLogo.
    private static readonly byte[]? LogoBytes = LoadLogo();

    private static byte[]? LoadLogo()
    {
        var asm = typeof(EmailService).Assembly;
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("pwa-logo.png", StringComparison.OrdinalIgnoreCase));
        if (name is null) return null;
        using var stream = asm.GetManifestResourceStream(name);
        if (stream is null) return null;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    public EmailService(
        IOptions<EmailOptions> options,
        IOptions<EmailRoutingOptions> routing,
        ILogger<EmailService> logger)
    {
        _options = options.Value;
        _routing = routing.Value;
        _logger = logger;
    }

    private bool UseMock => _options.UseMock || string.IsNullOrWhiteSpace(_options.SmtpServer);

    public async Task SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (UseMock)
        {
            _logger.LogInformation("Email mock mode. Simulating email to {Recipient}. Subject: {Subject}", toAddress, subject);
            return;
        }

        using var message = new MailMessage(_options.FromAddress, toAddress, subject, body);
        if (_options.IsNonProduction && !string.IsNullOrWhiteSpace(_options.AuditEmail))
        {
            message.Bcc.Add(_options.AuditEmail);
        }

        await SendCoreAsync(_options.SmtpServer, _options.SmtpPort, message, cancellationToken);
        _logger.LogInformation("Sent email via {SmtpServer} to {Recipient}. Subject: {Subject}", _options.SmtpServer, toAddress, subject);
    }

    public async Task SendProjectNotificationAsync(string projectId, string? subjectOverride, string body, CancellationToken cancellationToken = default)
    {
        var project = _routing.FindProject(projectId);
        if (project is null)
        {
            _logger.LogWarning("No email routing configured for project {ProjectId}. Skipping notification.", projectId);
            return;
        }

        var subject = string.IsNullOrWhiteSpace(subjectOverride) ? project.DefaultSubject : subjectOverride;
        var server = string.IsNullOrWhiteSpace(project.EmailServer) ? _options.SmtpServer : project.EmailServer;

        if (_options.UseMock || string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(project.DefaultTo))
        {
            _logger.LogInformation(
                "Email mock mode. Simulating project notification for {ProjectId}: FROM={From} TO={To} CC={Cc} BCC={Bcc} SUBJECT={Subject}",
                project.ProjectId, project.DefaultFrom, project.DefaultTo, project.DefaultCc, project.DefaultBcc, subject);
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(string.IsNullOrWhiteSpace(project.DefaultFrom) ? _options.FromAddress : project.DefaultFrom!),
            Subject = subject,
            Body = body
        };
        message.To.Add(project.DefaultTo!);
        if (!string.IsNullOrWhiteSpace(project.DefaultCc))
        {
            message.CC.Add(project.DefaultCc);
        }

        if (!string.IsNullOrWhiteSpace(project.DefaultBcc))
        {
            message.Bcc.Add(project.DefaultBcc);
        }

        await SendCoreAsync(server, _options.SmtpPort, message, cancellationToken);
        _logger.LogInformation("Sent project notification for {ProjectId} via {Server}. Subject: {Subject}", project.ProjectId, server, subject);
    }

    private static async Task SendCoreAsync(string server, int port, MailMessage message, CancellationToken cancellationToken)
    {
        using var client = new SmtpClient(server, port);
        await client.SendMailAsync(message, cancellationToken);
    }

    public async Task SendNotificationAsync(EmailNotification notification, CancellationToken cancellationToken = default)
    {
        var to = notification.To.Where(a => !string.IsNullOrWhiteSpace(a)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (to.Count == 0)
        {
            _logger.LogWarning("Email notification '{Subject}' has no recipients; skipping.", notification.Subject);
            return;
        }

        var cc = notification.Cc.Where(a => !string.IsNullOrWhiteSpace(a)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var bcc = notification.Bcc.Where(a => !string.IsNullOrWhiteSpace(a)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        if (UseMock)
        {
            _logger.LogInformation(
                "Email mock mode. Simulating notification. TO=[{To}] CC=[{Cc}] BCC=[{Bcc}] ATTACH={Attach} SUBJECT={Subject}",
                string.Join(",", to), string.Join(",", cc), string.Join(",", bcc), notification.Attachments.Count, notification.Subject);
            return;
        }

        using var message = new MailMessage { From = new MailAddress(_options.FromAddress), Subject = notification.Subject, IsBodyHtml = true };
        foreach (var a in to) message.To.Add(a);
        foreach (var a in cc) message.CC.Add(a);
        foreach (var a in bcc) message.Bcc.Add(a);

        var streams = new List<Stream>();
        try
        {
            // Embed the agency logo inline (cid:pwaLogo) when the body references it, so it renders
            // reliably across email clients without an external image host.
            var embedLogo = LogoBytes is not null
                && notification.HtmlBody.Contains("cid:" + EmailTemplate.LogoContentId, StringComparison.OrdinalIgnoreCase);
            if (embedLogo)
            {
                var htmlView = AlternateView.CreateAlternateViewFromString(notification.HtmlBody, null, MediaTypeNames.Text.Html);
                var logoStream = new MemoryStream(LogoBytes!);
                streams.Add(logoStream);
                var logo = new LinkedResource(logoStream, new ContentType("image/png"))
                {
                    ContentId = EmailTemplate.LogoContentId,
                    TransferEncoding = TransferEncoding.Base64
                };
                htmlView.LinkedResources.Add(logo);
                message.AlternateViews.Add(htmlView);
            }
            else
            {
                message.Body = notification.HtmlBody;
            }

            foreach (var attachment in notification.Attachments)
            {
                var stream = new MemoryStream(attachment.Content);
                streams.Add(stream);
                message.Attachments.Add(new Attachment(stream, attachment.FileName, attachment.ContentType));
            }

            await SendCoreAsync(_options.SmtpServer, _options.SmtpPort, message, cancellationToken);
            _logger.LogInformation("Sent email via {SmtpServer} to [{To}]. Subject: {Subject}", _options.SmtpServer, string.Join(",", to), notification.Subject);
        }
        finally
        {
            foreach (var stream in streams) await stream.DisposeAsync();
        }
    }

    public async Task SendAuditAsync(string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.AuditEmail))
        {
            _logger.LogInformation("Audit email '{Subject}' skipped — no AuditEmail configured.", subject);
            return;
        }

        var notification = new EmailNotification { Subject = subject, HtmlBody = htmlBody };
        notification.To.Add(_options.AuditEmail);
        await SendNotificationAsync(notification, cancellationToken);
    }
}
