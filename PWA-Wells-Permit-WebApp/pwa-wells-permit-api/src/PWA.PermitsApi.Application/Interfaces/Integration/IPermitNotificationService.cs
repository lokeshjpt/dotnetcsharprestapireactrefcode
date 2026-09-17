using PWA.PermitsApi.Application.Notifications;
using DomainApplication = PWA.PermitsApi.Domain.Models.Application;

namespace PWA.PermitsApi.Application.Interfaces.Integration;

/// <summary>
/// Central, best-effort dispatcher for every Wells Permits email scenario. Each method composes the
/// correct recipients (applicant / cc parties / bcc audit), applies the environment ("TEST - ")
/// subject prefix, and delegates to <see cref="IEmailService"/>. Every send is best-effort: a mail
/// failure is logged and swallowed so it never rolls back or breaks the originating operation.
/// </summary>
public interface IPermitNotificationService
{
    Task SendApplicationConfirmationAsync(DomainApplication application, decimal authAmount, string paymentType, string? checkNum, CancellationToken cancellationToken = default);

    Task SendSitemapReceivedAsync(DomainApplication application, CancellationToken cancellationToken = default);

    Task SendApprovalAsync(DomainApplication application, IReadOnlyList<string> permitNumbers, InspectorContact? inspector, IReadOnlyList<EmailAttachment>? attachments = null, CancellationToken cancellationToken = default);

    Task SendPaymentDeclineAsync(DomainApplication application, CancellationToken cancellationToken = default);

    Task SendApplicationCancelledAsync(DomainApplication application, string cancelledBy, CancellationToken cancellationToken = default);

    Task SendInspectionScheduledAsync(DomainApplication application, IReadOnlyList<string> permitNumbers, DateTime? inspectionDateTime, InspectorContact? inspector, CancellationToken cancellationToken = default);

    // Audit / operational (to AuditEmail)
    Task SendCcPreAuthAuditAsync(DomainApplication application, decimal authAmount, string? customerId, CancellationToken cancellationToken = default);

    Task SendPaymentErrorAuditAsync(string appId, string paymentType, string? applicantName, string? email, bool dbCommitted, string error, CancellationToken cancellationToken = default);

    Task SendSitemapScanAuditAsync(string appId, string reason, CancellationToken cancellationToken = default);

    Task SendApprovalExceptionAuditAsync(string appId, string process, string user, Exception exception, CancellationToken cancellationToken = default);

    Task SendCcChargeAuditAsync(string appId, string amount, bool success, string? failReason, string? paymentId, string? authCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generic system-exception audit. Sent TO the audit list in every environment for any error the
    /// application catches or that reaches the global exception handler. Best-effort (logged/swallowed).
    /// </summary>
    Task SendSystemExceptionAuditAsync(string source, string? context, Exception exception, CancellationToken cancellationToken = default);

    /// <summary>
    /// Audit notification for an error HTTP status code returned without an exception (e.g. 401/403
    /// from the auth middleware, or 429 from the rate limiter, or another non-validation error
    /// status). Includes the caller's IP address. Sent TO the audit list in every environment.
    /// Best-effort (logged/swallowed).
    /// </summary>
    Task SendHttpErrorAuditAsync(string method, string path, int statusCode, string reason, string? user, string? clientIp, string? correlationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Audit notification raised when an <b>authenticated</b> Entra user who is NOT on the application
    /// allowlist is blocked (403) from an intra endpoint. Includes the caller's IP address. Sent TO
    /// the audit list in every environment so staff can see access-request attempts. Best-effort
    /// (logged/swallowed).
    /// </summary>
    Task SendUnauthorizedAccessAuditAsync(string? user, string? clientIp, string method, string path, string? correlationId, CancellationToken cancellationToken = default);
}
