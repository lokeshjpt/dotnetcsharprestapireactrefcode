namespace PWA.PermitsApi.Application.DTOs;

public sealed record SubmitHazardSubstanceRequest
{
    public string? Concentration { get; init; }
    public string? PelPpm { get; init; }
    public string? HealthEffects { get; init; }
}
