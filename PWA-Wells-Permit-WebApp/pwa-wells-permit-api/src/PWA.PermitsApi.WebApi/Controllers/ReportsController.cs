using Microsoft.AspNetCore.Mvc;
using PWA.PermitsApi.Application.DTOs;
using PWA.PermitsApi.Application.Interfaces;

namespace PWA.PermitsApi.WebApi.Controllers;

[ApiController]
[Route("api/reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    // Reconciliation report (legacy report=rec). payType: "cc" | "ch" | "" (all).
    [HttpGet("reconciliation")]
    public async Task<ActionResult<ReconciliationReportDto>> Reconciliation(
        [FromQuery] DateTime start,
        [FromQuery] DateTime end,
        [FromQuery] string? payType,
        CancellationToken cancellationToken)
        => Ok(await _reportService.GetReconciliationAsync(start, end, payType, cancellationToken));

    // Completed Work report (legacy report=compwrkrpt).
    [HttpGet("completed-works")]
    public async Task<ActionResult<CompletedWorkReportDto>> CompletedWorks(
        [FromQuery] DateTime start,
        [FromQuery] DateTime end,
        [FromQuery] string? inspectorId,
        [FromQuery] string? cityCode,
        CancellationToken cancellationToken)
        => Ok(await _reportService.GetCompletedWorksAsync(start, end, inspectorId, cityCode, cancellationToken));

    // Completed Inspections by Inspector (legacy report=inspr).
    [HttpGet("completed-inspections")]
    public async Task<ActionResult<InspectionReportDto>> CompletedInspections(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] string? inspectorId,
        CancellationToken cancellationToken)
        => Ok(await _reportService.GetCompletedInspectionsByInspectorAsync(fromDate, toDate, inspectorId, cancellationToken));

    // Extract to Excel (legacy report=extractrpt).
    [HttpPost("extract")]
    public async Task<ActionResult<ExtractReportResult>> Extract(
        [FromBody] ExtractReportRequest request,
        CancellationToken cancellationToken)
        => Ok(await _reportService.GetExtractAsync(request ?? new ExtractReportRequest(), cancellationToken));

    // Dashboard queue counts (grouped by APPLICATION_INFO.status_code).
    [HttpGet("queue-counts")]
    public async Task<ActionResult<QueueCountsDto>> QueueCounts(CancellationToken cancellationToken)
        => Ok(await _reportService.GetQueueCountsAsync(cancellationToken));
}
