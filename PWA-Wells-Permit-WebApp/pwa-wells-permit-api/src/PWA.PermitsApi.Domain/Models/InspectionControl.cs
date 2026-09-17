namespace PWA.PermitsApi.Domain.Models;

/// <summary>Maps EEAOWN.INSPECTION_CONTROLS (ID, MAX_SLOTS_PER_DAY).</summary>
public sealed class InspectionControl
{
    public int Id { get; set; }
    public int? MaxSlotsPerDay { get; set; }
}
