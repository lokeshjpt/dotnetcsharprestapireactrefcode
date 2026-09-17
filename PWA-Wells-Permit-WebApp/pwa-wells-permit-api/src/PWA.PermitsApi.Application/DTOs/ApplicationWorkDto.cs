namespace PWA.PermitsApi.Application.DTOs;

public sealed record ApplicationWorkDto(
    int WorkId,
    string WorkCategory,
    string WorkType,
    string? WellUseType,
    string? WorkCategoryDesc,
    string? WorkTypeDesc,
    string? WellUseDesc,
    string? DrillMethodName,
    string? DrillerName,
    string? DrillerLicenseNum,
    string? DrillMethodType,
    string? DrillMethodOtherDesc,
    decimal? WorkFeeRate,
    string? WorkFeeUnit,
    int? WorkSiteMax,
    decimal? WorkSiteExtraRate,
    string StatusCode,
    IReadOnlyList<ApplicationWorkSpecDto> Specs);
