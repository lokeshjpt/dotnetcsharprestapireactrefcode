namespace PWA.PermitsApi.Domain.Models;

public sealed class Inspection
{
    public DateTime InspectionDate { get; set; }
    public int SlotId { get; set; }
    public string AppId { get; set; } = string.Empty;
    public int? InspectorId { get; set; }
    public DateTime? InspectionDateTime { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
