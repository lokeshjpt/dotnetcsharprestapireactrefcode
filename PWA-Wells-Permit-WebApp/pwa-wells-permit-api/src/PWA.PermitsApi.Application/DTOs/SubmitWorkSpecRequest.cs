namespace PWA.PermitsApi.Application.DTOs;

public sealed record SubmitWorkSpecRequest
{
    public string? OwnerWellNum { get; init; }
    public int? DrillCount { get; init; }
    public decimal? HoleDiamIn { get; init; }
    public decimal? CasingDiamIn { get; init; }
    public decimal? SealDepthFt { get; init; }
    public decimal? MaxDepthFt { get; init; }
    public string? Latitude { get; init; }
    public string? Longitude { get; init; }
    public string? StateWellId { get; init; }
    public string? DwrNum { get; init; }
    public string? PermitNum { get; init; }
}
