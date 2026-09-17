namespace PWA.PermitsApi.Application.DTOs;

// Detail + edit DTOs for the legacy history tables (HIST_PERMITS / HIST_WELL_LOC). These port the
// legacy BeanHistPermits.retrieveByPermit / BeanHistWells.retrieveByWellKey SELECTs and the
// UpdateAppServlet updhist / delhist / updHistWell / delHistWell operations. Dates are surfaced as
// ISO yyyy-MM-dd strings so the React date inputs bind directly; an empty date string clears the
// column (stored as NULL).

public sealed record HistoryPermitDetailDto(
    string? PermitNum,
    int? WellLocationWellKey,
    string? WorkType,
    string? CityName,
    string? AddrStreet,
    string? BldgNum,
    string? Consultant,
    string? PermitDate,
    string? StartDate,
    string? EndDate,
    string? StartNoticeDate,
    string? SealDate,
    string? WellComplRptExmpt,
    string? WellComplRptRecvdt,
    string? WellComplRptNum,
    string? StateWellNumber,
    string? DrillerName,
    string? DrillerLicenseNum,
    string? AddDate,
    string? AddBy,
    string? UpdateDate,
    string? UpdateBy,
    string? DocumentImageFilename,
    string? PermitImageFilename,
    string? WellComplRptFilename);

public sealed record HistoryPermitCreateRequest
{
    public string? PermitNum { get; init; }
    public string? WorkType { get; init; }
    public string? CityName { get; init; }
    public string? AddrStreet { get; init; }
    public string? BldgNum { get; init; }
    public string? Consultant { get; init; }
    public string? PermitDate { get; init; }
    public string? StartDate { get; init; }
    public string? EndDate { get; init; }
    public string? StartNoticeDate { get; init; }
    public string? SealDate { get; init; }
    public string? WellComplRptExmpt { get; init; }
    public string? WellComplRptRecvdt { get; init; }
    public string? WellComplRptNum { get; init; }
    public string? StateWellNumber { get; init; }
    public string? DrillerName { get; init; }
    public string? DrillerLicenseNum { get; init; }
}

public sealed record HistoryPermitUpdateRequest
{
    public string? WorkType { get; init; }
    public string? CityName { get; init; }
    public string? AddrStreet { get; init; }
    public string? BldgNum { get; init; }
    public string? Consultant { get; init; }
    public string? PermitDate { get; init; }
    public string? StartDate { get; init; }
    public string? EndDate { get; init; }
    public string? StartNoticeDate { get; init; }
    public string? SealDate { get; init; }
    public string? WellComplRptExmpt { get; init; }
    public string? WellComplRptRecvdt { get; init; }
    public string? WellComplRptNum { get; init; }
    public string? StateWellNumber { get; init; }
    public string? DrillerName { get; init; }
    public string? DrillerLicenseNum { get; init; }
}

public sealed record HistoryWellDetailDto(
    int WellKey,
    string? PermitNum,
    string? HistoryPermitNum,
    string? TractNum,
    string? SectNum,
    string? AddrStreet,
    string? AddrCity,
    string? AddrCityCode,
    string? OwnerName,
    string? CoordX,
    string? CoordY,
    string? MatchLevel,
    string? TsrQq,
    string? RecCode,
    string? PhoneNum,
    string? DrillDate,
    string? Elevation,
    int? TotalDepth,
    decimal? WaterDepth,
    string? Diameter,
    string? WellUse,
    string? LogCode,
    string? Wq,
    string? Wi,
    int? Yield,
    int? DtwCalc,
    string? AddDate,
    string? AddBy,
    string? UpdateDate,
    string? UpdateBy);

public sealed record HistoryWellUpdateRequest
{
    public string? PermitNum { get; init; }
    public string? TractNum { get; init; }
    public string? SectNum { get; init; }
    public string? AddrStreet { get; init; }
    public string? AddrCity { get; init; }
    public string? AddrCityCode { get; init; }
    public string? OwnerName { get; init; }
    public string? CoordX { get; init; }
    public string? CoordY { get; init; }
    public string? MatchLevel { get; init; }
    public string? TsrQq { get; init; }
    public string? RecCode { get; init; }
    public string? PhoneNum { get; init; }
    public string? DrillDate { get; init; }
    public string? Elevation { get; init; }
    public string? TotalDepth { get; init; }
    public string? WaterDepth { get; init; }
    public string? Diameter { get; init; }
    public string? WellUse { get; init; }
    public string? LogCode { get; init; }
    public string? Wq { get; init; }
    public string? Wi { get; init; }
    public string? Yield { get; init; }
    public string? DtwCalc { get; init; }
}

// Distinct city name/code pairs sourced from HIST_WELL_LOC for the well-edit city dropdown
// (legacy BeanHistoryCities.retrieveHistoryCities).
public sealed record HistoryCityDto(string Code, string Name);
