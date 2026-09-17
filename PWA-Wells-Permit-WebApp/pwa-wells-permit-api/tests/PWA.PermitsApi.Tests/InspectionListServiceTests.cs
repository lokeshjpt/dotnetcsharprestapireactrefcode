using Microsoft.Extensions.Logging.Abstractions;
using PWA.PermitsApi.Application.DTOs;
using PWA.PermitsApi.Application.Services;

namespace PWA.PermitsApi.Tests;

// Verifies the InspectionService passes the intra Inspections list requests straight through to the
// repository and returns its result unchanged (the SQL parity itself is covered by integration runs).
public sealed class InspectionListServiceTests
{
    private static InspectionService CreateService(FakeInspectionRepository repository)
        => new(repository, new FakeApplicationRepository(), new FakePermitNotificationService(), NullLogger<InspectionService>.Instance);

    [Fact]
    public async Task SearchPendingInspectionsAsync_PassesRequestThroughAndReturnsResult()
    {
        var repository = new FakeInspectionRepository();
        var service = CreateService(repository);

        var request = new InspectionPendingSearchRequest { InspectorId = 4, Page = 2, PageSize = 10 };
        var result = await service.SearchPendingInspectionsAsync(request);

        Assert.Same(request, repository.LastPendingRequest);
        Assert.Equal(1, result.TotalCount);
        var item = Assert.Single(result.Items);
        Assert.Equal("1700000000001", item.AppId);
        Assert.Single(item.Schedule);
    }

    [Fact]
    public async Task SearchPendingWcrAsync_PassesRequestThroughAndReturnsResult()
    {
        var repository = new FakeInspectionRepository();
        var service = CreateService(repository);

        var request = new InspectionDueSearchRequest { AppId = "1700000000002" };
        var result = await service.SearchPendingWcrAsync(request);

        Assert.Same(request, repository.LastWcrRequest);
        Assert.Equal("01/20/2026", Assert.Single(result.Items).DueDate);
    }

    [Fact]
    public async Task SearchPendingGeoLogAsync_PassesRequestThroughAndReturnsResult()
    {
        var repository = new FakeInspectionRepository();
        var service = CreateService(repository);

        var request = new InspectionDueSearchRequest { InspectorId = 4 };
        var result = await service.SearchPendingGeoLogAsync(request);

        Assert.Same(request, repository.LastGeoLogRequest);
        Assert.Equal("01/25/2026", Assert.Single(result.Items).DueDate);
    }

    [Fact]
    public async Task SearchHoldListAsync_PassesRequestThroughAndReturnsResult()
    {
        var repository = new FakeInspectionRepository();
        var service = CreateService(repository);

        var request = new InspectionHoldSearchRequest { InspectorId = 4 };
        var result = await service.SearchHoldListAsync(request);

        Assert.Same(request, repository.LastHoldRequest);
        var item = Assert.Single(result.Items);
        Assert.Equal("HOLD", item.StatusCode);
    }

    [Fact]
    public async Task GetScheduledInspectionsAsync_PassesRangeThroughAndReturnsLines()
    {
        var repository = new FakeInspectionRepository();
        var service = CreateService(repository);

        var from = new DateTime(2026, 1, 1);
        var to = new DateTime(2026, 1, 31);
        var lines = await service.GetScheduledInspectionsAsync(from, to);

        Assert.Equal((from, to), repository.LastScheduledRange);
        Assert.Equal("Sam Insp", Assert.Single(lines).InspectorName);
    }
}
