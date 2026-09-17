namespace PWA.PermitsApi.Application.Interfaces.Integration;

public interface IFileTransferService
{
    Task<string> UploadAsync(string fileName, byte[] content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a previously uploaded file from the FTP sitemap location. Best-effort: returns null
    /// when running in mock mode, when the server is absent, or when the transfer fails, so callers
    /// (e.g. attaching the site map to the approval email) can degrade gracefully.
    /// </summary>
    Task<byte[]?> DownloadAsync(string fileName, CancellationToken cancellationToken = default);
}
