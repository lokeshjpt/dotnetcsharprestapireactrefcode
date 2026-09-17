using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PWA.PermitsApi.Application.Configuration;
using PWA.PermitsApi.Application.Interfaces.Integration;

namespace PWA.PermitsApi.Infrastructure.Services;

/// <summary>
/// Uploads a document to the FTP sitemap location (legacy ftpServer / sitemapFileURL). Falls back
/// to a no-op success when <see cref="FtpOptions.UseMock"/> is set or the server is absent, so the
/// build/test never require a live FTP connection.
/// </summary>
public sealed class FtpFileTransferService : IFileTransferService
{
    private readonly FtpOptions _options;
    private readonly IPermitNotificationService _notifications;
    private readonly ILogger<FtpFileTransferService> _logger;

    public FtpFileTransferService(IOptions<FtpOptions> options, IPermitNotificationService notifications, ILogger<FtpFileTransferService> logger)
    {
        _options = options.Value;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<string> UploadAsync(string fileName, byte[] content, CancellationToken cancellationToken = default)
    {
        var directory = string.IsNullOrWhiteSpace(_options.RemoteDirectory) ? "permits" : _options.RemoteDirectory.Trim('/');

        if (_options.UseMock || string.IsNullOrWhiteSpace(_options.Server))
        {
            var mockPath = $"ftp://local-mock/{directory}/{fileName}";
            _logger.LogWarning("FTP running in mock mode. Simulated upload of {FileName} ({Size} bytes) to {RemotePath}.", fileName, content.Length, mockPath);
            return mockPath;
        }

        var server = _options.Server.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase)
            ? _options.Server.TrimEnd('/')
            : $"ftp://{_options.Server.TrimEnd('/')}";
        var remotePath = $"{server}/{directory}/{fileName}";

        _logger.LogInformation("Uploading {FileName} ({Size} bytes) to {RemotePath} with user {User}.", fileName, content.Length, remotePath, _options.Username);

#pragma warning disable SYSLIB0014 // FtpWebRequest is the minimal built-in FTP client available on net8.0.
        var request = (FtpWebRequest)WebRequest.Create(remotePath);
#pragma warning restore SYSLIB0014
        request.Method = WebRequestMethods.Ftp.UploadFile;
        request.UseBinary = true;
        request.KeepAlive = false;
        request.ContentLength = content.Length;
        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            request.Credentials = new NetworkCredential(_options.Username, _options.Password);
        }

        await using (var requestStream = await request.GetRequestStreamAsync())
        {
            await requestStream.WriteAsync(content, cancellationToken);
        }

        using var response = (FtpWebResponse)await request.GetResponseAsync();
        _logger.LogInformation("FTP upload of {FileName} completed with status {Status}.", fileName, response.StatusDescription?.Trim());
        return remotePath;
    }

    public async Task<byte[]?> DownloadAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var bareName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(bareName))
        {
            return null;
        }

        var directory = string.IsNullOrWhiteSpace(_options.RemoteDirectory) ? "permits" : _options.RemoteDirectory.Trim('/');

        if (_options.UseMock || string.IsNullOrWhiteSpace(_options.Server))
        {
            _logger.LogWarning("FTP running in mock mode. Skipping download of {FileName}.", bareName);
            return null;
        }

        var server = _options.Server.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase)
            ? _options.Server.TrimEnd('/')
            : $"ftp://{_options.Server.TrimEnd('/')}";
        var remotePath = $"{server}/{directory}/{bareName}";

        try
        {
#pragma warning disable SYSLIB0014 // FtpWebRequest is the minimal built-in FTP client available on net8.0.
            var request = (FtpWebRequest)WebRequest.Create(remotePath);
#pragma warning restore SYSLIB0014
            request.Method = WebRequestMethods.Ftp.DownloadFile;
            request.UseBinary = true;
            request.KeepAlive = false;
            if (!string.IsNullOrWhiteSpace(_options.Username))
            {
                request.Credentials = new NetworkCredential(_options.Username, _options.Password);
            }

            using var response = (FtpWebResponse)await request.GetResponseAsync();
            await using var responseStream = response.GetResponseStream();
            using var ms = new MemoryStream();
            await responseStream.CopyToAsync(ms, cancellationToken);
            _logger.LogInformation("FTP download of {FileName} completed ({Size} bytes).", bareName, ms.Length);
            return ms.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FTP download of {FileName} failed; skipping.", bareName);
            await _notifications.SendSystemExceptionAuditAsync("FtpFileTransferService.Download", bareName, ex, cancellationToken);
            return null;
        }
    }
}
