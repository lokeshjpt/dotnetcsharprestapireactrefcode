using PWA.PermitsApi.Application.DTOs;

namespace PWA.PermitsApi.Application.Interfaces;

public interface IHistoryService
{
    Task<HistoryPermitSearchResult> SearchPermitsAsync(HistoryPermitSearchRequest request, CancellationToken cancellationToken = default);

    Task<HistoryWellSearchResult> SearchWellsAsync(HistoryWellSearchRequest request, CancellationToken cancellationToken = default);

    Task<HistoryPermitDetailDto?> GetPermitByIdAsync(string permitNum, CancellationToken cancellationToken = default);

    Task<int> UpdatePermitAsync(string permitNum, HistoryPermitUpdateRequest request, string updatedBy, CancellationToken cancellationToken = default);

    Task<int> InsertPermitAsync(HistoryPermitCreateRequest request, string createdBy, CancellationToken cancellationToken = default);

    Task<int> DeletePermitAsync(string permitNum, CancellationToken cancellationToken = default);

    Task<HistoryWellDetailDto?> GetWellByIdAsync(int wellKey, CancellationToken cancellationToken = default);

    Task<int> UpdateWellAsync(int wellKey, HistoryWellUpdateRequest request, string updatedBy, CancellationToken cancellationToken = default);

    Task<int> InsertWellAsync(HistoryWellUpdateRequest request, string createdBy, CancellationToken cancellationToken = default);

    Task<int> DeleteWellAsync(int wellKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HistoryCityDto>> GetHistoryCitiesAsync(CancellationToken cancellationToken = default);
}
