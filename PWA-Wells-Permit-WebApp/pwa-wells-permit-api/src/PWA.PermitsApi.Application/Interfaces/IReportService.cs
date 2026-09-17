using PWA.PermitsApi.Application.DTOs;

namespace PWA.PermitsApi.Application.Interfaces;

public interface IReportService
{
    Task<ReconciliationReportDto> GetReconciliationAsync(DateTime start, DateTime end, string? payType, CancellationToken cancellationToken = default);
    Task<CompletedWorkReportDto> GetCompletedWorksAsync(DateTime start, DateTime end, string? inspectorId, string? cityCode, CancellationToken cancellationToken = default);
    Task<InspectionReportDto> GetCompletedInspectionsByInspectorAsync(DateTime? fromDate, DateTime? toDate, string? inspectorId, CancellationToken cancellationToken = default);
    Task<ExtractReportResult> GetExtractAsync(ExtractReportRequest request, CancellationToken cancellationToken = default);
    Task<QueueCountsDto> GetQueueCountsAsync(CancellationToken cancellationToken = default);
}
