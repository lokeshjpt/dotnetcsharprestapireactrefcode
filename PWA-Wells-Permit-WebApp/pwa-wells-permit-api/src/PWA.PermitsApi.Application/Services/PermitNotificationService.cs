using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PWA.PermitsApi.Application.Configuration;
using PWA.PermitsApi.Application.Interfaces.Integration;
using PWA.PermitsApi.Application.Notifications;
using DomainApplication = PWA.PermitsApi.Domain.Models.Application;

namespace PWA.PermitsApi.Application.Services;

/// <inheritdoc />
public sealed class PermitNotificationService : IPermitNotificationService
{
    private readonly IEmailService _emailService;
    private readonly EmailOptions _options;
    private readonly IcapOptions _icapOptions;
    private readonly ILogger<PermitNotificationService> _logger;

    public PermitNotificationService(
        IEmailService emailService,
        IOptions<EmailOptions> options,
        IOptions<IcapOptions> icapOptions,
        ILogger<PermitNotificationService> logger)
    {
        _emailService = emailService;
        _options = options.Value;
        _icapOptions = icapOptions.Value;
        _logger = logger;
    }

    public Task SendApplicationConfirmationAsync(DomainApplication application, decimal authAmount, string paymentType, string? checkNum, CancellationToken cancellationToken = default) =>
        SendApplicantAsync("application confirmation", application.AppId, () =>
        {
            var content = EmailMessages.ApplicationConfirmation(application, authAmount, paymentType, checkNum, TrackingLink(application), _options.ContactEmail);
            return BuildNotification(content, application.Applicant.AppEmailAddr);
        }, cancellationToken);

    public Task SendSitemapReceivedAsync(DomainApplication application, CancellationToken cancellationToken = default) =>
        SendApplicantAsync("sitemap received", application.AppId, () =>
        {
            var content = EmailMessages.SitemapReceived(application, TrackingLink(application), _options.ContactEmail);
            var notification = BuildNotification(content, application.Applicant.AppEmailAddr);
            // CC the applicant's "Other Email Addresses" (APP_EMAIL_CC, flagged "to be CC'd in
            // notification emails") on the sitemap-received confirmation — legacy ecomm/intra parity.
            foreach (var extra in application.EmailCcs)
            {
                AddCc(notification, extra.EmailAddrCc);
            }
            return notification;
        }, cancellationToken);

    public Task SendApprovalAsync(DomainApplication application, IReadOnlyList<string> permitNumbers, InspectorContact? inspector, IReadOnlyList<EmailAttachment>? attachments = null, CancellationToken cancellationToken = default) =>
        SendApplicantAsync("approval notification", application.AppId, () =>
        {
            var content = EmailMessages.ApprovalNotification(application, permitNumbers, inspector, TrackingLink(application), _options.WebsiteUrl, _options.ContactEmail);
            var notification = BuildNotification(content, application.Applicant.AppEmailAddr);
            // Approval CCs the contact/owner/client and any extra CC recipients, plus the assigned
            // inspector; the audit mailbox is BCC'd (legacy intra ProcessApprovalServlet parity).
            AddCc(notification, application.Contact.ContactEmail);
            AddCc(notification, application.Owner.Email);
            AddCc(notification, application.Client.Email);
            foreach (var extra in application.EmailCcs)
            {
                AddCc(notification, extra.EmailAddrCc);
            }
            if (inspector is not null)
            {
                AddCc(notification, inspector.Email);
            }
            if (attachments is not null)
            {
                foreach (var a in attachments) notification.Attachments.Add(a);
            }
            return notification;
        }, cancellationToken);

    public Task SendPaymentDeclineAsync(DomainApplication application, CancellationToken cancellationToken = default) =>
        SendApplicantAsync("payment decline", application.AppId, () =>
        {
            var hasEmail = !string.IsNullOrWhiteSpace(application.Applicant.AppEmailAddr);
            var auditNote = hasEmail
                ? null
                : "<strong>Payment Failure</strong><br>Customer NOT automatically alerted with the following email because no email address is available.";
            var content = EmailMessages.PaymentDecline(application, auditNote);
            return BuildNotification(content, application.Applicant.AppEmailAddr);
        }, cancellationToken);

    public Task SendApplicationCancelledAsync(DomainApplication application, string cancelledBy, CancellationToken cancellationToken = default) =>
        SendApplicantAsync("application cancelled", application.AppId, () =>
        {
            var content = EmailMessages.ApplicationCancelled(application, cancelledBy, _options.WebsiteUrl);
            return BuildNotification(content, application.Applicant.AppEmailAddr);
        }, cancellationToken);

    public Task SendInspectionScheduledAsync(DomainApplication application, IReadOnlyList<string> permitNumbers, DateTime? inspectionDateTime, InspectorContact? inspector, CancellationToken cancellationToken = default) =>
        SendApplicantAsync("inspection scheduled", application.AppId, () =>
        {
            var content = EmailMessages.InspectionScheduled(application, permitNumbers, inspectionDateTime, inspector);
            return BuildNotification(content, application.Applicant.AppEmailAddr);
        }, cancellationToken);

    // ------------------------------------------------------------------ audit ------

    public Task SendCcPreAuthAuditAsync(DomainApplication application, decimal authAmount, string? customerId, CancellationToken cancellationToken = default) =>
        SendAuditAsync("CC pre-auth audit", application.AppId, () => EmailMessages.CcPreAuthAudit(application, authAmount, customerId), cancellationToken);

    public Task SendPaymentErrorAuditAsync(string appId, string paymentType, string? applicantName, string? email, bool dbCommitted, string error, CancellationToken cancellationToken = default) =>
        SendAuditAsync("payment error audit", appId, () => EmailMessages.PaymentErrorAudit(appId, paymentType, applicantName, email, dbCommitted, error), cancellationToken);

    public Task SendSitemapScanAuditAsync(string appId, string reason, CancellationToken cancellationToken = default) =>
        SendAuditAsync("sitemap scan audit", appId, () => EmailMessages.SitemapScanAudit(appId, reason, EnvironmentLabel(), _icapOptions.ServerUrl), cancellationToken);

    public Task SendApprovalExceptionAuditAsync(string appId, string process, string user, Exception exception, CancellationToken cancellationToken = default) =>
        SendAuditAsync("approval exception audit", appId, () => EmailMessages.ApprovalExceptionAudit(appId, process, user, exception), cancellationToken);

    public Task SendCcChargeAuditAsync(string appId, string amount, bool success, string? failReason, string? paymentId, string? authCode, CancellationToken cancellationToken = default) =>
        SendAuditAsync("CC charge audit", appId, () => EmailMessages.CcChargeAudit(appId, amount, success, failReason, paymentId, authCode), cancellationToken);

    public Task SendSystemExceptionAuditAsync(string source, string? context, Exception exception, CancellationToken cancellationToken = default) =>
        SendAuditAsync("system exception audit", context ?? source, () => EmailMessages.SystemExceptionAudit(source, context, exception), cancellationToken);

    public Task SendHttpErrorAuditAsync(string method, string path, int statusCode, string reason, string? user, string? clientIp, string? correlationId, CancellationToken cancellationToken = default) =>
        SendAuditAsync("http error audit", $"{method} {path}", () => EmailMessages.HttpErrorAudit(method, path, statusCode, reason, user, clientIp, correlationId), cancellationToken);

    public Task SendUnauthorizedAccessAuditAsync(string? user, string? clientIp, string method, string path, string? correlationId, CancellationToken cancellationToken = default) =>
        SendAuditAsync("unauthorized access audit", $"{method} {path}", () => EmailMessages.UnauthorizedAccessAudit(user, clientIp, method, path, correlationId), cancellationToken);

    // ------------------------------------------------------------------ helpers ------

    private async Task SendApplicantAsync(string scenario, string appId, Func<EmailNotification> build, CancellationToken cancellationToken)
    {
        try
        {
            var notification = build();
            if (notification.To.Count == 0)
            {
                _logger.LogWarning("{Scenario} email for {AppId} has no recipient; skipping.", scenario, appId);
                return;
            }
            await _emailService.SendNotificationAsync(notification, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Scenario} email failed for application {AppId}; continuing.", scenario, appId);
            // A routine applicant/notification email failed — audit-notify (best-effort; the audit
            // send below has its own guard, so a mail outage cannot loop).
            await SendSystemExceptionAuditAsync("PermitNotificationService." + scenario, "Application " + appId, ex, cancellationToken);
        }
    }

    private async Task SendAuditAsync(string scenario, string appId, Func<EmailContent> build, CancellationToken cancellationToken)
    {
        try
        {
            var content = build();
            await _emailService.SendAuditAsync(Subject(content.Subject), ApplyEnvironmentBanner(content.HtmlBody), cancellationToken);
        }
        catch (Exception ex)
        {
            // Intentionally log-only: this IS the audit-send path, so re-sending an audit email about
            // an audit-email failure would recurse indefinitely when the mail server is unavailable.
            _logger.LogWarning(ex, "{Scenario} audit email failed for application {AppId}; continuing.", scenario, appId);
        }
    }

    /// <summary>
    /// Builds the addressed notification: TO = applicant email (fallback to audit mailbox when
    /// absent, mirroring the legacy servlets), BCC = audit mailbox. When <paramref name="ccApplicant"/>
    /// is set (approval), the contact/owner/client and extra CC addresses on the application are added
    /// to CC, matching intra ProcessApprovalServlet.
    /// </summary>
    private EmailNotification BuildNotification(EmailContent content, string? applicantEmail)
    {
        var notification = new EmailNotification { Subject = Subject(content.Subject), HtmlBody = ApplyEnvironmentBanner(content.HtmlBody) };

        var hasApplicant = !string.IsNullOrWhiteSpace(applicantEmail);
        if (hasApplicant)
        {
            notification.To.Add(applicantEmail!);
        }
        else if (!string.IsNullOrWhiteSpace(_options.AuditEmail))
        {
            notification.To.Add(_options.AuditEmail);
        }

        // Copy the audit distribution list on every applicant application email — confirmation,
        // sitemap received, approval, inspection scheduled, payment decline, cancellation — but only
        // in non-production, so staff can monitor test activity without CC'ing the audit inboxes on
        // production applicant mail. When the applicant email is missing the audit list is already the
        // TO recipient, so the CC would be redundant.
        if (hasApplicant && _options.IsNonProduction && !string.IsNullOrWhiteSpace(_options.AuditEmail))
        {
            notification.Cc.Add(_options.AuditEmail);
        }

        return notification;
    }

    private static void AddCc(EmailNotification notification, string? email)
    {
        if (!string.IsNullOrWhiteSpace(email))
        {
            notification.Cc.Add(email!);
        }
    }

    private string Subject(string baseSubject) => _options.IsNonProduction ? $"{_options.EnvironmentName.Trim().ToUpperInvariant()} - {baseSubject}" : baseSubject;

    private string ApplyEnvironmentBanner(string html) =>
        EmailTemplate.ApplyEnvironmentBanner(html, _options.IsNonProduction, _options.EnvironmentName);

    private string EnvironmentLabel() => string.IsNullOrWhiteSpace(_options.EnvironmentName) ? "prod" : _options.EnvironmentName;

    private string TrackingLink(DomainApplication application)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_options.PublicAppBaseUrl) ? string.Empty : _options.PublicAppBaseUrl.TrimEnd('/');
        var email = Uri.EscapeDataString(application.Applicant.AppEmailAddr ?? string.Empty);
        var appId = Uri.EscapeDataString(application.AppId);
        return $"{baseUrl}/#/track?email={email}&appid={appId}";
    }
}
