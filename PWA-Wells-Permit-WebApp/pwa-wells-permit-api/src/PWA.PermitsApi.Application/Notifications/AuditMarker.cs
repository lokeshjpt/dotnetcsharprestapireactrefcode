namespace PWA.PermitsApi.Application.Notifications;

/// <summary>
/// Shared key marking that an error has already had its audit email sent, so the global exception
/// handler (for rethrown exceptions) and the status-code middleware (for 401/403) never send a
/// duplicate. It is stored on <see cref="System.Exception.Data"/> — which travels with a rethrown
/// exception across layers — and mirrored onto <c>HttpContext.Items</c> for the current request.
/// </summary>
public static class AuditMarker
{
    public const string AuditEmailedKey = "__auditEmailed";
}
