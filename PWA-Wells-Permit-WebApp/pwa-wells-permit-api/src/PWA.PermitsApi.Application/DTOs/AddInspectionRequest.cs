namespace PWA.PermitsApi.Application.DTOs;

public sealed record AddInspectionRequest
{
    public string AppId { get; init; } = string.Empty;
    public DateTime InspectionDate { get; init; }
    public int SlotId { get; init; }
    public int? InspectorId { get; init; }
    public DateTime? InspectionDateTime { get; init; }
    public string AddedBy { get; init; } = string.Empty;
    public string? Notes { get; init; }

    /// <summary>Assignment status to store. Defaults to IPEND ("Inspection Pending") because the intra
    /// "Add a Site Visit" flow captures inspector + date + time in one confirmed step, mirroring the
    /// legacy Java Update that advances a reserved slot to IPEND (which is what marks the app's
    /// Inspections/Reviews step complete).</summary>
    public string StatusCode { get; init; } = "IPEND";
}
