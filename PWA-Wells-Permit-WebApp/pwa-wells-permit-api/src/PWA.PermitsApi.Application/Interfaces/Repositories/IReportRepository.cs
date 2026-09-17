using PWA.PermitsApi.Application.DTOs;

namespace PWA.PermitsApi.Application.Interfaces.Repositories;

/// <summary>
/// Data access for the intra operational reports and the dashboard queue counts, porting the
/// legacy BeanReport* SQL. All queries are parameterized (the legacy code concatenated strings).
/// </summary>
public interface IReportRepository
{
    Task<IReadOnlyList<ReconciliationLineDto>> GetReconciliationAsync(DateTime start, DateTime end, string payType, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CompletedWorkLineDto>> GetCompletedWorksAsync(DateTime start, DateTime end, string? inspectorId, string? cityCode, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InspectionReportLineDto>> GetCompletedInspectionsByInspectorAsync(DateTime? fromDate, DateTime? toDate, string? inspectorId, CancellationToken cancellationToken = default);

    Task<ExtractReportResult> GetExtractAsync(ExtractReportRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, int>> GetStatusCountsAsync(CancellationToken cancellationToken = default);
}
