using Microsoft.AspNetCore.Mvc;
using PWA.PermitsApi.Application.DTOs;
using PWA.PermitsApi.Application.Interfaces;

namespace PWA.PermitsApi.WebApi.Controllers;

[ApiController]
[Route("api/ref")]
public sealed class ReferenceController : ControllerBase
{
    private readonly IReferenceService _referenceService;

    public ReferenceController(IReferenceService referenceService)
    {
        _referenceService = referenceService;
    }

    [HttpGet("states")]
    public async Task<ActionResult<IReadOnlyList<ReferenceItemDto>>> States(CancellationToken cancellationToken)
        => Ok(await _referenceService.GetStatesAsync(cancellationToken));

    [HttpGet("cities")]
    public async Task<ActionResult<IReadOnlyList<ReferenceItemDto>>> Cities(CancellationToken cancellationToken)
        => Ok(await _referenceService.GetCitiesAsync(cancellationToken));

    [HttpGet("payment-types")]
    public async Task<ActionResult<IReadOnlyList<ReferenceItemDto>>> PaymentTypes(CancellationToken cancellationToken)
        => Ok(await _referenceService.GetPaymentTypesAsync(cancellationToken));

    [HttpGet("work-categories")]
    public async Task<ActionResult<IReadOnlyList<ReferenceItemDto>>> WorkCategories(CancellationToken cancellationToken)
        => Ok(await _referenceService.GetWorkCategoriesAsync(cancellationToken));

    [HttpGet("work-types")]
    public async Task<ActionResult<IReadOnlyList<WorkTypeDto>>> WorkTypes([FromQuery(Name = "cat")] string? cat, CancellationToken cancellationToken)
        => Ok(await _referenceService.GetWorkTypesAsync(cat, cancellationToken));

    [HttpGet("well-use-types")]
    public async Task<ActionResult<IReadOnlyList<ReferenceItemDto>>> WellUseTypes([FromQuery(Name = "cat")] string? cat, [FromQuery(Name = "type")] string? type, CancellationToken cancellationToken)
        => Ok(await _referenceService.GetWellUseTypesAsync(cat, type, cancellationToken));

    [HttpGet("drill-methods")]
    public async Task<ActionResult<IReadOnlyList<ReferenceItemDto>>> DrillMethods(CancellationToken cancellationToken)
        => Ok(await _referenceService.GetDrillMethodsAsync(cancellationToken));

    [HttpGet("inspectors")]
    public async Task<ActionResult<IReadOnlyList<ReferenceItemDto>>> Inspectors(CancellationToken cancellationToken)
        => Ok(await _referenceService.GetInspectorsAsync(cancellationToken));
}
