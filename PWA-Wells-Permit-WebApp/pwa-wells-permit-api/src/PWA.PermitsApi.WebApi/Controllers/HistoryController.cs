using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using PWA.PermitsApi.Application.DTOs;
using PWA.PermitsApi.Application.Interfaces;
using PWA.PermitsApi.Application.Interfaces.Integration;

namespace PWA.PermitsApi.WebApi.Controllers;

// Read-only search over the legacy history tables surfaced by the intra
// "Pre-System History Permits (1987-April 2005)" and "History Well Locations" screens, plus the
// detail / edit / add / delete operations ported from the legacy DisplaySearchServlet (s=HD/HWD)
// and UpdateAppServlet (updhist / delhist / updHistWell / delHistWell, plus a=add).
[ApiController]
[Route("api/history")]
public sealed class HistoryController : ControllerBase
{
    private readonly IHistoryService _historyService;
    private readonly IFileTransferService _fileTransfer;

    public HistoryController(IHistoryService historyService, IFileTransferService fileTransfer)
    {
        _historyService = historyService;
        _fileTransfer = fileTransfer;
    }

    [HttpGet("permits")]
    public async Task<ActionResult<HistoryPermitSearchResult>> SearchPermits(
        [FromQuery] HistoryPermitSearchRequest request,
        CancellationToken cancellationToken)
        => Ok(await _historyService.SearchPermitsAsync(request, cancellationToken));

    [HttpGet("wells")]
    public async Task<ActionResult<HistoryWellSearchResult>> SearchWells(
        [FromQuery] HistoryWellSearchRequest request,
        CancellationToken cancellationToken)
        => Ok(await _historyService.SearchWellsAsync(request, cancellationToken));

    // Distinct city name/code pairs for the well-edit city dropdown.
    [HttpGet("cities")]
    public async Task<ActionResult<IReadOnlyList<HistoryCityDto>>> GetCities(CancellationToken cancellationToken)
        => Ok(await _historyService.GetHistoryCitiesAsync(cancellationToken));

    [HttpGet("permits/{permitNum}")]
    public async Task<ActionResult<HistoryPermitDetailDto>> GetPermit(string permitNum, CancellationToken cancellationToken)
    {
        var permit = await _historyService.GetPermitByIdAsync(permitNum, cancellationToken);
        return permit is null ? NotFound() : Ok(permit);
    }

    [HttpPost("permits")]
    [Authorize]
    public async Task<IActionResult> CreatePermit([FromBody] HistoryPermitCreateRequest request, CancellationToken cancellationToken)
    {
        var permitNum = request.PermitNum?.Trim();
        if (string.IsNullOrWhiteSpace(permitNum))
        {
            return BadRequest("Permit Number is required.");
        }

        try
        {
            await _historyService.InsertPermitAsync(request, ResolveActingUser(), cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }

        var created = await _historyService.GetPermitByIdAsync(permitNum, cancellationToken);
        return created is null
            ? Ok(request)
            : CreatedAtAction(nameof(GetPermit), new { permitNum }, created);
    }

    [HttpPut("permits/{permitNum}")]
    [Authorize]
    public async Task<IActionResult> UpdatePermit(string permitNum, [FromBody] HistoryPermitUpdateRequest request, CancellationToken cancellationToken)
    {
        var affected = await _historyService.UpdatePermitAsync(permitNum, request, ResolveActingUser(), cancellationToken);
        if (affected == 0)
        {
            return NotFound();
        }

        var updated = await _historyService.GetPermitByIdAsync(permitNum, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("permits/{permitNum}")]
    [Authorize]
    public async Task<IActionResult> DeletePermit(string permitNum, CancellationToken cancellationToken)
    {
        var affected = await _historyService.DeletePermitAsync(permitNum, cancellationToken);
        return affected == 0 ? NotFound() : NoContent();
    }

    [HttpGet("wells/{wellKey:int}")]
    public async Task<ActionResult<HistoryWellDetailDto>> GetWell(int wellKey, CancellationToken cancellationToken)
    {
        var well = await _historyService.GetWellByIdAsync(wellKey, cancellationToken);
        return well is null ? NotFound() : Ok(well);
    }

    [HttpPost("wells")]
    [Authorize]
    public async Task<IActionResult> CreateWell([FromBody] HistoryWellUpdateRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TractNum) || string.IsNullOrWhiteSpace(request.SectNum))
        {
            return BadRequest("Tract Number and Section Number are required.");
        }

        var newWellKey = await _historyService.InsertWellAsync(request, ResolveActingUser(), cancellationToken);
        var created = await _historyService.GetWellByIdAsync(newWellKey, cancellationToken);
        return created is null
            ? Ok(new { wellKey = newWellKey })
            : CreatedAtAction(nameof(GetWell), new { wellKey = newWellKey }, created);
    }

    [HttpPut("wells/{wellKey:int}")]
    [Authorize]
    public async Task<IActionResult> UpdateWell(int wellKey, [FromBody] HistoryWellUpdateRequest request, CancellationToken cancellationToken)
    {
        var affected = await _historyService.UpdateWellAsync(wellKey, request, ResolveActingUser(), cancellationToken);
        if (affected == 0)
        {
            return NotFound();
        }

        var updated = await _historyService.GetWellByIdAsync(wellKey, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("wells/{wellKey:int}")]
    [Authorize]
    public async Task<IActionResult> DeleteWell(int wellKey, CancellationToken cancellationToken)
    {
        var affected = await _historyService.DeleteWellAsync(wellKey, cancellationToken);
        return affected == 0 ? NotFound() : NoContent();
    }

    // Ports the legacy "Uploaded Image Files" view links (ProcessFileUploadServlet?p=getFile). The
    // stored file name is read back from the permit detail, then streamed from the file-transfer
    // service. fileType selects the column: document | permit | wellrpt.
    [HttpGet("permits/{permitNum}/files/{fileType}")]
    public async Task<IActionResult> GetPermitFile(string permitNum, string fileType, CancellationToken cancellationToken)
    {
        var permit = await _historyService.GetPermitByIdAsync(permitNum, cancellationToken);
        if (permit is null)
        {
            return NotFound();
        }

        var fileName = fileType?.ToLowerInvariant() switch
        {
            "document" => permit.DocumentImageFilename,
            "permit" => permit.PermitImageFilename,
            "wellrpt" => permit.WellComplRptFilename,
            _ => null,
        };

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return NotFound();
        }

        var bytes = await _fileTransfer.DownloadAsync(fileName, cancellationToken);
        if (bytes is null || bytes.Length == 0)
        {
            return NotFound();
        }

        var bareName = fileName.Replace('\\', '/');
        var slash = bareName.LastIndexOf('/');
        if (slash >= 0)
        {
            bareName = bareName[(slash + 1)..];
        }

        return File(bytes, ResolveContentType(bareName), bareName);
    }

    private static string ResolveContentType(string fileName)
    {
        var dot = fileName.LastIndexOf('.');
        var ext = dot >= 0 ? fileName[(dot + 1)..].ToLowerInvariant() : string.Empty;
        return ext switch
        {
            "pdf" => "application/pdf",
            "png" => "image/png",
            "jpg" or "jpeg" => "image/jpeg",
            "gif" => "image/gif",
            "tif" or "tiff" => "image/tiff",
            "bmp" => "image/bmp",
            _ => "application/octet-stream",
        };
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
