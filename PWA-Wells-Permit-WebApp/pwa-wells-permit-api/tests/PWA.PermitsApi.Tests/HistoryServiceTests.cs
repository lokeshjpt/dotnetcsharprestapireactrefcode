using PWA.PermitsApi.Application.DTOs;
using PWA.PermitsApi.Application.Interfaces.Repositories;
using PWA.PermitsApi.Application.Services;

namespace PWA.PermitsApi.Tests;

public sealed class HistoryServiceTests
{
    [Fact]
    public async Task SearchPermitsAsync_PassesRequestThroughAndReturnsRepositoryResult()
    {
        var repository = new FakeHistoryRepository();
        var service = new HistoryService(repository);

        var request = new HistoryPermitSearchRequest { PermitNum = "123", Page = 2, PageSize = 10 };
        var result = await service.SearchPermitsAsync(request);

        Assert.Same(request, repository.LastPermitRequest);
        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("123", result.Items[0].PermitNum);
    }

    [Fact]
    public async Task SearchWellsAsync_PassesRequestThroughAndReturnsRepositoryResult()
    {
        var repository = new FakeHistoryRepository();
        var service = new HistoryService(repository);

        var request = new HistoryWellSearchRequest { TractNum = "T7" };
        var result = await service.SearchWellsAsync(request);

        Assert.Same(request, repository.LastWellRequest);
        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("T7", result.Items[0].TractNum);
    }

    [Fact]
    public async Task GetPermitByIdAsync_ReturnsRepositoryResult()
    {
        var repository = new FakeHistoryRepository();
        var service = new HistoryService(repository);

        var result = await service.GetPermitByIdAsync("0000001234");

        Assert.NotNull(result);
        Assert.Equal("0000001234", result!.PermitNum);
    }

    [Fact]
    public async Task UpdatePermitAsync_PassesPermitNumAndActingUserThrough()
    {
        var repository = new FakeHistoryRepository();
        var service = new HistoryService(repository);

        var affected = await service.UpdatePermitAsync("0000001234", new HistoryPermitUpdateRequest(), "jane@acgov.org");

        Assert.Equal(1, affected);
        Assert.Equal("0000001234", repository.LastUpdatedPermitNum);
        Assert.Equal("jane@acgov.org", repository.LastUpdatedBy);
    }

    [Fact]
    public async Task UpdateWellAsync_PassesWellKeyAndActingUserThrough()
    {
        var repository = new FakeHistoryRepository();
        var service = new HistoryService(repository);

        var affected = await service.UpdateWellAsync(42, new HistoryWellUpdateRequest(), "jane@acgov.org");

        Assert.Equal(1, affected);
        Assert.Equal(42, repository.LastUpdatedWellKey);
        Assert.Equal("jane@acgov.org", repository.LastUpdatedBy);
    }

    [Fact]
    public async Task InsertPermitAsync_PassesRequestAndActingUserThrough()
    {
        var repository = new FakeHistoryRepository();
        var service = new HistoryService(repository);

        var affected = await service.InsertPermitAsync(
            new HistoryPermitCreateRequest { PermitNum = "0000009999" }, "jane@acgov.org");

        Assert.Equal(1, affected);
        Assert.Equal("0000009999", repository.LastCreatedPermitNum);
        Assert.Equal("jane@acgov.org", repository.LastCreatedBy);
    }

    [Fact]
    public async Task InsertWellAsync_ReturnsNewWellKeyAndPassesActingUserThrough()
    {
        var repository = new FakeHistoryRepository();
        var service = new HistoryService(repository);

        var newWellKey = await service.InsertWellAsync(
            new HistoryWellUpdateRequest { TractNum = "T-1" }, "jane@acgov.org");

        Assert.Equal(4242, newWellKey);
        Assert.Equal("T-1", repository.LastCreatedWellTractNum);
        Assert.Equal("jane@acgov.org", repository.LastCreatedBy);
    }

    [Fact]
    public async Task GetHistoryCitiesAsync_ReturnsRepositoryResult()
    {
        var repository = new FakeHistoryRepository();
        var service = new HistoryService(repository);

        var cities = await service.GetHistoryCitiesAsync();

        Assert.Single(cities);
        Assert.Equal("OAK", cities[0].Code);
        Assert.Equal("Oakland", cities[0].Name);
    }

    private sealed class FakeHistoryRepository : IHistoryRepository
    {
        public HistoryPermitSearchRequest? LastPermitRequest { get; private set; }
        public HistoryWellSearchRequest? LastWellRequest { get; private set; }
        public string? LastUpdatedPermitNum { get; private set; }
        public string? LastUpdatedBy { get; private set; }
        public int? LastUpdatedWellKey { get; private set; }
        public string? LastCreatedPermitNum { get; private set; }
        public string? LastCreatedWellTractNum { get; private set; }
        public string? LastCreatedBy { get; private set; }

        public Task<HistoryPermitSearchResult> SearchPermitsAsync(HistoryPermitSearchRequest request, CancellationToken cancellationToken = default)
        {
            LastPermitRequest = request;
            var item = new HistoryPermitDto(request.PermitNum, "New Well", null, null, "1 Main St", "Oakland", null, null, null, null, null, null);
            return Task.FromResult(new HistoryPermitSearchResult(new List<HistoryPermitDto> { item }, 1));
        }

        public Task<HistoryWellSearchResult> SearchWellsAsync(HistoryWellSearchRequest request, CancellationToken cancellationToken = default)
        {
            LastWellRequest = request;
            var item = new HistoryWellDto(1, "P1", request.TractNum, "S1", "1 Main St", "Oakland", "Owner", "Domestic");
            return Task.FromResult(new HistoryWellSearchResult(new List<HistoryWellDto> { item }, 1));
        }

        public Task<HistoryPermitDetailDto?> GetPermitByIdAsync(string permitNum, CancellationToken cancellationToken = default)
            => Task.FromResult<HistoryPermitDetailDto?>(new HistoryPermitDetailDto(
                permitNum, null, "New Well", "Oakland", "1 Main St", null, null, null, null, null, null, null,
                null, null, null, null, null, null, null, null, null, null, null, null, null));

        public Task<int> UpdatePermitAsync(string permitNum, HistoryPermitUpdateRequest request, string updatedBy, CancellationToken cancellationToken = default)
        {
            LastUpdatedPermitNum = permitNum;
            LastUpdatedBy = updatedBy;
            return Task.FromResult(1);
        }

        public Task<int> InsertPermitAsync(HistoryPermitCreateRequest request, string createdBy, CancellationToken cancellationToken = default)
        {
            LastCreatedPermitNum = request.PermitNum;
            LastCreatedBy = createdBy;
            return Task.FromResult(1);
        }

        public Task<int> DeletePermitAsync(string permitNum, CancellationToken cancellationToken = default)
            => Task.FromResult(1);

        public Task<HistoryWellDetailDto?> GetWellByIdAsync(int wellKey, CancellationToken cancellationToken = default)
            => Task.FromResult<HistoryWellDetailDto?>(null);

        public Task<int> UpdateWellAsync(int wellKey, HistoryWellUpdateRequest request, string updatedBy, CancellationToken cancellationToken = default)
        {
            LastUpdatedWellKey = wellKey;
            LastUpdatedBy = updatedBy;
            return Task.FromResult(1);
        }

        public Task<int> InsertWellAsync(HistoryWellUpdateRequest request, string createdBy, CancellationToken cancellationToken = default)
        {
            LastCreatedWellTractNum = request.TractNum;
            LastCreatedBy = createdBy;
            return Task.FromResult(4242);
        }

        public Task<int> DeleteWellAsync(int wellKey, CancellationToken cancellationToken = default)
            => Task.FromResult(1);

        public Task<IReadOnlyList<HistoryCityDto>> GetHistoryCitiesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<HistoryCityDto>>(new List<HistoryCityDto> { new("OAK", "Oakland") });
    }
}
