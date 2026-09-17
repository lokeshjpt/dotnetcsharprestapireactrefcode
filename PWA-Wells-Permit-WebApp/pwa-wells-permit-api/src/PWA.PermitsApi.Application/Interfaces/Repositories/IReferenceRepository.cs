using PWA.PermitsApi.Application.DTOs;

namespace PWA.PermitsApi.Application.Interfaces.Repositories;

public interface IReferenceRepository
{
    Task<IReadOnlyList<ReferenceItemDto>> GetStatesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReferenceItemDto>> GetCitiesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReferenceItemDto>> GetPaymentTypesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReferenceItemDto>> GetWorkCategoriesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkTypeDto>> GetWorkTypesAsync(string? category, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReferenceItemDto>> GetWellUseTypesAsync(string? category, string? workType, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReferenceItemDto>> GetDrillMethodsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReferenceItemDto>> GetInspectorsAsync(CancellationToken cancellationToken = default);
}
