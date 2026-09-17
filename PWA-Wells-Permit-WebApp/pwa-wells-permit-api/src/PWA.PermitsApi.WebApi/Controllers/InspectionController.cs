using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PWA.PermitsApi.Application.DTOs;
using PWA.PermitsApi.Application.Interfaces;

namespace PWA.PermitsApi.WebApi.Controllers;

[ApiController]
[Route("api/inspections")]
[Authorize]
public sealed class InspectionController : ControllerBase
{
    private readonly IInspectionService _inspectionService;

    public InspectionController(IInspectionService inspectionService)
    {
        _inspectionService = inspectionService;
    }

    [HttpGet("{appId}")]
    public async Task<ActionResult<IReadOnlyList<InspectionDto>>> GetByApplication(string appId, CancellationToken cancellationToken)
    {
        var inspections = await _inspectionService.GetByApplicationAsync(appId, cancellationToken);
        return Ok(inspections);
    }

    [HttpPost]
    public async Task<ActionResult<InspectionDto>> Add([FromBody] AddInspectionRequest request, CancellationToken cancellationToken)
    {
        var created = await _inspectionService.AddAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetByApplication), new { appId = created.AppId }, created);
    }

    [HttpPut]
    public async Task<ActionResult<InspectionDto>> Update([FromBody] UpdateInspectionRequest request, CancellationToken cancellationToken)
    {
        var updated = await _inspectionService.UpdateAsync(request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{inspectionDate}/{slotId:int}")]
    public async Task<IActionResult> Delete(DateTime inspectionDate, int slotId, CancellationToken cancellationToken)
    {
        var deleted = await _inspectionService.DeleteAsync(inspectionDate, slotId, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    [HttpGet("availability")]
    public async Task<ActionResult<IReadOnlyList<InspectionAvailabilityDto>>> GetAvailability(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        CancellationToken cancellationToken)
    {
        var availability = await _inspectionService.GetAvailabilityAsync(from, to, cancellationToken);
        return Ok(availability);
    }

    /// <summary>
    /// Public inspection availability calendar used by the ecomm project-date pickers. Anonymous because
    /// applicants are not authenticated; it exposes only date availability (no applicant data).
    /// </summary>
    [AllowAnonymous]
    [HttpGet("calendar")]
    public async Task<ActionResult<InspectionCalendarDto>> GetCalendar(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        CancellationToken cancellationToken)
    {
        var calendar = await _inspectionService.GetCalendarAsync(from, to, cancellationToken);
        return Ok(calendar);
    }

    [HttpPost("schedule")]
    public async Task<ActionResult<InspectionDto>> Schedule([FromBody] ScheduleInspectionRequest request, CancellationToken cancellationToken)
    {
        var scheduled = await _inspectionService.ScheduleAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetByApplication), new { appId = scheduled.AppId }, scheduled);
    }

    // ---- Intra Inspections menu list screens (paged + sorted) ----

    // Permits with Pending Inspections (inspection_pending_list.jsp).
    [HttpGet("pending")]
    public async Task<ActionResult<InspectionPendingSearchResult>> SearchPending(
        [FromQuery] InspectionPendingSearchRequest request,
        CancellationToken cancellationToken)
        => Ok(await _inspectionService.SearchPendingInspectionsAsync(request, cancellationToken));

    // Pending WCR List (pending_dwr_list.jsp).
    [HttpGet("pending-wcr")]
    public async Task<ActionResult<InspectionDueSearchResult>> SearchPendingWcr(
        [FromQuery] InspectionDueSearchRequest request,
        CancellationToken cancellationToken)
        => Ok(await _inspectionService.SearchPendingWcrAsync(request, cancellationToken));

    // Pending GeoLog List (pending_geo_list.jsp).
    [HttpGet("pending-geolog")]
    public async Task<ActionResult<InspectionDueSearchResult>> SearchPendingGeoLog(
        [FromQuery] InspectionDueSearchRequest request,
        CancellationToken cancellationToken)
        => Ok(await _inspectionService.SearchPendingGeoLogAsync(request, cancellationToken));

    // Permits On Hold List (hold_list.jsp).
    [HttpGet("hold")]
    public async Task<ActionResult<InspectionHoldSearchResult>> SearchHold(
        [FromQuery] InspectionHoldSearchRequest request,
        CancellationToken cancellationToken)
        => Ok(await _inspectionService.SearchHoldListAsync(request, cancellationToken));

    // Scheduled inspection assignment lines for the Inspections Calendar (inspection_calendar.jsp).
    [HttpGet("scheduled")]
    public async Task<ActionResult<IReadOnlyList<InspectionScheduleLineDto>>> Scheduled(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        CancellationToken cancellationToken)
        => Ok(await _inspectionService.GetScheduledInspectionsAsync(from, to, cancellationToken));
}
