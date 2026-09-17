using PWA.PermitsApi.Application.DTOs;

namespace PWA.PermitsApi.Application.Interfaces;

public interface IReferenceService
{
    Task<IReadOnlyList<ReferenceItemDto>> GetStatesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReferenceItemDto>> GetCitiesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReferenceItemDto>> GetPaymentTypesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReferenceItemDto>> GetWorkCategoriesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkTypeDto>> GetWorkTypesAsync(string? category = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReferenceItemDto>> GetWellUseTypesAsync(string? category = null, string? workType = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReferenceItemDto>> GetDrillMethodsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReferenceItemDto>> GetInspectorsAsync(CancellationToken cancellationToken = default);
}
