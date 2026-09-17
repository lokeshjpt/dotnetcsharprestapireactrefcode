namespace PWA.PermitsApi.Application.DTOs;

/// <summary>A single inspection slot on a given day and whether it can still be booked.</summary>
public sealed record InspectionAvailabilityDto(DateTime Date, int SlotId, bool Available, string Label);
