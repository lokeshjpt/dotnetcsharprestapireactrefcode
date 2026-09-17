namespace PWA.PermitsApi.Application.Interfaces.Repositories;

/// <summary>
/// Reads the intra (staff) application allowlist from the <c>EEAOWN.app_users</c> table. This is the
/// database-backed replacement for the former <c>Auth:WhitelistedUsers</c> appsettings key: access can
/// be granted or revoked by editing rows instead of redeploying the API.
/// </summary>
public interface IAppUserRepository
{
    /// <summary>
    /// Returns the emails/UPNs of every active (<c>active = 1</c>) allowlisted user, trimmed and
    /// compared case-insensitively. An empty result means no active users are configured.
    /// </summary>
    Task<IReadOnlyCollection<string>> GetActiveEmailsAsync(CancellationToken cancellationToken = default);
}
