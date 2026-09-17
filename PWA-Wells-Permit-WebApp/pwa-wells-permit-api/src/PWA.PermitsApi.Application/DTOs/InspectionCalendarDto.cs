namespace PWA.PermitsApi.Application.DTOs;

/// <summary>One day on the inspection availability calendar (mirrors the legacy BeanCalendar cell).</summary>
/// <param name="Date">The calendar day.</param>
/// <param name="Available">True when the day can be selected for inspection.</param>
/// <param name="Reason">
/// Why the day is unavailable: "H" = weekend/County holiday, "U" = date blocked by Public Works Agency,
/// "M" = maximum inspections reached. Empty when the day is available.
/// </param>
public sealed record InspectionDayDto(DateTime Date, bool Available, string Reason);

/// <summary>
/// Inspection availability calendar for the public ecomm project-date pickers. Mirrors the legacy
/// app_avail_cal.jsp: a valid project start date must be at least 10 and no more than 90 days from
/// today and fall on an available inspection day.
/// </summary>
/// <param name="ValidRangeStart">Earliest valid project start date (today + 10 days).</param>
/// <param name="ValidRangeEnd">Latest valid project start date (today + 90 days).</param>
/// <param name="Days">Per-day availability across the requested range.</param>
public sealed record InspectionCalendarDto(
    DateTime ValidRangeStart,
    DateTime ValidRangeEnd,
    IReadOnlyList<InspectionDayDto> Days);
