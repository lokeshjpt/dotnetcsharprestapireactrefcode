namespace PWA.PermitsApi.Application.Notifications;

/// <summary>A file attached to an outgoing email (e.g. the approval permit PDF).</summary>
public sealed record EmailAttachment(string FileName, byte[] Content, string ContentType = "application/octet-stream");
