using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PWA.PermitsApi.Application.Configuration;
using PWA.PermitsApi.Application.DTOs;
using PWA.PermitsApi.Application.Interfaces;
using PWA.PermitsApi.Application.Interfaces.Integration;
using PWA.PermitsApi.Application.Interfaces.Repositories;
using PWA.PermitsApi.Domain.Models;

namespace PWA.PermitsApi.Application.Services;

public sealed class FileUploadService : IFileUploadService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".tif", ".tiff"
    };

    private readonly IVirusScanner _virusScanner;
    private readonly IFileTransferService _fileTransferService;
    private readonly IApplicationRepository _applicationRepository;
    private readonly IPermitNotificationService _notifications;
    private readonly FtpOptions _ftpOptions;
    private readonly ILogger<FileUploadService> _logger;

    public FileUploadService(
        IVirusScanner virusScanner,
        IFileTransferService fileTransferService,
        IApplicationRepository applicationRepository,
        IPermitNotificationService notifications,
        IOptions<FtpOptions> ftpOptions,
        ILogger<FileUploadService> logger)
    {
        _virusScanner = virusScanner;
        _fileTransferService = fileTransferService;
        _applicationRepository = applicationRepository;
        _notifications = notifications;
        _ftpOptions = ftpOptions.Value;
        _logger = logger;
    }

    public async Task<FileUploadResult> UploadAsync(string appId, string fileName, string contentType, Stream fileStream, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName);
        if (!AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException($"File type {extension} is not allowed for sitemap uploads.");
        }

        using var memoryStream = new MemoryStream();
        await fileStream.CopyToAsync(memoryStream, cancellationToken);
        var content = memoryStream.ToArray();

        var clean = await _virusScanner.ScanAsync(fileName, content, cancellationToken);
        if (!clean)
        {
            throw new InvalidOperationException("The uploaded file did not pass the ICAP virus scan.");
        }

        var stampedFileName = $"{appId}_{DateTime.UtcNow:yyyyMMddHHmmss}_{fileName}";
        var remotePath = await _fileTransferService.UploadAsync(stampedFileName, content, cancellationToken);
        _logger.LogInformation("Uploaded sitemap file {FileName} for application {AppId} to {RemotePath}", fileName, appId, remotePath);

        return new FileUploadResult(stampedFileName, remotePath, true, $"{contentType} file uploaded successfully.");
    }

    public async Task<DocumentUploadResult> UploadDocumentAsync(string appId, string fileName, string contentType, string documentType, string? otherTypeDesc, Stream fileStream, string uploadedBy, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName);
        if (!AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException($"File type {extension} is not allowed for document uploads.");
        }

        using var memoryStream = new MemoryStream();
        await fileStream.CopyToAsync(memoryStream, cancellationToken);
        var content = memoryStream.ToArray();

        var clean = await _virusScanner.ScanAsync(fileName, content, cancellationToken);
        if (!clean)
        {
            throw new InvalidOperationException("The uploaded file did not pass the ICAP virus scan.");
        }

        var stampedFileName = $"{appId}_{DateTime.UtcNow:yyyyMMddHHmmss}_{fileName}";

        // FTP delivery is best-effort: a transfer failure must not lose the document link record.
        try
        {
            var remotePath = await _fileTransferService.UploadAsync(stampedFileName, content, cancellationToken);
            _logger.LogInformation("Delivered document {FileName} for application {AppId} to {RemotePath}", fileName, appId, remotePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FTP delivery failed for document {FileName} (application {AppId}); recording link anyway.", fileName, appId);
            await _notifications.SendSystemExceptionAuditAsync("FileUploadService.UploadDocument", $"Application {appId} - {fileName} FTP delivery failed", ex, cancellationToken);
        }

        var link = new AppDocumentLink
        {
            AppId = appId,
            DocumentType = documentType,
            OtherTypeDesc = string.IsNullOrWhiteSpace(otherTypeDesc) ? null : otherTypeDesc.Trim(),
            DocumentFilename = stampedFileName
        };

        var seqNum = await _applicationRepository.AddDocumentLinkAsync(link, uploadedBy, cancellationToken);
        return new DocumentUploadResult(seqNum, stampedFileName, documentType);
    }

    public async Task<FileUploadResult> UploadWorkSpecFileAsync(string appId, int workId, int workSpecsId, WorkSpecFileKind kind, string fileName, string contentType, Stream fileStream, string uploadedBy, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName);
        if (!AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException($"File type {extension} is not allowed for {kind} uploads.");
        }

        using var memoryStream = new MemoryStream();
        await fileStream.CopyToAsync(memoryStream, cancellationToken);
        var content = memoryStream.ToArray();

        var clean = await _virusScanner.ScanAsync(fileName, content, cancellationToken);
        if (!clean)
        {
            throw new InvalidOperationException("The uploaded file did not pass the ICAP virus scan.");
        }

        var column = kind == WorkSpecFileKind.GeoLog ? "geolog_file" : "dwr_image";
        var stampedFileName = $"{appId}_{workId}_{workSpecsId}_{DateTime.UtcNow:yyyyMMddHHmmss}_{fileName}";

        // FTP delivery is best-effort: a transfer failure must not lose the recorded file name.
        var remotePath = stampedFileName;
        try
        {
            remotePath = await _fileTransferService.UploadAsync(stampedFileName, content, cancellationToken);
            _logger.LogInformation("Delivered {Kind} file {FileName} for application {AppId} work {WorkId} spec {SpecId} to {RemotePath}", kind, fileName, appId, workId, workSpecsId, remotePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FTP delivery failed for {Kind} file {FileName} (application {AppId}); recording file name anyway.", kind, fileName, appId);
            await _notifications.SendSystemExceptionAuditAsync("FileUploadService.UploadWorkSpecFile", $"Application {appId} - {fileName} FTP delivery failed", ex, cancellationToken);
        }

        await _applicationRepository.UpdateSpecFileAsync(appId, workId, workSpecsId, column, stampedFileName, uploadedBy, cancellationToken);
        return new FileUploadResult(stampedFileName, remotePath, true, $"{kind} file uploaded successfully.");
    }

    public async Task<FileUploadResult> UploadSitemapAsync(string appId, string fileName, string contentType, Stream fileStream, string uploadedBy, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName);
        if (!AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException($"File type {extension} is not allowed for sitemap uploads.");
        }

        using var memoryStream = new MemoryStream();
        await fileStream.CopyToAsync(memoryStream, cancellationToken);
        var content = memoryStream.ToArray();

        var clean = await _virusScanner.ScanAsync(fileName, content, cancellationToken);
        if (!clean)
        {
            await _notifications.SendSitemapScanAuditAsync(appId, "INFECTED - upload rejected", cancellationToken);
            throw new InvalidOperationException("The uploaded file did not pass the ICAP virus scan.");
        }

        var stampedFileName = $"{appId}_{DateTime.UtcNow:yyyyMMddHHmmss}_{fileName}";
        var recordedName = BuildRecordedSitemapName(stampedFileName);

        // FTP delivery is best-effort: a transfer failure must not lose the sitemap record.
        var remotePath = recordedName;
        try
        {
            remotePath = await _fileTransferService.UploadAsync(stampedFileName, content, cancellationToken);
            _logger.LogInformation("Delivered sitemap {FileName} for application {AppId} to {RemotePath}", fileName, appId, remotePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FTP delivery failed for sitemap {FileName} (application {AppId}); recording sitemap anyway.", fileName, appId);
            await _notifications.SendSitemapScanAuditAsync(appId, "FTP delivery failed - " + ex.Message, cancellationToken);
        }

        await _applicationRepository.UpdateSitemapAsync(appId, recordedName, uploadedBy, cancellationToken);

        // Applicant "site map received" confirmation (best-effort). Load the application for the
        // applicant email + project details used in the email body.
        var application = await _applicationRepository.GetByIdAsync(appId, cancellationToken);
        if (application is not null)
        {
            await _notifications.SendSitemapReceivedAsync(application, cancellationToken);
        }

        return new FileUploadResult(recordedName, remotePath, true, "Sitemap file uploaded successfully.");
    }

    /// <summary>
    /// Builds the value stored in APPLICATION_INFO.sitemap_filename. Mirrors the legacy servlet which
    /// records <c>sitemapFileURL + newFileName</c> (a UNC share path), so staff in the intra app can
    /// locate the delivered file. When no prefix is configured the bare file name is stored.
    /// </summary>
    private string BuildRecordedSitemapName(string stampedFileName) =>
        string.IsNullOrEmpty(_ftpOptions.RecordedPathPrefix)
            ? stampedFileName
            : $"{_ftpOptions.RecordedPathPrefix}{stampedFileName}";
}
