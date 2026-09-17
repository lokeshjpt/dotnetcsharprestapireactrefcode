using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PWA.PermitsApi.Application.DTOs;
using PWA.PermitsApi.Application.Interfaces;

namespace PWA.PermitsApi.WebApi.Controllers;

[ApiController]
[Route("api/files")]
[Authorize]
public sealed class FileUploadController : ControllerBase
{
    private readonly IFileUploadService _fileUploadService;

    public FileUploadController(IFileUploadService fileUploadService)
    {
        _fileUploadService = fileUploadService;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(20_000_000)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<FileUploadResult>> Upload([FromForm] FileUploadForm form, CancellationToken cancellationToken)
    {
        var file = form.File;
        if (file is null || file.Length == 0)
        {
            return BadRequest("A non-empty file is required.");
        }

        await using var stream = file.OpenReadStream();
        var result = await _fileUploadService.UploadAsync(form.AppId, file.FileName, file.ContentType, stream, cancellationToken);
        return Ok(result);
    }
}

public sealed class FileUploadForm
{
    public string AppId { get; set; } = string.Empty;

    public IFormFile File { get; set; } = default!;
}
