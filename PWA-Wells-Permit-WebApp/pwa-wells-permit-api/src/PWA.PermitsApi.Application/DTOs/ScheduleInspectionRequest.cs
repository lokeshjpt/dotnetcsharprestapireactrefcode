namespace PWA.PermitsApi.Application.DTOs;

/// <summary>Books an inspection slot (INSPECTION_ASSIGNMENTS PK = INSPECTION_DATE + SLOT_ID).</summary>
public sealed record ScheduleInspectionRequest
{
    public string ApplicationId { get; init; } = string.Empty;
    public DateTime Date { get; init; }
    public int SlotId { get; init; }
    public string ScheduledBy { get; init; } = "public-portal";
}
