using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using PWA.PermitsApi.Application.Interfaces.Repositories;
using PWA.PermitsApi.WebApi.Authorization;

namespace PWA.PermitsApi.Tests;

public sealed class WhitelistProviderTests
{
    private sealed class FakeAppUserRepository : IAppUserRepository
    {
        private readonly Func<IReadOnlyCollection<string>> _factory;
        public int Calls { get; private set; }

        public FakeAppUserRepository(Func<IReadOnlyCollection<string>> factory) => _factory = factory;

        public Task<IReadOnlyCollection<string>> GetActiveEmailsAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(_factory());
        }
    }

    private static WhitelistProvider Build(IAppUserRepository repository) =>
        new(repository, new MemoryCache(new MemoryCacheOptions()), NullLogger<WhitelistProvider>.Instance);

    [Fact]
    public async Task ReturnsActiveEmailsFromRepository()
    {
        var repo = new FakeAppUserRepository(() => new[] { "a@x.gov", "b@x.gov" });
        var provider = Build(repo);

        var result = await provider.GetAllowedEmailsAsync();

        Assert.Contains("a@x.gov", result);
        Assert.Contains("b@x.gov", result);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task CachesLookupSoRepositoryIsQueriedOnce()
    {
        var repo = new FakeAppUserRepository(() => new[] { "a@x.gov" });
        var provider = Build(repo);

        await provider.GetAllowedEmailsAsync();
        await provider.GetAllowedEmailsAsync();

        Assert.Equal(1, repo.Calls);
    }

    [Fact]
    public async Task FailsOpenReturningEmptyWhenRepositoryThrows()
    {
        var repo = new FakeAppUserRepository(() => throw new InvalidOperationException("db down"));
        var provider = Build(repo);

        var result = await provider.GetAllowedEmailsAsync();

        Assert.Empty(result);
    }
}
