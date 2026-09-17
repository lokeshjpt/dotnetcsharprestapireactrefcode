using Microsoft.Extensions.Logging;
using PWA.PermitsApi.Application.DTOs;
using PWA.PermitsApi.Application.Interfaces;
using PWA.PermitsApi.Application.Interfaces.Integration;
using PWA.PermitsApi.Application.Interfaces.Repositories;

namespace PWA.PermitsApi.Application.Services;

public sealed class ReferenceService : IReferenceService
{
    private readonly IReferenceRepository _repository;
    private readonly IPermitNotificationService _notifications;
    private readonly ILogger<ReferenceService> _logger;

    public ReferenceService(IReferenceRepository repository, IPermitNotificationService notifications, ILogger<ReferenceService> logger)
    {
        _repository = repository;
        _notifications = notifications;
        _logger = logger;
    }

    // Small hardcoded fallbacks used ONLY when the database is unreachable.
    private static readonly IReadOnlyList<ReferenceItemDto> StatesFallback = new List<ReferenceItemDto>
    {
        new("CA", "California"),
        new("NV", "Nevada"),
        new("OR", "Oregon"),
        new("WA", "Washington")
    };

    private static readonly IReadOnlyList<ReferenceItemDto> PaymentTypesFallback = new List<ReferenceItemDto>
    {
        new("CC", "Credit Card"),
        new("CHECK", "Check"),
        new("EXMPT", "Fee Exempt")
    };

    public Task<IReadOnlyList<ReferenceItemDto>> GetStatesAsync(CancellationToken cancellationToken = default)
        => QueryAsync(ct => _repository.GetStatesAsync(ct), StatesFallback, "states", cancellationToken);

    public Task<IReadOnlyList<ReferenceItemDto>> GetCitiesAsync(CancellationToken cancellationToken = default)
        => QueryAsync(ct => _repository.GetCitiesAsync(ct), Array.Empty<ReferenceItemDto>(), "cities", cancellationToken);

    public Task<IReadOnlyList<ReferenceItemDto>> GetPaymentTypesAsync(CancellationToken cancellationToken = default)
        => QueryAsync(ct => _repository.GetPaymentTypesAsync(ct), PaymentTypesFallback, "payment-types", cancellationToken);

    public Task<IReadOnlyList<ReferenceItemDto>> GetWorkCategoriesAsync(CancellationToken cancellationToken = default)
        => QueryAsync(ct => _repository.GetWorkCategoriesAsync(ct), Array.Empty<ReferenceItemDto>(), "work-categories", cancellationToken);

    public Task<IReadOnlyList<WorkTypeDto>> GetWorkTypesAsync(string? category = null, CancellationToken cancellationToken = default)
        => QueryAsync(ct => _repository.GetWorkTypesAsync(category, ct), Array.Empty<WorkTypeDto>(), "work-types", cancellationToken);

    public Task<IReadOnlyList<ReferenceItemDto>> GetWellUseTypesAsync(string? category = null, string? workType = null, CancellationToken cancellationToken = default)
        => QueryAsync(ct => _repository.GetWellUseTypesAsync(category, workType, ct), Array.Empty<ReferenceItemDto>(), "well-use-types", cancellationToken);

    public Task<IReadOnlyList<ReferenceItemDto>> GetDrillMethodsAsync(CancellationToken cancellationToken = default)
        => QueryAsync(ct => _repository.GetDrillMethodsAsync(ct), Array.Empty<ReferenceItemDto>(), "drill-methods", cancellationToken);

    public Task<IReadOnlyList<ReferenceItemDto>> GetInspectorsAsync(CancellationToken cancellationToken = default)
        => QueryAsync(ct => _repository.GetInspectorsAsync(ct), Array.Empty<ReferenceItemDto>(), "inspectors", cancellationToken);

    private async Task<IReadOnlyList<T>> QueryAsync<T>(
        Func<CancellationToken, Task<IReadOnlyList<T>>> query,
        IReadOnlyList<T> fallback,
        string label,
        CancellationToken cancellationToken)
    {
        try
        {
            return await query(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Reference lookup {Label} failed; returning fallback list", label);
            await _notifications.SendSystemExceptionAuditAsync("ReferenceService." + label, null, ex, cancellationToken);
            return fallback;
        }
    }
}
