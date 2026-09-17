namespace PWA.PermitsApi.Application.DTOs;

/// <summary>
/// Staff edit of a single "Work Requesting Permit" section: the work-level fields (APP_WORKS)
/// plus the editable numeric fields of each existing well spec row (APP_WORK_SPECS).
/// Spec rows are matched by <see cref="UpdateWorkSpecRequest.WorkSpecsId"/>; rows are not added or removed here.
/// </summary>
public sealed record UpdateWorkRequest
{
    public string? WellUseType { get; init; }
    public string? DrillerName { get; init; }
    public string? DrillerLicenseNum { get; init; }
    public string? DrillMethodType { get; init; }
    public string? DrillMethodOtherDesc { get; init; }
    public decimal? WorkFeeRate { get; init; }
    public string? WorkFeeUnit { get; init; }
    public int? WorkSiteMax { get; init; }

    public IReadOnlyList<UpdateWorkSpecRequest> Specs { get; init; } = new List<UpdateWorkSpecRequest>();
}

public sealed record UpdateWorkSpecRequest
{
    public int WorkSpecsId { get; init; }
    public string? OwnerWellNum { get; init; }
    public int? DrillCount { get; init; }
    public decimal? HoleDiamIn { get; init; }
    public decimal? CasingDiamIn { get; init; }
    public decimal? SealDepthFt { get; init; }
    public decimal? MaxDepthFt { get; init; }
    public string? Latitude { get; init; }
    public string? Longitude { get; init; }
}
