using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using PWA.PermitsApi.Application.DTOs;
using PWA.PermitsApi.Application.Interfaces;
using PWA.PermitsApi.Application.Interfaces.Integration;

namespace PWA.PermitsApi.WebApi.Controllers;

[ApiController]
[Route("api/applications")]
public sealed class ApplicationController : ControllerBase
{
    private readonly IApplicationService _applicationService;
    private readonly IFileUploadService _fileUploadService;
    private readonly IConditionsService _conditionsService;
    private readonly IPermitNotificationService _notifications;

    public ApplicationController(IApplicationService applicationService, IFileUploadService fileUploadService, IConditionsService conditionsService, IPermitNotificationService notifications)
    {
        _applicationService = applicationService;
        _fileUploadService = fileUploadService;
        _conditionsService = conditionsService;
        _notifications = notifications;
    }

    [HttpGet("{appId}")]
    public async Task<ActionResult<ApplicationDto>> GetById(string appId, CancellationToken cancellationToken)
    {
        var application = await _applicationService.GetByIdAsync(appId, cancellationToken);
        return application is null ? NotFound() : Ok(application);
    }

    [HttpPost]
    [ServiceFilter(typeof(PWA.PermitsApi.WebApi.Authorization.PublicAccessGuardFilter))]
    public async Task<ActionResult<ApplicationDto>> Submit([FromBody] SubmitApplicationRequest request, CancellationToken cancellationToken)
    {
        var created = await _applicationService.SubmitAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { appId = created.AppId }, created);
    }

    [HttpGet("search")]
    [ServiceFilter(typeof(PWA.PermitsApi.WebApi.Authorization.PublicAccessGuardFilter))]
    public async Task<ActionResult<ApplicationSearchResult>> Search([FromQuery] ApplicationSearchRequest request, CancellationToken cancellationToken)
    {
        var results = await _applicationService.SearchAsync(request, cancellationToken);
        return Ok(results);
    }

    // Staff edits from the intra Application Detail page. Each returns the refreshed application.
    // The acting user is read from the signed-in Entra identity for the audit columns.

    [HttpPut("{appId}/project")]
    [Authorize]
    public async Task<ActionResult<ApplicationDto>> UpdateProject(string appId, [FromBody] UpdateProjectInfoRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _applicationService.UpdateProjectInfoAsync(appId, request, ResolveActingUser(), cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPut("{appId}/applicant")]
    [Authorize]
    public async Task<ActionResult<ApplicationDto>> UpdateApplicant(string appId, [FromBody] UpdateApplicantInfoRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _applicationService.UpdateApplicantInfoAsync(appId, request, ResolveActingUser(), cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPut("{appId}/hazard")]
    [Authorize]
    public async Task<ActionResult<ApplicationDto>> UpdateHazard(string appId, [FromBody] UpdateHazardRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _applicationService.UpdateHazardAsync(appId, request, ResolveActingUser(), cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPut("{appId}/works/{workId:int}")]
    [Authorize]
    public async Task<ActionResult<ApplicationDto>> UpdateWork(string appId, int workId, [FromBody] UpdateWorkRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _applicationService.UpdateWorkAsync(appId, workId, request, ResolveActingUser(), cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    // Staff "Add Work": creates a new APP_WORKS row from a work category + type (fees seeded from the
    // WORK_TYPES lookup), returning the refreshed application. 404 when unknown, 409 when the
    // application is terminal (approved/cancelled) or the work type is unknown.
    [HttpPost("{appId}/works")]
    [Authorize]
    public async Task<ActionResult<ApplicationDto>> AddWork(string appId, [FromBody] AddWorkRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _applicationService.AddWorkAsync(appId, request, ResolveActingUser(), cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    // Staff "Cancel Work": sets CAN on a single work + its specs, returning the refreshed application.
    // 404 when the work/application is unknown, 409 when the application is terminal.
    [HttpPost("{appId}/works/{workId:int}/cancel")]
    [Authorize]
    public async Task<ActionResult<ApplicationDto>> CancelWork(string appId, int workId, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _applicationService.CancelWorkAsync(appId, workId, ResolveActingUser(), cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    // Staff "Delete Work": hard-deletes a not-yet-approved work (and its specs/conditions), returning
    // the refreshed application. 404 when unknown, 409 when it is the last work or the app is terminal.
    [HttpDelete("{appId}/works/{workId:int}")]
    [Authorize]
    public async Task<ActionResult<ApplicationDto>> DeleteWork(string appId, int workId, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _applicationService.DeleteWorkAsync(appId, workId, ResolveActingUser(), cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    // "Enter WCR" — staff data entry of the Well Completion Report grid for a work (approved apps only).
    [HttpPut("{appId}/works/{workId:int}/wcr")]
    [Authorize]
    public async Task<ActionResult<ApplicationDto>> UpdateWcr(string appId, int workId, [FromBody] WcrUpdateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _applicationService.UpdateWcrAsync(appId, workId, request, ResolveActingUser(), cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            // Not approved yet / spec not found — a client-correctable conflict.
            return Conflict(ex.Message);
        }
    }

    // ---- Approval Wizard persistence ----

    [HttpPut("{appId}/approval-details")]
    [Authorize]
    public async Task<ActionResult<ApplicationDto>> UpdateApprovalDetails(string appId, [FromBody] UpdateApprovalDetailsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _applicationService.UpdateApprovalDetailsAsync(appId, request, ResolveActingUser(), cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("{appId}/conditions")]
    public async Task<ActionResult<ApplicationConditionsDto>> GetConditions(string appId, CancellationToken cancellationToken)
        => Ok(await _conditionsService.GetConditionsAsync(appId, cancellationToken));

    // Cancels an application that has not yet been approved (parity with the legacy "Cancel Application"
    // button). Returns the refreshed application, 404 when unknown, or 409 when it cannot be cancelled
    // in its current state (approved / already cancelled / payment paid or failed).
    [HttpPost("{appId}/cancel")]
    [Authorize]
    public async Task<ActionResult<ApplicationDto>> Cancel(string appId, CancellationToken cancellationToken)
    {
        try
        {
            var cancelled = await _applicationService.CancelAsync(appId, ResolveActingUser(), cancellationToken);
            return cancelled is null ? NotFound() : Ok(cancelled);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    // Permit-specific extras (approver/date + issued permit numbers) for the printable permit page.
    [HttpGet("{appId}/permit")]
    public async Task<ActionResult<PermitInfoDto>> GetPermitInfo(string appId, CancellationToken cancellationToken)
    {
        var info = await _applicationService.GetPermitInfoAsync(appId, cancellationToken);
        return info is null ? NotFound() : Ok(info);
    }
    [HttpPut("{appId}/works/{workId:int}/conditions")]
    [Authorize]
    public async Task<ActionResult<ApplicationConditionsDto>> UpdateWorkConditions(string appId, int workId, [FromBody] UpdateWorkConditionsRequest request, CancellationToken cancellationToken)
        => Ok(await _conditionsService.UpdateWorkConditionsAsync(appId, workId, request, ResolveActingUser(), cancellationToken));

    // ---- Staff "Upload Documents" (APP_DOCUMENT_LINKS) ----

    [HttpGet("{appId}/documents")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<AppDocumentDto>>> GetDocuments(string appId, CancellationToken cancellationToken)
        => Ok(await _applicationService.GetDocumentsAsync(appId, cancellationToken));

    // Staff upload path (authorized): records the acting staff identity in APP_DOCUMENT_LINKS.ADD_BY
    // and captures the optional free-text description (OTHER_TYPE_DESC). Kept alongside the
    // {applicationId}/documents route above; both are Entra-authorized staff upload endpoints.
    [HttpPost("{appId}/documents/staff")]
    [Authorize]
    [RequestSizeLimit(104_857_600)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<DocumentUploadResult>> UploadDocumentAsStaff(
        string appId,
        [FromForm] UploadDocumentForm form,
        CancellationToken cancellationToken)
    {
        var file = form.File;
        if (file is null || file.Length == 0)
        {
            return BadRequest("A non-empty file is required.");
        }

        if (string.IsNullOrWhiteSpace(form.DocumentType))
        {
            return BadRequest("A documentType is required.");
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _fileUploadService.UploadDocumentAsync(appId, file.FileName, file.ContentType, form.DocumentType, form.OtherTypeDesc, stream, ResolveActingUser(), cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            await _notifications.SendSystemExceptionAuditAsync("ApplicationController.UploadDocumentAsStaff", $"Application {appId} - virus scan service unreachable", ex, cancellationToken);
            return StatusCode(StatusCodes.Status502BadGateway, "The virus scan service is currently unavailable. Please try again later.");
        }
    }

    [HttpDelete("{appId}/documents/{seqNum:int}")]
    [Authorize]
    public async Task<IActionResult> DeleteDocument(string appId, int seqNum, CancellationToken cancellationToken)
        => await _applicationService.DeleteDocumentAsync(appId, seqNum, cancellationToken) ? NoContent() : NotFound();

    // ---- Staff "View/Add Notes" (INSPECTION_NOTES) ----

    [HttpGet("{appId}/notes")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<AppNoteDto>>> GetNotes(string appId, CancellationToken cancellationToken)
        => Ok(await _applicationService.GetNotesAsync(appId, cancellationToken));

    [HttpPost("{appId}/notes")]
    [Authorize]
    public async Task<ActionResult<AppNoteDto>> AddNote(string appId, [FromBody] AddNoteRequest request, CancellationToken cancellationToken)
    {
        var text = request?.NotesText?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return BadRequest("Note text is required.");
        }

        // INSPECTION_NOTES.NOTES_TEXT is varchar(4000), matching the legacy BeanInspectionNotes cap.
        if (text.Length > 4000)
        {
            return BadRequest("Note text cannot exceed 4000 characters.");
        }

        var note = await _applicationService.AddNoteAsync(appId, text, ResolveActingUser(), cancellationToken);
        return Ok(note);
    }

    // ---- Staff "New/Edit Post Approval Extension" (legacy proc=extendu) ----

    [HttpPut("{appId}/extension")]
    [Authorize]
    public async Task<ActionResult<ApplicationDto>> UpdateExtension(string appId, [FromBody] UpdateExtensionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _applicationService.UpdateExtensionAsync(appId, request, ResolveActingUser(), cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    private string ResolveActingUser()
    {
        var user = User.FindFirst(ClaimTypes.Name)?.Value;
        if (string.IsNullOrWhiteSpace(user))
        {
            return "intra-staff";
        }

        // Audit columns (add_by / update_by / approved_by) are VARCHAR(100), which fits the full
        // Entra email. Keep a defensive cap so an unexpectedly long identity can never raise
        // SQL Server error 2628 ("String or binary data would be truncated").
        return user.Length > 100 ? user[..100] : user;
    }

    // Authorized document upload. The public ecomm app does not upload arbitrary documents (it only
    // uploads the site map via {applicationId}/sitemap), so this route requires an Entra token like the
    // other staff endpoints and records the acting staff identity in APP_DOCUMENT_LINKS.ADD_BY.
    [HttpPost("{applicationId}/documents")]
    [Authorize]
    [RequestSizeLimit(104_857_600)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<DocumentUploadResult>> UploadDocument(
        string applicationId,
        [FromForm] UploadDocumentForm form,
        CancellationToken cancellationToken)
    {
        var file = form.File;
        if (file is null || file.Length == 0)
        {
            return BadRequest("A non-empty file is required.");
        }

        if (string.IsNullOrWhiteSpace(form.DocumentType))
        {
            return BadRequest("A documentType is required.");
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _fileUploadService.UploadDocumentAsync(applicationId, file.FileName, file.ContentType, form.DocumentType, form.OtherTypeDesc, stream, ResolveActingUser(), cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            // The virus-scan service is unreachable. Fail closed (the file is not stored) and return
            // a clean 502 so the client shows the mail/email fallback instead of a raw 500.
            await _notifications.SendSystemExceptionAuditAsync("ApplicationController.UploadDocument", $"Application {applicationId} - virus scan service unreachable", ex, cancellationToken);
            return StatusCode(StatusCodes.Status502BadGateway, "The virus scan service is currently unavailable. Please try again later, or mail/email your file.");
        }
    }

    [HttpPost("{applicationId}/sitemap")]
    [ServiceFilter(typeof(PWA.PermitsApi.WebApi.Authorization.PublicAccessGuardFilter))]
    [RequestSizeLimit(104_857_600)]
    [Consumes("multipart/form-data")]
    public Task<ActionResult<FileUploadResult>> UploadSitemap(
        string applicationId,
        [FromForm] SitemapUploadForm form,
        CancellationToken cancellationToken)
        // Public (ecomm) upload path — the applicant is recorded as the uploader.
        => HandleSitemapUploadAsync(applicationId, form, "public-portal", cancellationToken);

    // Staff (intra) upload path: when the applicant did not provide a site map, county staff can
    // upload it from the Approval Wizard. Authorized so the acting staff identity is captured in
    // APPLICATION_INFO.update_by — the anonymous route above runs under the (handler-less) default
    // scheme and cannot resolve the AzureAD principal.
    [HttpPost("{applicationId}/sitemap/staff")]
    [Authorize]
    [RequestSizeLimit(104_857_600)]
    [Consumes("multipart/form-data")]
    public Task<ActionResult<FileUploadResult>> UploadSitemapAsStaff(
        string applicationId,
        [FromForm] SitemapUploadForm form,
        CancellationToken cancellationToken)
        => HandleSitemapUploadAsync(applicationId, form, ResolveActingUser(), cancellationToken);

    // "Enter GeoLog" — staff upload of a geotechnical log for a single well spec (approved apps only).
    [HttpPost("{appId}/works/{workId:int}/specs/{workSpecsId:int}/geolog")]
    [Authorize]
    [RequestSizeLimit(104_857_600)]
    [Consumes("multipart/form-data")]
    public Task<ActionResult<FileUploadResult>> UploadGeolog(
        string appId,
        int workId,
        int workSpecsId,
        [FromForm] SitemapUploadForm form,
        CancellationToken cancellationToken)
        => HandleWorkSpecFileUploadAsync(appId, workId, workSpecsId, WorkSpecFileKind.GeoLog, form, cancellationToken);

    // Per-row "Upload WCR Image" button on the Enter WCR grid.
    [HttpPost("{appId}/works/{workId:int}/specs/{workSpecsId:int}/wcr-image")]
    [Authorize]
    [RequestSizeLimit(104_857_600)]
    [Consumes("multipart/form-data")]
    public Task<ActionResult<FileUploadResult>> UploadWcrImage(
        string appId,
        int workId,
        int workSpecsId,
        [FromForm] SitemapUploadForm form,
        CancellationToken cancellationToken)
        => HandleWorkSpecFileUploadAsync(appId, workId, workSpecsId, WorkSpecFileKind.WcrImage, form, cancellationToken);

    private async Task<ActionResult<FileUploadResult>> HandleWorkSpecFileUploadAsync(
        string appId,
        int workId,
        int workSpecsId,
        WorkSpecFileKind kind,
        SitemapUploadForm form,
        CancellationToken cancellationToken)
    {
        var file = form.File;
        if (file is null || file.Length == 0)
        {
            return BadRequest("A non-empty file is required.");
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _fileUploadService.UploadWorkSpecFileAsync(appId, workId, workSpecsId, kind, file.FileName, file.ContentType, stream, ResolveActingUser(), cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            await _notifications.SendSystemExceptionAuditAsync("ApplicationController.UploadWorkSpecFile", $"Application {appId} - virus scan service unreachable", ex, cancellationToken);
            return StatusCode(StatusCodes.Status502BadGateway, "The virus scan service is currently unavailable. Please try again later.");
        }
    }

    private async Task<ActionResult<FileUploadResult>> HandleSitemapUploadAsync(
        string applicationId,
        SitemapUploadForm form,
        string uploadedBy,
        CancellationToken cancellationToken)
    {
        var file = form.File;
        if (file is null || file.Length == 0)
        {
            return BadRequest("A non-empty file is required.");
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _fileUploadService.UploadSitemapAsync(applicationId, file.FileName, file.ContentType, stream, uploadedBy, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            // The virus-scan service is unreachable. Fail closed (the sitemap is not stored) and
            // return a clean 502 so the client shows the mail/email fallback instead of a raw 500.
            await _notifications.SendSystemExceptionAuditAsync("ApplicationController.UploadSitemap", $"Application {applicationId} - virus scan service unreachable", ex, cancellationToken);
            return StatusCode(StatusCodes.Status502BadGateway, "The virus scan service is currently unavailable. Please try again later, or mail/email your site map.");
        }
    }
}

public sealed class SitemapUploadForm
{
    public IFormFile File { get; set; } = default!;
}

public sealed class UploadDocumentForm
{
    public IFormFile File { get; set; } = default!;

    public string DocumentType { get; set; } = string.Empty;

    // Optional free-text description (APP_DOCUMENT_LINKS.OTHER_TYPE_DESC).
    public string? OtherTypeDesc { get; set; }
}
