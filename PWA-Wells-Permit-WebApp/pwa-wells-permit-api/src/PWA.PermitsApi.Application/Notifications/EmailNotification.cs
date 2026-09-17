using System.Collections.Generic;

namespace PWA.PermitsApi.Application.Notifications;

/// <summary>
/// A fully-addressed outgoing email (recipients + rendered HTML + optional attachments) ready for
/// the transport (<see cref="Interfaces.Integration.IEmailService"/>). Recipient lists are
/// de-duplicated and blank/invalid entries dropped by the notification service before sending.
/// </summary>
public sealed class EmailNotification
{
    public IList<string> To { get; } = new List<string>();
    public IList<string> Cc { get; } = new List<string>();
    public IList<string> Bcc { get; } = new List<string>();
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public IList<EmailAttachment> Attachments { get; } = new List<EmailAttachment>();
}
