namespace PWA.PermitsApi.Application.DTOs;

/// <summary>
/// Report DTOs porting the legacy JBoss intra reports (DisplayReportServlet + BeanReport*).
/// Each report returns flat rows; grouping/subtotals are computed on the client to mirror the
/// legacy JSP layout. Money/count totals that the legacy JSPs derived from the same rows are left
/// to the client so the .NET layer stays a faithful data source.
/// </summary>

// ---- Reconciliation Report (report=rec / retrieveReconciliationData) ----
public sealed record ReconciliationLineDto(
    string AppId,
    string? BusinessName,
    string? ApplicantName,
    string? Payer,
    string PaymentType,
    string? PaymentDesc,
    string? CheckNum,
    string? ReceiptNum,
    string? PaidDate,
    string? PaidTime,
    decimal Amount,
    string? PermitRange);

public sealed record ReconciliationReportDto(
    string PayType,
    string? StartDate,
    string? EndDate,
    IReadOnlyList<ReconciliationLineDto> Lines);

// ---- Completed Work Report (report=compwrkrpt / retrieveCompletedWorks) ----
public sealed record CompletedWorkLineDto(
    string InspectorId,
    string? InspectorName,
    string WorkCategory,
    string? WorkCatDesc,
    string WorkType,
    string? WorkDesc,
    string? WellUseType,
    string? WellUseDesc,
    string AppId,
    string? CityCode,
    string? CityName,
    string? PermitNumber,
    string? PermitStatus,
    int DrillCount,
    string? InspectionCompleteDate);

public sealed record CompletedWorkReportDto(
    string? StartDate,
    string? EndDate,
    string? InspectorId,
    string? InspectorName,
    string? CityCode,
    string? CityName,
    IReadOnlyList<CompletedWorkLineDto> Lines);

// ---- Completed Inspections by Inspector (report=inspr / retrieveInspectionDetailsByInspector) ----
public sealed record InspectionReportLineDto(
    string InspectorId,
    string? InspectorName,
    string AppId,
    string? PermitRange,
    string? BusinessName,
    string? ApplicantName,
    string? ProjectSiteLocation,
    string? CityName,
    string? ProjectStartDate,
    string? ProjectEndDate);

public sealed record InspectionReportDto(
    string? FromDate,
    string? ToDate,
    string? InspectorId,
    string? InspectorName,
    IReadOnlyList<InspectionReportLineDto> Lines);

// ---- Extract to Excel (report=extractrpt / retrieveExtractData) ----
public sealed record ExtractLineDto(
    string AppId,
    string? ProjLocation,
    string? CityCode,
    string? CityName,
    string? BusinessName,
    string? WorkId,
    string? WorkCategory,
    string? WorkCatDesc,
    string? WorkType,
    string? WorkDesc,
    string? HistWorkType,
    string? HistWellUse,
    string? WorkSpecsId,
    string? StateWellNum,
    string? Latitude,
    string? Longitude,
    string? TractNum,
    string? SectNum,
    string? PermitNumber,
    string? PermitIssuedDate,
    string? PermitStatus,
    string? PermitStatusDesc);

public sealed record ExtractReportRequest
{
    public string? IssueYear { get; init; }
    public DateTime? PermitFromDate { get; init; }
    public DateTime? PermitToDate { get; init; }
    public string? CityName { get; init; }
    public string? PermitNum { get; init; }
    public string? PermitStatus { get; init; }
    public string? TractNum { get; init; }
    public string? SectNum { get; init; }
    public string? WorkCategory { get; init; }
    public string? WorkType { get; init; }
    public string? HistWorkType { get; init; }
    public string? HistWellUse { get; init; }

    // Server-side sorting/paging for the on-screen grid. A PageSize of 0 (or less) returns every
    // matching row — used by the "Export CSV" action, which must include the full filtered set.
    public string? SortBy { get; init; }
    public string? SortDir { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}

public sealed record ExtractReportResult(IReadOnlyList<ExtractLineDto> Items, int TotalCount);

// ---- Dashboard queue counts (prefilled search boxes) ----
public sealed record QueueCountsDto(IReadOnlyDictionary<string, int> Counts);
