namespace PWA.PermitsApi.Domain.Models;

/// <summary>Maps EEAOWN.INSPECTION_UNAVAILABLE_DAYS (INSPECTION_DATE, COMMENTS).</summary>
public sealed class InspectionUnavailableDay
{
    public DateTime InspectionDate { get; set; }
    public string? Comments { get; set; }
}
