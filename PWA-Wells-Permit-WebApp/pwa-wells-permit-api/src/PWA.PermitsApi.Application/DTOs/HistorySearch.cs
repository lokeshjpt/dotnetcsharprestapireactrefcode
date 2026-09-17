namespace PWA.PermitsApi.Application.DTOs;

// Server-side search over the legacy history tables (HIST_PERMITS / HIST_WELL_LOC) exposed by the
// intra "Pre-System History Permits (1987-April 2005)" and "History Well Locations" screens. These
// tables are read-only legacy data, so only search + paging + sort are supported.

public sealed record HistoryPermitSearchRequest
{
    public string? PermitNum { get; init; }
    public string? WorkType { get; init; }
    public string? WellComplRptNum { get; init; }
    public DateTime? PermitDate { get; init; }
    public int? PermitYear { get; init; }
    public DateTime? StartDate { get; init; }
    public int? StartYear { get; init; }
    public string? AddrStreet { get; init; }
    public string? CityName { get; init; }
    public string? Consultant { get; init; }
    public string? DrillerName { get; init; }

    public string? SortBy { get; init; }
    public string? SortDir { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}

public sealed record HistoryPermitDto(
    string? PermitNum,
    string? WorkType,
    string? WellComplRptNum,
    string? BldgNum,
    string? AddrStreet,
    string? CityName,
    string? Consultant,
    string? DrillerName,
    string? DrillerLicenseNum,
    string? StateWellNumber,
    string? StartDate,
    string? PermitDate);

public sealed record HistoryPermitSearchResult(IReadOnlyList<HistoryPermitDto> Items, int TotalCount);

public sealed record HistoryWellSearchRequest
{
    public string? PermitNum { get; init; }
    public string? TractNum { get; init; }
    public string? SectNum { get; init; }
    public string? AddrStreet { get; init; }
    public string? AddrCity { get; init; }
    public string? OwnerName { get; init; }
    public string? WellUse { get; init; }

    public string? SortBy { get; init; }
    public string? SortDir { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}

public sealed record HistoryWellDto(
    int WellKey,
    string? PermitNum,
    string? TractNum,
    string? SectNum,
    string? AddrStreet,
    string? AddrCity,
    string? OwnerName,
    string? WellUse);

public sealed record HistoryWellSearchResult(IReadOnlyList<HistoryWellDto> Items, int TotalCount);
