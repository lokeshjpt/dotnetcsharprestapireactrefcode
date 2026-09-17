using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PWA.PermitsApi.WebApi.Authorization;

namespace PWA.PermitsApi.WebApi.RateLimiting;

/// <summary>
/// Decides whether the per-client-IP rate limiter applies to a request, scoping it to the
/// <b>open public</b> routes only: anonymous endpoints that are <i>not</i> already behind the
/// <see cref="PublicAccessGuardFilter"/> (captcha token / trusted Entra bearer) gate.
/// </summary>
/// <remarks>
/// Rationale for the exemptions:
/// <list type="bullet">
/// <item>Captcha / trusted-Entra guarded public writes already prove they are a human browser or a
/// trusted client, so they carry their own anti-bot protection.</item>
/// <item>Entra-authorized (staff/intra) routes require a validated bearer token, so an IP flood limiter
/// adds nothing and could throttle legitimate staff behind a shared egress IP.</item>
/// </list>
/// Everything left over — the anonymous, unguarded public lookups/reads — is what the sliding-window
/// IP limiter protects. Because the decision is driven by endpoint metadata, it stays correct as
/// endpoints are added, guarded, or authorized without needing per-route annotations.
/// </remarks>
public static class PublicRateLimit
{
    /// <summary>
    /// Returns <c>true</c> when the limiter should throttle requests to <paramref name="endpoint"/>.
    /// </summary>
    public static bool AppliesTo(Endpoint? endpoint)
    {
        if (endpoint is null)
        {
            return false;
        }

        // Entra-protected staff routes are not "public" (unless the action opts out via [AllowAnonymous]).
        var allowsAnonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;
        var requiresAuthorization = !allowsAnonymous
            && endpoint.Metadata.GetMetadata<IAuthorizeData>() is not null;
        if (requiresAuthorization)
        {
            return false;
        }

        // Captcha / trusted-Entra guarded routes already defend against scripted abuse.
        var captchaGuarded = endpoint.Metadata
            .OfType<ServiceFilterAttribute>()
            .Any(f => f.ServiceType == typeof(PublicAccessGuardFilter));
        if (captchaGuarded)
        {
            return false;
        }

        return true;
    }
}
