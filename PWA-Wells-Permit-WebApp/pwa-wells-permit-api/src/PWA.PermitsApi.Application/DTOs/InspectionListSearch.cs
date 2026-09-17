namespace PWA.PermitsApi.Application.DTOs;

// Server-side search/paging DTOs for the intra Inspections menu list screens, porting the legacy
// ProcessInspectionServlet lists (inspection_pending_list.jsp, pending_dwr_list.jsp,
// pending_geo_list.jsp, hold_list.jsp) and the Inspections Calendar (inspection_calendar.jsp).

// ---- Inspections List (Permits with Pending Inspections) ----

public sealed record InspectionPendingSearchRequest
{
    public int? InspectorId { get; init; }
    public DateTime? InspectionDate { get; init; }

    public string? SortBy { get; init; }
    public string? SortDir { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}

// A single scheduled inspection line shown under an application (date, time, inspector, status).
public sealed record InspectionScheduleLineDto(
    string AppId,
    string InspectionDate,
    string? InspectionTimeDisp,
    int? InspectorId,
    string? InspectorName,
    string StatusCode,
    string? StatusDesc);

public sealed record InspectionPendingItemDto(
    string AppId,
    string? PermitRange,
    string? AppBusinessName,
    string? ApplicantName,
    string? ProjectSiteLocation,
    string? SiteCityName,
    string? ProjectStartDate,
    string? ProjectEndDate,
    IReadOnlyList<InspectionScheduleLineDto> Schedule);

public sealed record InspectionPendingSearchResult(IReadOnlyList<InspectionPendingItemDto> Items, int TotalCount);

// ---- Pending WCR / Pending GeoLog lists ----

public sealed record InspectionDueSearchRequest
{
    public int? InspectorId { get; init; }
    public string? AppId { get; init; }

    public string? SortBy { get; init; }
    public string? SortDir { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}

public sealed record InspectionDueItemDto(
    string AppId,
    string? PermitRange,
    string? AppBusinessName,
    string? ApplicantName,
    string? ProjectSiteLocation,
    string? SiteCityName,
    int? InspectorId,
    string? InspectorName,
    string? DueDate);

public sealed record InspectionDueSearchResult(IReadOnlyList<InspectionDueItemDto> Items, int TotalCount);

// ---- Permits On Hold list ----

public sealed record InspectionHoldSearchRequest
{
    public int? InspectorId { get; init; }

    public string? SortBy { get; init; }
    public string? SortDir { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}

public sealed record InspectionHoldItemDto(
    string AppId,
    string? PermitRange,
    string? AppBusinessName,
    string? ApplicantName,
    string? ProjectSiteLocation,
    string? SiteCityName,
    string? ProjectStartDate,
    string? ProjectEndDate,
    int? InspectorId,
    string? InspectorName,
    string? StatusCode);

public sealed record InspectionHoldSearchResult(IReadOnlyList<InspectionHoldItemDto> Items, int TotalCount);
