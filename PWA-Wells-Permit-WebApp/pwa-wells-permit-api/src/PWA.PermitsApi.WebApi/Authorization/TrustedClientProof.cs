using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using PWA.PermitsApi.Application.Configuration;

namespace PWA.PermitsApi.WebApi.Authorization;

/// <summary>
/// Public-access proof for a <b>trusted Entra (Azure AD) caller</b> — the authenticated intra (staff)
/// SPA, or another whitelisted client app, reaching an endpoint it shares with the public ecomm SPA
/// (e.g. Track search). The bearer token is validated explicitly against the <c>AzureAD</c> scheme
/// because <c>UseAuthentication</c>'s default scheme has no registered handler and therefore does not
/// populate <c>HttpContext.User</c> on these anonymous endpoints.
/// </summary>
/// <remarks>
/// When an <see cref="AuthOptions.TrustedClientIds"/> allowlist is configured, the token's
/// <c>appid</c>/<c>azp</c> claim must be on it; when the allowlist is blank, any validated Entra token
/// is trusted (fail-open). The proof short-circuits to <c>false</c> when no <c>Authorization</c> header
/// is present, so anonymous browser requests (and unit tests without authentication services wired up)
/// never invoke <c>AuthenticateAsync</c>.
/// </remarks>
public sealed class TrustedClientProof : IPublicAccessProof
{
    private const string AzureAdScheme = "AzureAD";

    private readonly AuthOptions _auth;

    public TrustedClientProof(IOptions<AuthOptions> auth) => _auth = auth.Value;

    public async Task<bool> IsSatisfiedAsync(HttpContext context)
    {
        if (!context.Request.Headers.ContainsKey("Authorization"))
        {
            return false;
        }

        var authResult = await context.AuthenticateAsync(AzureAdScheme);
        if (!authResult.Succeeded)
        {
            return false;
        }

        // No allowlist configured → trust any validated Entra token (fail-open).
        if (!_auth.HasTrustedClients)
        {
            return true;
        }

        var appId = authResult.Principal?.FindFirst("appid")?.Value
            ?? authResult.Principal?.FindFirst("azp")?.Value;
        return _auth.IsTrustedClient(appId);
    }
}
