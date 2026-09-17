using PWA.PermitsApi.Application.DTOs;
using PWA.PermitsApi.Application.Interfaces;
using PWA.PermitsApi.Application.Interfaces.Repositories;

namespace PWA.PermitsApi.Application.Services;

public sealed class HistoryService : IHistoryService
{
    private readonly IHistoryRepository _repository;

    public HistoryService(IHistoryRepository repository)
    {
        _repository = repository;
    }

    public Task<HistoryPermitSearchResult> SearchPermitsAsync(HistoryPermitSearchRequest request, CancellationToken cancellationToken = default)
        => _repository.SearchPermitsAsync(request, cancellationToken);

    public Task<HistoryWellSearchResult> SearchWellsAsync(HistoryWellSearchRequest request, CancellationToken cancellationToken = default)
        => _repository.SearchWellsAsync(request, cancellationToken);

    public Task<HistoryPermitDetailDto?> GetPermitByIdAsync(string permitNum, CancellationToken cancellationToken = default)
        => _repository.GetPermitByIdAsync(permitNum, cancellationToken);

    public Task<int> UpdatePermitAsync(string permitNum, HistoryPermitUpdateRequest request, string updatedBy, CancellationToken cancellationToken = default)
        => _repository.UpdatePermitAsync(permitNum, request, updatedBy, cancellationToken);

    public Task<int> InsertPermitAsync(HistoryPermitCreateRequest request, string createdBy, CancellationToken cancellationToken = default)
        => _repository.InsertPermitAsync(request, createdBy, cancellationToken);

    public Task<int> DeletePermitAsync(string permitNum, CancellationToken cancellationToken = default)
        => _repository.DeletePermitAsync(permitNum, cancellationToken);

    public Task<HistoryWellDetailDto?> GetWellByIdAsync(int wellKey, CancellationToken cancellationToken = default)
        => _repository.GetWellByIdAsync(wellKey, cancellationToken);

    public Task<int> UpdateWellAsync(int wellKey, HistoryWellUpdateRequest request, string updatedBy, CancellationToken cancellationToken = default)
        => _repository.UpdateWellAsync(wellKey, request, updatedBy, cancellationToken);

    public Task<int> InsertWellAsync(HistoryWellUpdateRequest request, string createdBy, CancellationToken cancellationToken = default)
        => _repository.InsertWellAsync(request, createdBy, cancellationToken);

    public Task<int> DeleteWellAsync(int wellKey, CancellationToken cancellationToken = default)
        => _repository.DeleteWellAsync(wellKey, cancellationToken);

    public Task<IReadOnlyList<HistoryCityDto>> GetHistoryCitiesAsync(CancellationToken cancellationToken = default)
        => _repository.GetHistoryCitiesAsync(cancellationToken);
}
