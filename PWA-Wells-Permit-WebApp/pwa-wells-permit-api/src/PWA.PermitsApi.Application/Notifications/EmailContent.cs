namespace PWA.PermitsApi.Application.Notifications;

/// <summary>
/// The rendered subject + HTML body of a single email scenario, produced by
/// <see cref="EmailMessages"/>. The subject here is the base subject WITHOUT the environment
/// ("TEST - ") prefix — that prefix is applied centrally by the notification service.
/// </summary>
public sealed record EmailContent(string Subject, string HtmlBody);
