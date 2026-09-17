namespace PWA.PermitsApi.WebApi.Authorization;

/// <summary>
/// Supplies the active intra (staff) allowlist to the <see cref="WhitelistAuthorizationFilter"/>.
/// Wraps the database-backed <c>EEAOWN.app_users</c> lookup with short-lived caching so the table is
/// not queried on every request.
/// </summary>
public interface IWhitelistProvider
{
    /// <summary>
    /// Returns the set of allowed emails/UPNs (case-insensitive). An empty set means the allowlist is
    /// unseeded (or temporarily unavailable), in which case the gate stays open for authenticated users.
    /// </summary>
    Task<IReadOnlyCollection<string>> GetAllowedEmailsAsync(CancellationToken cancellationToken = default);
}
