namespace PWA.PermitsApi.Application.DTOs;

public sealed record UpdateInspectionRequest
{
    public DateTime InspectionDate { get; init; }
    public int SlotId { get; init; }
    public int? InspectorId { get; init; }
    public DateTime? InspectionDateTime { get; init; }
    public string StatusCode { get; init; } = "IPEND";
    public string? Notes { get; init; }
}
