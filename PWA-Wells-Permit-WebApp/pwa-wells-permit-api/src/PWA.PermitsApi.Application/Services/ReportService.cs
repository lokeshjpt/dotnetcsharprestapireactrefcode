using Microsoft.Extensions.Logging;
using PWA.PermitsApi.Application.DTOs;
using PWA.PermitsApi.Application.Interfaces;
using PWA.PermitsApi.Application.Interfaces.Repositories;

namespace PWA.PermitsApi.Application.Services;

public sealed class ReportService : IReportService
{
    private readonly IReportRepository _repository;
    private readonly ILogger<ReportService> _logger;

    public ReportService(IReportRepository repository, ILogger<ReportService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<ReconciliationReportDto> GetReconciliationAsync(DateTime start, DateTime end, string? payType, CancellationToken cancellationToken = default)
    {
        var normalized = (payType ?? string.Empty).Trim().ToLowerInvariant();
        var lines = await _repository.GetReconciliationAsync(start, end, normalized, cancellationToken);
        return new ReconciliationReportDto(normalized, start.ToString("MM/dd/yyyy"), end.ToString("MM/dd/yyyy"), lines);
    }

    public async Task<CompletedWorkReportDto> GetCompletedWorksAsync(DateTime start, DateTime end, string? inspectorId, string? cityCode, CancellationToken cancellationToken = default)
    {
        var lines = await _repository.GetCompletedWorksAsync(start, end, inspectorId, cityCode, cancellationToken);
        var inspectorName = lines.FirstOrDefault(l => string.Equals(l.InspectorId, inspectorId, StringComparison.OrdinalIgnoreCase))?.InspectorName;
        var cityName = lines.FirstOrDefault(l => string.Equals(l.CityCode, cityCode, StringComparison.OrdinalIgnoreCase))?.CityName;
        return new CompletedWorkReportDto(start.ToString("MM/dd/yyyy"), end.ToString("MM/dd/yyyy"), inspectorId, inspectorName, cityCode, cityName, lines);
    }

    public async Task<InspectionReportDto> GetCompletedInspectionsByInspectorAsync(DateTime? fromDate, DateTime? toDate, string? inspectorId, CancellationToken cancellationToken = default)
    {
        var lines = await _repository.GetCompletedInspectionsByInspectorAsync(fromDate, toDate, inspectorId, cancellationToken);
        var inspectorName = lines.FirstOrDefault()?.InspectorName;
        return new InspectionReportDto(fromDate?.ToString("MM/dd/yyyy"), toDate?.ToString("MM/dd/yyyy"), inspectorId, inspectorName, lines);
    }

    public Task<ExtractReportResult> GetExtractAsync(ExtractReportRequest request, CancellationToken cancellationToken = default)
        => _repository.GetExtractAsync(request, cancellationToken);

    public async Task<QueueCountsDto> GetQueueCountsAsync(CancellationToken cancellationToken = default)
    {
        var counts = await _repository.GetStatusCountsAsync(cancellationToken);
        return new QueueCountsDto(counts);
    }
}
