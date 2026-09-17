using PWA.PermitsApi.Application.DTOs;

namespace PWA.PermitsApi.Application.Interfaces;

public interface IInspectionService
{
    Task<IReadOnlyList<InspectionDto>> GetByApplicationAsync(string appId, CancellationToken cancellationToken = default);
    Task<InspectionDto> AddAsync(AddInspectionRequest request, CancellationToken cancellationToken = default);
    Task<InspectionDto?> UpdateAsync(UpdateInspectionRequest request, CancellationToken cancellationToken = default);

    /// <summary>Removes an inspection assignment (date + slot). Returns true if a row was deleted.</summary>
    Task<bool> DeleteAsync(DateTime inspectionDate, int slotId, CancellationToken cancellationToken = default);

    /// <summary>Computes bookable inspection slots for the inclusive [from, to] date range.</summary>
    Task<IReadOnlyList<InspectionAvailabilityDto>> GetAvailabilityAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the public inspection availability calendar for the inclusive [from, to] range: each day
    /// is flagged available or coded H (weekend/holiday), U (blocked by PWA) or M (max inspections),
    /// plus the valid project-start-date range (today + 10 .. today + 90).
    /// </summary>
    Task<InspectionCalendarDto> GetCalendarAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);

    /// <summary>Books an inspection slot and returns the created assignment.</summary>
    Task<InspectionDto> ScheduleAsync(ScheduleInspectionRequest request, CancellationToken cancellationToken = default);

    // ---- Intra Inspections menu list screens ----

    Task<InspectionPendingSearchResult> SearchPendingInspectionsAsync(InspectionPendingSearchRequest request, CancellationToken cancellationToken = default);
    Task<InspectionDueSearchResult> SearchPendingWcrAsync(InspectionDueSearchRequest request, CancellationToken cancellationToken = default);
    Task<InspectionDueSearchResult> SearchPendingGeoLogAsync(InspectionDueSearchRequest request, CancellationToken cancellationToken = default);
    Task<InspectionHoldSearchResult> SearchHoldListAsync(InspectionHoldSearchRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InspectionScheduleLineDto>> GetScheduledInspectionsAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
}
