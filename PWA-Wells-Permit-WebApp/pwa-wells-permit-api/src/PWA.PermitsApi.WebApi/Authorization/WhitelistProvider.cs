using Microsoft.Extensions.Caching.Memory;
using PWA.PermitsApi.Application.Interfaces.Repositories;

namespace PWA.PermitsApi.WebApi.Authorization;

/// <summary>
/// Caches the active <c>EEAOWN.app_users</c> allowlist for a short window so the whitelist filter can
/// run on every intra request without a database round-trip each time. On a lookup failure it logs and
/// returns an empty set (fail-open): the gate then treats the request as "allowlist unavailable" and
/// lets the already-Entra-authenticated user through rather than bricking the app on a transient DB
/// error. Access is still fully controlled by Entra authentication.
/// </summary>
public sealed class WhitelistProvider : IWhitelistProvider
{
    internal const string CacheKey = "auth:whitelist:active-emails";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly IAppUserRepository _repository;
    private readonly IMemoryCache _cache;
    private readonly ILogger<WhitelistProvider> _logger;

    public WhitelistProvider(
        IAppUserRepository repository,
        IMemoryCache cache,
        ILogger<WhitelistProvider> logger)
    {
        _repository = repository;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<string>> GetAllowedEmailsAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyCollection<string>? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var emails = await _repository.GetActiveEmailsAsync(cancellationToken);
            _cache.Set(CacheKey, emails, CacheTtl);
            return emails;
        }
        catch (Exception ex)
        {
            // Fail-open: never lock every staff member out of the app because the allowlist lookup hit
            // a transient error. The request is still Entra-authenticated.
            _logger.LogWarning(ex, "Allowlist lookup from app_users failed; treating the gate as open for this request.");
            return Array.Empty<string>();
        }
    }
}
