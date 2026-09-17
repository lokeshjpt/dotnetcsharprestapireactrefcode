using PWA.PermitsApi.Application.DTOs;
using PWA.PermitsApi.Domain.Models;

namespace PWA.PermitsApi.Application.Interfaces.Repositories;

public interface IInspectionRepository
{
    // ---- Intra Inspections menu list screens (paged + sorted) ----

    /// <summary>Permits with a pending (IPEND) inspection — inspection_pending_list.jsp.</summary>
    Task<InspectionPendingSearchResult> SearchPendingInspectionsAsync(InspectionPendingSearchRequest request, CancellationToken cancellationToken = default);

    /// <summary>Permits pending a Well Completion Report (PDWR) — pending_dwr_list.jsp.</summary>
    Task<InspectionDueSearchResult> SearchPendingWcrAsync(InspectionDueSearchRequest request, CancellationToken cancellationToken = default);

    /// <summary>Permits pending a GeoLog (PGEO) — pending_geo_list.jsp.</summary>
    Task<InspectionDueSearchResult> SearchPendingGeoLogAsync(InspectionDueSearchRequest request, CancellationToken cancellationToken = default);

    /// <summary>Permits placed on HOLD status — hold_list.jsp.</summary>
    Task<InspectionHoldSearchResult> SearchHoldListAsync(InspectionHoldSearchRequest request, CancellationToken cancellationToken = default);

    /// <summary>Scheduled inspection assignment lines within the inclusive range — inspection_calendar.jsp.</summary>
    Task<IReadOnlyList<InspectionScheduleLineDto>> GetScheduledInspectionsAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Inspection>> GetByApplicationAsync(string appId, CancellationToken cancellationToken = default);
    Task<Inspection> AddAsync(Inspection inspection, CancellationToken cancellationToken = default);
    Task<Inspection?> UpdateAsync(Inspection inspection, CancellationToken cancellationToken = default);

    /// <summary>Removes an inspection assignment identified by its PK (date + slot). Returns true if a row was deleted.</summary>
    Task<bool> DeleteAsync(DateTime inspectionDate, int slotId, CancellationToken cancellationToken = default);

    /// <summary>Reads the configured max inspection slots per day from INSPECTION_CONTROLS.</summary>
    Task<int?> GetMaxSlotsPerDayAsync(CancellationToken cancellationToken = default);

    /// <summary>Reads INSPECTION_UNAVAILABLE_DAYS within the inclusive date range.</summary>
    Task<IReadOnlyList<InspectionUnavailableDay>> GetUnavailableDaysAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);

    /// <summary>Reads booked INSPECTION_ASSIGNMENTS (date + slot) within the inclusive date range.</summary>
    Task<IReadOnlyList<Inspection>> GetAssignmentsInRangeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
}
