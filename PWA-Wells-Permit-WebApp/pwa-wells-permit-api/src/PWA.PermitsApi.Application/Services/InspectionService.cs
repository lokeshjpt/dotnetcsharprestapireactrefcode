using Microsoft.Extensions.Logging;
using PWA.PermitsApi.Application.Common;
using PWA.PermitsApi.Application.DTOs;
using PWA.PermitsApi.Application.Interfaces;
using PWA.PermitsApi.Application.Interfaces.Integration;
using PWA.PermitsApi.Application.Interfaces.Repositories;
using PWA.PermitsApi.Domain.Models;

namespace PWA.PermitsApi.Application.Services;

public sealed class InspectionService : IInspectionService
{
    private readonly IInspectionRepository _inspectionRepository;
    private readonly IApplicationRepository _applicationRepository;
    private readonly IPermitNotificationService _notifications;
    private readonly ILogger<InspectionService> _logger;

    public InspectionService(
        IInspectionRepository inspectionRepository,
        IApplicationRepository applicationRepository,
        IPermitNotificationService notifications,
        ILogger<InspectionService> logger)
    {
        _inspectionRepository = inspectionRepository;
        _applicationRepository = applicationRepository;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<IReadOnlyList<InspectionDto>> GetByApplicationAsync(string appId, CancellationToken cancellationToken = default)
    {
        var inspections = await _inspectionRepository.GetByApplicationAsync(appId, cancellationToken);
        return inspections.Select(Map).ToList();
    }

    public async Task<InspectionDto> AddAsync(AddInspectionRequest request, CancellationToken cancellationToken = default)
    {
        var inspection = new Inspection
        {
            InspectionDate = request.InspectionDate,
            SlotId = request.SlotId,
            AppId = request.AppId,
            InspectorId = request.InspectorId,
            InspectionDateTime = request.InspectionDateTime,
            StatusCode = string.IsNullOrWhiteSpace(request.StatusCode) ? "IPEND" : request.StatusCode,
            Notes = request.Notes
        };

        _logger.LogInformation("Adding inspection assignment slot {SlotId} for application {AppId}", request.SlotId, request.AppId);
        var saved = await _inspectionRepository.AddAsync(inspection, cancellationToken);
        await NotifyInspectionScheduledAsync(request.AppId, saved.InspectionDateTime ?? saved.InspectionDate, cancellationToken);
        return Map(saved);
    }

    public async Task<InspectionDto?> UpdateAsync(UpdateInspectionRequest request, CancellationToken cancellationToken = default)
    {
        var inspection = new Inspection
        {
            InspectionDate = request.InspectionDate,
            SlotId = request.SlotId,
            InspectorId = request.InspectorId,
            InspectionDateTime = request.InspectionDateTime,
            StatusCode = string.IsNullOrWhiteSpace(request.StatusCode) ? "IPEND" : request.StatusCode,
            Notes = request.Notes
        };

        var updated = await _inspectionRepository.UpdateAsync(inspection, cancellationToken);
        if (updated is null)
        {
            return null;
        }

        _logger.LogInformation("Updated inspection slot {SlotId} to {StatusCode}", request.SlotId, updated.StatusCode);
        return Map(updated);
    }

    public async Task<bool> DeleteAsync(DateTime inspectionDate, int slotId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Removing inspection slot {SlotId} on {Date:yyyy-MM-dd}", slotId, inspectionDate);
        return await _inspectionRepository.DeleteAsync(inspectionDate.Date, slotId, cancellationToken);
    }

    private const int FallbackMaxSlotsPerDay = 3;

    // Legacy rule (app_avail_cal.jsp): a valid project start date is 10..90 days from today.
    private const int ValidStartMinDays = 10;
    private const int ValidStartMaxDays = 90;

    public async Task<IReadOnlyList<InspectionAvailabilityDto>> GetAvailabilityAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var start = from.Date;
        var end = to.Date;
        if (end < start)
        {
            (start, end) = (end, start);
        }

        int maxSlots;
        HashSet<DateTime> unavailable;
        HashSet<(DateTime Date, int Slot)> booked;

        try
        {
            maxSlots = await _inspectionRepository.GetMaxSlotsPerDayAsync(cancellationToken) ?? FallbackMaxSlotsPerDay;
            var unavailableDays = await _inspectionRepository.GetUnavailableDaysAsync(start, end, cancellationToken);
            var assignments = await _inspectionRepository.GetAssignmentsInRangeAsync(start, end, cancellationToken);
            unavailable = unavailableDays.Select(d => d.InspectionDate.Date).ToHashSet();
            booked = assignments.Select(a => (a.InspectionDate.Date, a.SlotId)).ToHashSet();
        }
        catch (Exception ex)
        {
            // DB unavailable — degrade to a small hardcoded slot set so the calendar still renders.
            _logger.LogWarning(ex, "Inspection availability lookup failed; falling back to default slot set.");
            maxSlots = FallbackMaxSlotsPerDay;
            unavailable = new HashSet<DateTime>();
            booked = new HashSet<(DateTime, int)>();
            await _notifications.SendSystemExceptionAuditAsync("InspectionService.GetAvailability", null, ex, cancellationToken);
        }

        if (maxSlots < 1)
        {
            maxSlots = FallbackMaxSlotsPerDay;
        }

        var results = new List<InspectionAvailabilityDto>();
        for (var day = start; day <= end; day = day.AddDays(1))
        {
            var dayUnavailable = unavailable.Contains(day);
            for (var slot = 1; slot <= maxSlots; slot++)
            {
                var available = !dayUnavailable && !booked.Contains((day, slot));
                results.Add(new InspectionAvailabilityDto(day, slot, available, $"{day:yyyy-MM-dd} Slot {slot}"));
            }
        }

        return results;
    }

    public async Task<InspectionCalendarDto> GetCalendarAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var start = from.Date;
        var end = to.Date;
        if (end < start)
        {
            (start, end) = (end, start);
        }

        int maxSlots;
        HashSet<DateTime> unavailable;
        Dictionary<DateTime, int> assignmentCounts;

        try
        {
            maxSlots = await _inspectionRepository.GetMaxSlotsPerDayAsync(cancellationToken) ?? FallbackMaxSlotsPerDay;
            var unavailableDays = await _inspectionRepository.GetUnavailableDaysAsync(start, end, cancellationToken);
            var assignments = await _inspectionRepository.GetAssignmentsInRangeAsync(start, end, cancellationToken);
            unavailable = unavailableDays.Select(d => d.InspectionDate.Date).ToHashSet();
            assignmentCounts = assignments
                .GroupBy(a => a.InspectionDate.Date)
                .ToDictionary(g => g.Key, g => g.Count());
        }
        catch (Exception ex)
        {
            // DB unavailable — still return weekends/holidays so the calendar renders offline.
            _logger.LogWarning(ex, "Inspection calendar lookup failed; returning weekends/holidays only.");
            maxSlots = FallbackMaxSlotsPerDay;
            unavailable = new HashSet<DateTime>();
            assignmentCounts = new Dictionary<DateTime, int>();
            await _notifications.SendSystemExceptionAuditAsync("InspectionService.GetCalendar", null, ex, cancellationToken);
        }

        if (maxSlots < 1)
        {
            maxSlots = FallbackMaxSlotsPerDay;
        }

        var today = DateTime.Today;
        var validRangeStart = today.AddDays(ValidStartMinDays);
        var validRangeEnd = today.AddDays(ValidStartMaxDays);

        var days = new List<InspectionDayDto>();
        for (var day = start; day <= end; day = day.AddDays(1))
        {
            // Reason precedence mirrors the legacy BeanCalendar: H set first, then U, then M override.
            var reason = string.Empty;
            var available = true;

            if (CountyHolidays.IsWeekend(day) || CountyHolidays.IsCountyHoliday(day))
            {
                reason = "H";
                available = false;
            }

            if (unavailable.Contains(day))
            {
                reason = "U";
                available = false;
            }

            if (assignmentCounts.TryGetValue(day, out var count) && count >= maxSlots)
            {
                reason = "M";
                available = false;
            }

            days.Add(new InspectionDayDto(day, available, reason));
        }

        return new InspectionCalendarDto(validRangeStart, validRangeEnd, days);
    }

    public async Task<InspectionDto> ScheduleAsync(ScheduleInspectionRequest request, CancellationToken cancellationToken = default)
    {
        var inspection = new Inspection
        {
            InspectionDate = request.Date.Date,
            SlotId = request.SlotId,
            AppId = request.ApplicationId,
            InspectionDateTime = request.Date,
            StatusCode = "IRSRV"
        };

        _logger.LogInformation("Scheduling inspection slot {SlotId} on {Date:yyyy-MM-dd} for application {AppId}", request.SlotId, request.Date, request.ApplicationId);
        var saved = await _inspectionRepository.AddAsync(inspection, cancellationToken);
        await NotifyInspectionScheduledAsync(request.ApplicationId, saved.InspectionDateTime ?? saved.InspectionDate, cancellationToken);
        return Map(saved);
    }

    /// <summary>
    /// Sends the applicant "site visit scheduled" notification (best-effort). Loads the application
    /// for the applicant email + project details and the issued permit numbers; a lookup or mail
    /// failure is logged and swallowed so scheduling is never blocked.
    /// </summary>
    private async Task NotifyInspectionScheduledAsync(string? appId, DateTime? inspectionDateTime, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return;
        }

        try
        {
            var application = await _applicationRepository.GetByIdAsync(appId, cancellationToken);
            if (application is null)
            {
                return;
            }

            var permitInfo = await _applicationRepository.GetPermitInfoAsync(appId, cancellationToken);
            var permitNumbers = permitInfo?.Permits.Select(p => p.PermitNumber).ToList() ?? new List<string>();
            await _notifications.SendInspectionScheduledAsync(application, permitNumbers, inspectionDateTime, inspector: null, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Inspection-scheduled notification failed for application {AppId}; continuing.", appId);
            await _notifications.SendSystemExceptionAuditAsync("InspectionService.NotifyInspectionScheduled", "Application " + appId, ex, cancellationToken);
        }
    }

    private static InspectionDto Map(Inspection inspection) => new(
        inspection.InspectionDate,
        inspection.SlotId,
        inspection.AppId,
        inspection.InspectorId,
        inspection.InspectionDateTime,
        inspection.StatusCode,
        inspection.Notes);

    // ---- Intra Inspections menu list screens (read-only passthrough) ----

    public Task<InspectionPendingSearchResult> SearchPendingInspectionsAsync(InspectionPendingSearchRequest request, CancellationToken cancellationToken = default)
        => _inspectionRepository.SearchPendingInspectionsAsync(request, cancellationToken);

    public Task<InspectionDueSearchResult> SearchPendingWcrAsync(InspectionDueSearchRequest request, CancellationToken cancellationToken = default)
        => _inspectionRepository.SearchPendingWcrAsync(request, cancellationToken);

    public Task<InspectionDueSearchResult> SearchPendingGeoLogAsync(InspectionDueSearchRequest request, CancellationToken cancellationToken = default)
        => _inspectionRepository.SearchPendingGeoLogAsync(request, cancellationToken);

    public Task<InspectionHoldSearchResult> SearchHoldListAsync(InspectionHoldSearchRequest request, CancellationToken cancellationToken = default)
        => _inspectionRepository.SearchHoldListAsync(request, cancellationToken);

    public Task<IReadOnlyList<InspectionScheduleLineDto>> GetScheduledInspectionsAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
        => _inspectionRepository.GetScheduledInspectionsAsync(from, to, cancellationToken);
}
