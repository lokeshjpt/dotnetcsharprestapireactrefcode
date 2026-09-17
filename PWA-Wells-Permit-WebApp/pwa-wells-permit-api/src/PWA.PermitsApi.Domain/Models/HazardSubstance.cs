namespace PWA.PermitsApi.Domain.Models;

public sealed class HazardSubstance
{
    public string AppId { get; set; } = string.Empty;
    public int HazardSeq { get; set; }
    public string? ConcentrationsPpm { get; set; }
    public string? PelPpm { get; set; }
    public string? HealthEffects { get; set; }
}
