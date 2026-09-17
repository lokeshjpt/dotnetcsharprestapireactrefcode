using Microsoft.Extensions.Logging.Abstractions;
using PWA.PermitsApi.Application.DTOs;
using PWA.PermitsApi.Application.Interfaces.Repositories;
using PWA.PermitsApi.Application.Services;

namespace PWA.PermitsApi.Tests;

public sealed class ReferenceServiceTests
{
    [Fact]
    public async Task GetPaymentTypesAsync_ReturnsExpectedCodes()
    {
        var service = new ReferenceService(new FakeReferenceRepository(), new FakePermitNotificationService(), NullLogger<ReferenceService>.Instance);

        var results = await service.GetPaymentTypesAsync();

        Assert.Contains(results, item => item.Code == "CC");
        Assert.Contains(results, item => item.Code == "EXMPT");
    }

    [Fact]
    public async Task GetPaymentTypesAsync_FallsBackWhenRepositoryFails()
    {
        var service = new ReferenceService(new ThrowingReferenceRepository(), new FakePermitNotificationService(), NullLogger<ReferenceService>.Instance);

        var results = await service.GetPaymentTypesAsync();

        Assert.Contains(results, item => item.Code == "CC");
        Assert.Contains(results, item => item.Code == "EXMPT");
    }

    private sealed class FakeReferenceRepository : IReferenceRepository
    {
        public Task<IReadOnlyList<ReferenceItemDto>> GetStatesAsync(CancellationToken cancellationToken = default) => Empty();
        public Task<IReadOnlyList<ReferenceItemDto>> GetCitiesAsync(CancellationToken cancellationToken = default) => Empty();
        public Task<IReadOnlyList<ReferenceItemDto>> GetPaymentTypesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ReferenceItemDto>>(new List<ReferenceItemDto>
            {
                new("CC", "Credit Card"),
                new("CHECK", "Check"),
                new("EXMPT", "Fee Exempt")
            });
        public Task<IReadOnlyList<ReferenceItemDto>> GetWorkCategoriesAsync(CancellationToken cancellationToken = default) => Empty();
        public Task<IReadOnlyList<WorkTypeDto>> GetWorkTypesAsync(string? category, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<WorkTypeDto>>(Array.Empty<WorkTypeDto>());
        public Task<IReadOnlyList<ReferenceItemDto>> GetWellUseTypesAsync(string? category, string? workType, CancellationToken cancellationToken = default) => Empty();
        public Task<IReadOnlyList<ReferenceItemDto>> GetDrillMethodsAsync(CancellationToken cancellationToken = default) => Empty();
        public Task<IReadOnlyList<ReferenceItemDto>> GetInspectorsAsync(CancellationToken cancellationToken = default) => Empty();
        private static Task<IReadOnlyList<ReferenceItemDto>> Empty() => Task.FromResult<IReadOnlyList<ReferenceItemDto>>(Array.Empty<ReferenceItemDto>());
    }

    private sealed class ThrowingReferenceRepository : IReferenceRepository
    {
        public Task<IReadOnlyList<ReferenceItemDto>> GetStatesAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<IReadOnlyList<ReferenceItemDto>> GetCitiesAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<IReadOnlyList<ReferenceItemDto>> GetPaymentTypesAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<IReadOnlyList<ReferenceItemDto>> GetWorkCategoriesAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<IReadOnlyList<WorkTypeDto>> GetWorkTypesAsync(string? category, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<IReadOnlyList<ReferenceItemDto>> GetWellUseTypesAsync(string? category, string? workType, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<IReadOnlyList<ReferenceItemDto>> GetDrillMethodsAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<IReadOnlyList<ReferenceItemDto>> GetInspectorsAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }
}
