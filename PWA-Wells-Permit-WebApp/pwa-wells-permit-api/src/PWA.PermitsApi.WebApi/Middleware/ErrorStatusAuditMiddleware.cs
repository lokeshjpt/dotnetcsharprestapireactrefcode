using Microsoft.AspNetCore.WebUtilities;
using PWA.PermitsApi.Application.Interfaces.Integration;
using PWA.PermitsApi.Application.Notifications;
using System.Security.Claims;

namespace PWA.PermitsApi.WebApi.Middleware;

/// <summary>
/// Emails the audit distribution list for error HTTP responses that are produced <b>without throwing</b>
/// — most importantly the <c>401</c>/<c>403</c> that the authentication/authorization middleware returns
/// and the <c>429</c> that the rate limiter returns (these never reach <see cref="GlobalExceptionHandler"/>
/// or any catch block). Each audit email includes the caller's IP address. Thrown exceptions are
/// still audited by <see cref="GlobalExceptionHandler"/>; that handler sets <c>__auditEmailed</c> so a
/// 500 is never emailed twice.
/// </summary>
/// <remarks>
/// Registered early (outer) in the pipeline so <c>await _next</c> flows through auth/authorization, the
/// rate limiter, and the controllers, and the final <see cref="HttpResponse.StatusCode"/> is observed on
/// the way out. Validation errors (<c>400 Bad Request</c>) are intentionally excluded.
/// </remarks>
public sealed class ErrorStatusAuditMiddleware
{
    // Same precedence the allowlist filter uses so the audit email shows the caller's real Entra email
    // (e.g. preferred_username / upn) rather than a bare object id or a null Identity.Name.
    private static readonly string[] EmailClaimTypes =
    {
        "preferred_username",
        "upn",
        "email",
        "unique_name",
        ClaimTypes.Upn,
        ClaimTypes.Email,
        ClaimTypes.Name,
    };

    // The intra SPA calls GET /api/auth/me on every load to discover whether the user is signed in.
    // When there is no session that probe returns 401 by design — it is an expected "am I logged in?"
    // check, not an access violation — so it must never generate an audit email (in any environment).
    private static readonly string[] AuditExemptPaths =
    {
        "/api/auth/me",
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorStatusAuditMiddleware> _logger;

    public ErrorStatusAuditMiddleware(RequestDelegate next, ILogger<ErrorStatusAuditMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        var status = context.Response.StatusCode;

        // Only audit access-denied and rate-limit responses here. Genuine 5xx are audited (with a
        // stack trace) by the global exception handler; validation 400s and normal 2xx/3xx/404-style
        // lookups are excluded.
        if (status != StatusCodes.Status401Unauthorized
            && status != StatusCodes.Status403Forbidden
            && status != StatusCodes.Status429TooManyRequests)
        {
            return;
        }

        if (context.Items.ContainsKey(AuditMarker.AuditEmailedKey))
        {
            return;
        }

        // Suppress audit emails when running under a test host (dotnet test / xunit). Production is
        // never a test host, so genuine 401/403 audit emails are still sent there.
        if (TestHostDetector.IsTestHost)
        {
            return;
        }

        // Skip benign auth session-probe endpoints (e.g. GET /api/auth/me) — a 401 there is the SPA
        // checking login state, not an access violation, so it should not page the audit list.
        if (IsAuditExemptPath(context.Request.Path))
        {
            return;
        }

        try
        {
            var notifications = context.RequestServices.GetRequiredService<IPermitNotificationService>();
            var user = ResolveUserEmail(context.User);
            var clientIp = Http.ClientIpResolver.Resolve(context);
            var correlationId = context.Response.Headers.TryGetValue(CorrelationIdMiddleware.HeaderName, out var cid)
                ? cid.ToString()
                : null;

            _logger.LogWarning("Access denied ({Status}) for {Method} {Path} (user {User}, ip {Ip})", status, context.Request.Method, context.Request.Path, user ?? "anonymous", clientIp ?? "unknown");
            await notifications.SendHttpErrorAuditAsync(
                context.Request.Method,
                context.Request.Path,
                status,
                ReasonPhrases.GetReasonPhrase(status),
                user,
                clientIp,
                correlationId,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Never let audit-emailing interfere with the response.
            _logger.LogWarning(ex, "Failed to send access-denied audit email for {Method} {Path}", context.Request.Method, context.Request.Path);
        }
    }

    private static bool IsAuditExemptPath(PathString path)
    {
        foreach (var exempt in AuditExemptPaths)
        {
            if (path.Equals(exempt, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ResolveUserEmail(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var email = EmailClaimTypes
            .Select(type => principal.FindFirst(type)?.Value)
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        return string.IsNullOrWhiteSpace(email) ? principal.Identity?.Name : email;
    }
}
