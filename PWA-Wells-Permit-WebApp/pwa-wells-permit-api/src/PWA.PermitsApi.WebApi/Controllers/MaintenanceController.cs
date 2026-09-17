using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using PWA.PermitsApi.Application.DTOs;
using PWA.PermitsApi.Application.Exceptions;
using PWA.PermitsApi.Application.Interfaces;

namespace PWA.PermitsApi.WebApi.Controllers;

// Code Maintenance module (parity with the legacy intra MaintCodeServlet + maint_*_list.jsp). Staff
// manage the EEAOWN reference tables. GET is open (consumed by both the SPA grids and existing
// dropdowns); writes require an authenticated staff identity so the inspector audit columns record
// the acting user. Validation → 400, duplicate / in-use → 409, unknown code → 404.
[ApiController]
[Route("api/maint")]
public sealed class MaintenanceController : ControllerBase
{
    private readonly IMaintenanceService _service;

    public MaintenanceController(IMaintenanceService service)
    {
        _service = service;
    }

    // ---------------------------------------------------------------- Cities
    [HttpGet("cities")]
    public async Task<ActionResult<IReadOnlyList<CityCodeDto>>> Cities(CancellationToken ct) => Ok(await _service.GetCitiesAsync(ct));

    [HttpPost("cities")]
    [Authorize]
    public Task<IActionResult> AddCity([FromBody] CityCodeRequest request, CancellationToken ct) => Handle(() => _service.AddCityAsync(request, ct));

    [HttpPut("cities")]
    [Authorize]
    public Task<IActionResult> UpdateCity([FromBody] CityCodeRequest request, CancellationToken ct) => Handle(() => _service.UpdateCityAsync(request, ct));

    [HttpDelete("cities/{code}")]
    [Authorize]
    public Task<IActionResult> DeleteCity(string code, CancellationToken ct) => Handle(() => _service.DeleteCityAsync(code, ct));

    // ---------------------------------------------------------------- States
    [HttpGet("states")]
    public async Task<ActionResult<IReadOnlyList<StateCodeDto>>> States(CancellationToken ct) => Ok(await _service.GetStatesAsync(ct));

    [HttpPost("states")]
    [Authorize]
    public Task<IActionResult> AddState([FromBody] StateCodeRequest request, CancellationToken ct) => Handle(() => _service.AddStateAsync(request, ct));

    [HttpPut("states")]
    [Authorize]
    public Task<IActionResult> UpdateState([FromBody] StateCodeRequest request, CancellationToken ct) => Handle(() => _service.UpdateStateAsync(request, ct));

    [HttpDelete("states/{code}")]
    [Authorize]
    public Task<IActionResult> DeleteState(string code, CancellationToken ct) => Handle(() => _service.DeleteStateAsync(code, ct));

    // ---------------------------------------------------------------- Work categories
    [HttpGet("work-categories")]
    public async Task<ActionResult<IReadOnlyList<WorkCategoryRowDto>>> WorkCategories(CancellationToken ct) => Ok(await _service.GetWorkCategoriesAsync(ct));

    [HttpPost("work-categories")]
    [Authorize]
    public Task<IActionResult> AddWorkCategory([FromBody] WorkCategoryRequest request, CancellationToken ct) => Handle(() => _service.AddWorkCategoryAsync(request, ct));

    [HttpPut("work-categories")]
    [Authorize]
    public Task<IActionResult> UpdateWorkCategory([FromBody] WorkCategoryRequest request, CancellationToken ct) => Handle(() => _service.UpdateWorkCategoryAsync(request, ct));

    [HttpDelete("work-categories/{code}")]
    [Authorize]
    public Task<IActionResult> DeleteWorkCategory(string code, CancellationToken ct) => Handle(() => _service.DeleteWorkCategoryAsync(code, ct));

    // ---------------------------------------------------------------- Work types
    [HttpGet("work-types")]
    public async Task<ActionResult<IReadOnlyList<WorkTypeRowDto>>> WorkTypes(CancellationToken ct) => Ok(await _service.GetWorkTypesAsync(ct));

    [HttpPost("work-types")]
    [Authorize]
    public Task<IActionResult> AddWorkType([FromBody] WorkTypeRequest request, CancellationToken ct) => Handle(() => _service.AddWorkTypeAsync(request, ct));

    [HttpPut("work-types")]
    [Authorize]
    public Task<IActionResult> UpdateWorkType([FromBody] WorkTypeRequest request, CancellationToken ct) => Handle(() => _service.UpdateWorkTypeAsync(request, ct));

    [HttpDelete("work-types")]
    [Authorize]
    public Task<IActionResult> DeleteWorkType([FromQuery] string cat, [FromQuery] string type, CancellationToken ct)
        => Handle(() => _service.DeleteWorkTypeAsync(cat, type, ct));

    // ---------------------------------------------------------------- Well use types
    [HttpGet("well-use-types")]
    public async Task<ActionResult<IReadOnlyList<WellUseTypeRowDto>>> WellUseTypes(CancellationToken ct) => Ok(await _service.GetWellUseTypesAsync(ct));

    [HttpPost("well-use-types")]
    [Authorize]
    public Task<IActionResult> AddWellUseType([FromBody] WellUseTypeRequest request, CancellationToken ct) => Handle(() => _service.AddWellUseTypeAsync(request, ct));

    [HttpPut("well-use-types")]
    [Authorize]
    public Task<IActionResult> UpdateWellUseType([FromBody] WellUseTypeRequest request, CancellationToken ct) => Handle(() => _service.UpdateWellUseTypeAsync(request, ct));

    [HttpDelete("well-use-types")]
    [Authorize]
    public Task<IActionResult> DeleteWellUseType([FromQuery] string cat, [FromQuery] string type, [FromQuery] string use, CancellationToken ct)
        => Handle(() => _service.DeleteWellUseTypeAsync(cat, type, use, ct));

    // ---------------------------------------------------------------- Drill methods
    [HttpGet("drill-methods")]
    public async Task<ActionResult<IReadOnlyList<DrillMethodRowDto>>> DrillMethods(CancellationToken ct) => Ok(await _service.GetDrillMethodsAsync(ct));

    [HttpPost("drill-methods")]
    [Authorize]
    public Task<IActionResult> AddDrillMethod([FromBody] DrillMethodRequest request, CancellationToken ct) => Handle(() => _service.AddDrillMethodAsync(request, ct));

    [HttpPut("drill-methods")]
    [Authorize]
    public Task<IActionResult> UpdateDrillMethod([FromBody] DrillMethodRequest request, CancellationToken ct) => Handle(() => _service.UpdateDrillMethodAsync(request, ct));

    [HttpDelete("drill-methods/{code}")]
    [Authorize]
    public Task<IActionResult> DeleteDrillMethod(string code, CancellationToken ct) => Handle(() => _service.DeleteDrillMethodAsync(code, ct));

    // ---------------------------------------------------------------- Condition types
    [HttpGet("condition-types")]
    public async Task<ActionResult<IReadOnlyList<ConditionTypeRowDto>>> ConditionTypes(CancellationToken ct) => Ok(await _service.GetConditionTypesAsync(ct));

    [HttpPost("condition-types")]
    [Authorize]
    public Task<IActionResult> AddConditionType([FromBody] ConditionTypeRequest request, CancellationToken ct) => Handle(() => _service.AddConditionTypeAsync(request, ct));

    [HttpPut("condition-types")]
    [Authorize]
    public Task<IActionResult> UpdateConditionType([FromBody] ConditionTypeRequest request, CancellationToken ct) => Handle(() => _service.UpdateConditionTypeAsync(request, ct));

    [HttpDelete("condition-types/{code}")]
    [Authorize]
    public Task<IActionResult> DeleteConditionType(string code, CancellationToken ct) => Handle(() => _service.DeleteConditionTypeAsync(code, ct));

    // ---------------------------------------------------------------- Work condition types
    [HttpGet("work-condition-types")]
    public async Task<ActionResult<IReadOnlyList<WorkConditionTypeRowDto>>> WorkConditionTypes(CancellationToken ct) => Ok(await _service.GetWorkConditionTypesAsync(ct));

    [HttpPost("work-condition-types")]
    [Authorize]
    public Task<IActionResult> AddWorkConditionType([FromBody] WorkConditionTypeRequest request, CancellationToken ct) => Handle(() => _service.AddWorkConditionTypeAsync(request, ct));

    [HttpDelete("work-condition-types")]
    [Authorize]
    public Task<IActionResult> DeleteWorkConditionType([FromQuery] string cat, [FromQuery] string type, [FromQuery] string cond, CancellationToken ct)
        => Handle(() => _service.DeleteWorkConditionTypeAsync(cat, type, cond, ct));

    // ---------------------------------------------------------------- Payment types
    [HttpGet("payment-types")]
    public async Task<ActionResult<IReadOnlyList<PaymentTypeRowDto>>> PaymentTypes(CancellationToken ct) => Ok(await _service.GetPaymentTypesAsync(ct));

    [HttpPost("payment-types")]
    [Authorize]
    public Task<IActionResult> AddPaymentType([FromBody] PaymentTypeRequest request, CancellationToken ct) => Handle(() => _service.AddPaymentTypeAsync(request, ct));

    [HttpPut("payment-types")]
    [Authorize]
    public Task<IActionResult> UpdatePaymentType([FromBody] PaymentTypeRequest request, CancellationToken ct) => Handle(() => _service.UpdatePaymentTypeAsync(request, ct));

    [HttpDelete("payment-types/{code}")]
    [Authorize]
    public Task<IActionResult> DeletePaymentType(string code, CancellationToken ct) => Handle(() => _service.DeletePaymentTypeAsync(code, ct));

    // ---------------------------------------------------------------- Status codes
    [HttpGet("status-codes")]
    public async Task<ActionResult<IReadOnlyList<StatusCodeRowDto>>> StatusCodes(CancellationToken ct) => Ok(await _service.GetStatusCodesAsync(ct));

    [HttpPost("status-codes")]
    [Authorize]
    public Task<IActionResult> AddStatusCode([FromBody] StatusCodeRequest request, CancellationToken ct) => Handle(() => _service.AddStatusCodeAsync(request, ct));

    [HttpPut("status-codes")]
    [Authorize]
    public Task<IActionResult> UpdateStatusCode([FromBody] StatusCodeRequest request, CancellationToken ct) => Handle(() => _service.UpdateStatusCodeAsync(request, ct));

    [HttpDelete("status-codes/{code}")]
    [Authorize]
    public Task<IActionResult> DeleteStatusCode(string code, CancellationToken ct) => Handle(() => _service.DeleteStatusCodeAsync(code, ct));

    // ---------------------------------------------------------------- Document types
    [HttpGet("document-types")]
    public async Task<ActionResult<IReadOnlyList<DocumentTypeRowDto>>> DocumentTypes(CancellationToken ct) => Ok(await _service.GetDocumentTypesAsync(ct));

    [HttpPost("document-types")]
    [Authorize]
    public Task<IActionResult> AddDocumentType([FromBody] DocumentTypeRequest request, CancellationToken ct) => Handle(() => _service.AddDocumentTypeAsync(request, ct));

    [HttpPut("document-types")]
    [Authorize]
    public Task<IActionResult> UpdateDocumentType([FromBody] DocumentTypeRequest request, CancellationToken ct) => Handle(() => _service.UpdateDocumentTypeAsync(request, ct));

    [HttpDelete("document-types/{code}")]
    [Authorize]
    public Task<IActionResult> DeleteDocumentType(string code, CancellationToken ct) => Handle(() => _service.DeleteDocumentTypeAsync(code, ct));

    // ---------------------------------------------------------------- Inspectors
    [HttpGet("inspectors")]
    public async Task<ActionResult<IReadOnlyList<InspectorRowDto>>> Inspectors(CancellationToken ct) => Ok(await _service.GetInspectorsAsync(ct));

    [HttpPost("inspectors")]
    [Authorize]
    public Task<IActionResult> AddInspector([FromBody] InspectorRequest request, CancellationToken ct)
        => Handle(() => _service.AddInspectorAsync(request, ResolveActingUser(), ct));

    [HttpPut("inspectors")]
    [Authorize]
    public Task<IActionResult> UpdateInspector([FromBody] InspectorRequest request, CancellationToken ct)
        => Handle(() => _service.UpdateInspectorAsync(request, ResolveActingUser(), ct));

    // ---------------------------------------------------------------- Inspection unavailable days
    [HttpGet("inspection-unavailable-days")]
    public async Task<ActionResult<IReadOnlyList<UnavailableDayDto>>> UnavailableDays(CancellationToken ct) => Ok(await _service.GetUnavailableDaysAsync(ct));

    [HttpPost("inspection-unavailable-days")]
    [Authorize]
    public Task<IActionResult> AddUnavailableDay([FromBody] UnavailableDayRequest request, CancellationToken ct) => Handle(() => _service.AddUnavailableDayAsync(request, ct));

    [HttpPut("inspection-unavailable-days")]
    [Authorize]
    public Task<IActionResult> UpdateUnavailableDay([FromBody] UnavailableDayRequest request, CancellationToken ct) => Handle(() => _service.UpdateUnavailableDayAsync(request, ct));

    [HttpDelete("inspection-unavailable-days")]
    [Authorize]
    public Task<IActionResult> DeleteUnavailableDay([FromQuery] string date, CancellationToken ct) => Handle(() => _service.DeleteUnavailableDayAsync(date, ct));

    // ---------------------------------------------------------------- Inspection controls (max slots per day)
    [HttpGet("inspection-slots")]
    public async Task<ActionResult<InspectionControlDto>> InspectionSlots(CancellationToken ct)
    {
        var control = await _service.GetInspectionControlAsync(ct);
        return control is null ? Ok(new InspectionControlDto(1, null)) : Ok(control);
    }

    [HttpPut("inspection-slots")]
    [Authorize]
    public Task<IActionResult> UpdateInspectionSlots([FromBody] InspectionControlRequest request, CancellationToken ct)
        => Handle(() => _service.UpdateInspectionControlAsync(request, ct));

    // ---------------------------------------------------------------- Helpers
    private async Task<IActionResult> Handle(Func<Task> action)
    {
        try
        {
            await action();
            return NoContent();
        }
        catch (MaintenanceValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
        catch (MaintenanceConflictException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (MaintenanceNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    private async Task<IActionResult> Handle<T>(Func<Task<T>> action)
    {
        try
        {
            var result = await action();
            return Ok(result);
        }
        catch (MaintenanceValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
        catch (MaintenanceConflictException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (MaintenanceNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    private string ResolveActingUser()
    {
        var user = User.FindFirst(ClaimTypes.Name)?.Value;
        if (string.IsNullOrWhiteSpace(user))
        {
            return "intra-staff";
        }
        return user.Length > 100 ? user[..100] : user;
    }
}
