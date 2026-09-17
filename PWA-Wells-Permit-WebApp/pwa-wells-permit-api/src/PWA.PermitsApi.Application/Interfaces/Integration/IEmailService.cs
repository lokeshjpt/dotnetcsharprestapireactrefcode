using PWA.PermitsApi.Application.Notifications;

namespace PWA.PermitsApi.Application.Interfaces.Integration;

public interface IEmailService
{
    Task SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a notification using the per-project routing defined in the legacy
    /// emailoutput configuration (FROM/TO/CC/BCC/SUBJECT resolved from the project).
    /// </summary>
    Task SendProjectNotificationAsync(string projectId, string? subjectOverride, string body, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a fully-addressed HTML notification (multiple To/Cc/Bcc recipients + optional
    /// attachments). Used by the permit notification service for the applicant-facing and
    /// operational emails ported from the legacy servlets.
    /// </summary>
    Task SendNotificationAsync(EmailNotification notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an internal audit/exception email to the configured audit mailbox
    /// (<see cref="Configuration.EmailOptions.AuditEmail"/>). No-ops when no audit address is set.
    /// </summary>
    Task SendAuditAsync(string subject, string htmlBody, CancellationToken cancellationToken = default);
}
