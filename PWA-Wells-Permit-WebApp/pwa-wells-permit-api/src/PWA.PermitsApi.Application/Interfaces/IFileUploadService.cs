using PWA.PermitsApi.Application.DTOs;

namespace PWA.PermitsApi.Application.Interfaces;

public interface IFileUploadService
{
    Task<FileUploadResult> UploadAsync(string appId, string fileName, string contentType, Stream fileStream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Scans, FTP-delivers (best effort) and records an APP_DOCUMENT_LINKS row for an uploaded
    /// document, returning the assigned SEQ_NUM plus the stored file name and document type.
    /// <paramref name="otherTypeDesc"/> is the optional free-text description (OTHER_TYPE_DESC),
    /// captured by the staff "Upload Documents" screen.
    /// </summary>
    Task<DocumentUploadResult> UploadDocumentAsync(string appId, string fileName, string contentType, string documentType, string? otherTypeDesc, Stream fileStream, string uploadedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Scans, FTP-delivers (best effort) and records an uploaded file name directly against a single
    /// APP_WORK_SPECS row — the geolog_file column for the "Enter GeoLog" screen or the dwr_image column
    /// for the per-row "Upload WCR Image" button. Mirrors the legacy ProcessImageDwrUploadServlet flow.
    /// </summary>
    Task<FileUploadResult> UploadWorkSpecFileAsync(string appId, int workId, int workSpecsId, WorkSpecFileKind kind, string fileName, string contentType, Stream fileStream, string uploadedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Scans, FTP-delivers (best effort) and records a received site map directly on APPLICATION_INFO
    /// (sitemap_filename + sitemap_received_date), matching the legacy ProcessFileUploadServlet / updateSitemap flow.
    /// </summary>
    Task<FileUploadResult> UploadSitemapAsync(string appId, string fileName, string contentType, Stream fileStream, string uploadedBy, CancellationToken cancellationToken = default);
}

/// <summary>Which APP_WORK_SPECS file column an upload targets.</summary>
public enum WorkSpecFileKind
{
    /// <summary>Geotechnical log PDF → geolog_file.</summary>
    GeoLog,

    /// <summary>Well Completion Report scan/image → dwr_image.</summary>
    WcrImage
}
