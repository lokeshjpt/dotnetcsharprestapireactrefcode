using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PWA.PermitsApi.Application.Interfaces.Integration;
using PWA.PermitsApi.Application.Notifications;
using System.Security.Claims;

namespace PWA.PermitsApi.WebApi.Authorization;

/// <summary>
/// Global authorization filter that enforces the application allowlist on every intra endpoint that
/// requires Microsoft Entra (Azure AD) authentication. Entra proves <b>who</b> the caller is; this
/// filter decides <b>whether</b> that authenticated user is permitted to use the intra app.
/// </summary>
/// <remarks>
/// <para>
/// Runs only for endpoints that carry <c>[Authorize]</c> metadata and are not <c>[AllowAnonymous]</c>,
/// so the public/ecomm anonymous endpoints are never affected. Because the authorization middleware
/// has already enforced authentication by the time MVC filters run, the user here is guaranteed to be
/// authenticated for a protected endpoint.
/// </para>
/// <para>
/// On rejection it logs a warning, sends a best-effort audit email to the audit distribution list, and
/// returns <c>403 Forbidden</c> with a ProblemDetails body carrying <c>code = "not_authorized"</c> so
/// the intra React app can show the "not authorized" banner. It sets <see cref="AuditMarker"/> so the
/// <c>ErrorStatusAuditMiddleware</c> does not also send a duplicate generic 403 audit email.
/// </para>
/// </remarks>
public sealed class WhitelistAuthorizationFilter : IAsyncAuthorizationFilter
{
    /// <summary>Extension key on the 403 ProblemDetails identifying an allowlist rejection.</summary>
    public const string NotAuthorizedCode = "not_authorized";

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

    private readonly IWhitelistProvider _whitelist;
    private readonly IPermitNotificationService _notifications;
    private readonly ILogger<WhitelistAuthorizationFilter> _logger;

    public WhitelistAuthorizationFilter(
        IWhitelistProvider whitelist,
        IPermitNotificationService notifications,
        ILogger<WhitelistAuthorizationFilter> logger)
    {
        _whitelist = whitelist;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var endpoint = context.HttpContext.GetEndpoint();

        // Public/ecomm endpoints: explicitly anonymous, or simply not [Authorize]-protected.
        if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            return;
        }
        if (endpoint?.Metadata.GetMetadata<IAuthorizeData>() is null)
        {
            return;
        }

        var user = context.HttpContext.User;

        // Should already be authenticated (auth middleware ran first); if not, leave the existing
        // 401 handling in place rather than converting it to a 403 here.
        if (user?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var allowed = await _whitelist.GetAllowedEmailsAsync(context.HttpContext.RequestAborted);

        // No active allowlist rows (unseeded table or a transient lookup failure) => gate disabled;
        // never brick the app on a missing/blank allowlist.
        if (allowed.Count == 0)
        {
            return;
        }

        var candidateEmails = EmailClaimTypes
            .Select(type => user.FindFirst(type)?.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!.Trim())
            .ToArray();

        if (candidateEmails.Any(allowed.Contains))
        {
            return;
        }

        var identity = candidateEmails.FirstOrDefault() ?? user.Identity?.Name;
        var method = context.HttpContext.Request.Method;
        var path = context.HttpContext.Request.Path.ToString();
        var clientIp = PWA.PermitsApi.WebApi.Http.ClientIpResolver.Resolve(context.HttpContext);
        var correlationId = context.HttpContext.Request.Headers.TryGetValue(
            PWA.PermitsApi.WebApi.Middleware.CorrelationIdMiddleware.HeaderName, out var cid)
                ? cid.ToString()
                : null;

        _logger.LogWarning(
            "Allowlist denied: user {User} is not authorized for {Method} {Path} (ip {Ip})", identity, method, path, clientIp ?? "unknown");

        // Mark so ErrorStatusAuditMiddleware does not also send a generic 403 audit email.
        context.HttpContext.Items[AuditMarker.AuditEmailedKey] = true;

        await _notifications.SendUnauthorizedAccessAuditAsync(
            identity, clientIp, method, path, correlationId, context.HttpContext.RequestAborted);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Not authorized",
            Detail = "You are not authorized to access this application. Contact your system administrator to request access.",
        };
        problem.Extensions["code"] = NotAuthorizedCode;

        context.Result = new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status403Forbidden,
            ContentTypes = { "application/problem+json" },
        };
    }
}
