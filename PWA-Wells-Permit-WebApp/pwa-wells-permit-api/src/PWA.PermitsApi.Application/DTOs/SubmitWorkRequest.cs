namespace PWA.PermitsApi.Application.DTOs;

public sealed record SubmitWorkRequest
{
    public string WorkCategory { get; init; } = string.Empty;
    public string WorkType { get; init; } = string.Empty;
    public string? WellUseType { get; init; }
    public string? DrillerName { get; init; }
    public string? DrillerLicenseNum { get; init; }
    public string? DrillMethodType { get; init; }
    public string? DrillMethodOtherDesc { get; init; }
    public decimal? WorkFeeRate { get; init; }
    public string? WorkFeeUnit { get; init; }
    public int? WorkSiteMax { get; init; }
    public IReadOnlyList<SubmitWorkSpecRequest> Specs { get; init; } = new List<SubmitWorkSpecRequest>();
}
