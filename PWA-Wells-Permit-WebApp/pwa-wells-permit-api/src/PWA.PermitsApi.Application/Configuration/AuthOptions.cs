namespace PWA.PermitsApi.Application.Configuration;

/// <summary>
/// Trust credentials that let a whitelisted <b>Entra (Azure AD) client</b> reach the captcha-guarded
/// public (ecomm) write endpoints without solving a browser captcha — chiefly the authenticated intra
/// (staff) SPA on endpoints it shares with ecomm. Bound from the <c>Auth</c> configuration section and
/// consumed by the public-endpoint guard.
/// </summary>
/// <remarks>
/// <see cref="TrustedClientIds"/> is an allowlist of Entra application/client IDs: a request carrying a
/// valid Entra bearer token whose <c>appid</c>/<c>azp</c> is on the list is trusted. <b>Blank means
/// accept any validated Entra token</b> (fail-open, preserving the pre-allowlist behavior) so no
/// environment is bricked before the list is provisioned. Browsers always satisfy the guard via the
/// captcha instead.
/// </remarks>
public sealed class AuthOptions
{
    private string _trustedClientIds = string.Empty;
    private HashSet<string> _trustedClientIdSet = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Comma/semicolon-separated allowlist of trusted Entra application/client IDs. Blank means any
    /// validated Entra token is trusted (fail-open).
    /// </summary>
    public string TrustedClientIds
    {
        get => _trustedClientIds;
        set
        {
            _trustedClientIds = value ?? string.Empty;
            _trustedClientIdSet = _trustedClientIds
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>True when a non-empty client-ID allowlist has been configured.</summary>
    public bool HasTrustedClients => _trustedClientIdSet.Count > 0;

    /// <summary>
    /// Returns true when <paramref name="appId"/> is on the configured allowlist. When no allowlist is
    /// configured this returns <c>false</c> — callers treat "no allowlist" as fail-open separately via
    /// <see cref="HasTrustedClients"/>.
    /// </summary>
    public bool IsTrustedClient(string? appId)
        => !string.IsNullOrWhiteSpace(appId) && _trustedClientIdSet.Contains(appId);
}
