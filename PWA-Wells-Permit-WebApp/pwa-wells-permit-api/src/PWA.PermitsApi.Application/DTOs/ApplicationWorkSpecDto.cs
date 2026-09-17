namespace PWA.PermitsApi.Application.DTOs;

public sealed record ApplicationWorkSpecDto(
    int WorkSpecsId,
    int WorkId,
    string? OwnerWellNum,
    int? DrillCount,
    decimal? HoleDiamIn,
    decimal? CasingDiamIn,
    decimal? SealDepthFt,
    decimal? MaxDepthFt,
    string? Latitude,
    string? Longitude,
    string? StateWellId,
    string? DwrNum,
    string? PermitNum,
    string? ComplWellDwrNum,
    string? DwrNumShared,
    string? DwrImage,
    string? GeologFile,
    string StatusCode);
