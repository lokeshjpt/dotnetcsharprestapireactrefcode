namespace PWA.PermitsApi.Application.DTOs;

public sealed record InspectionDto(
    DateTime InspectionDate,
    int SlotId,
    string AppId,
    int? InspectorId,
    DateTime? InspectionDateTime,
    string StatusCode,
    string? Notes);
