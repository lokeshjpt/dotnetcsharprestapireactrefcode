namespace PWA.PermitsApi.Application.Configuration;

public sealed class FtpOptions
{
    /// <summary>FTP host/server the sitemap is delivered to (legacy hardcoded ftpServer, e.g. wellpermit.user.root.acgov.org).</summary>
    public string Server { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Directory on the FTP server the file is uploaded into (legacy servlet <c>cd countyitd</c>,
    /// plus <c>cd test</c> for every non-prod environment). Prod = "countyitd", non-prod = "countyitd/test".
    /// </summary>
    public string RemoteDirectory { get; set; } = "permits";

    /// <summary>
    /// UNC share prefix recorded in APPLICATION_INFO.sitemap_filename so staff can locate the delivered
    /// file (legacy sitemapFileURL, e.g. //pwafile/Group/Engineering and Construction/Flood/Well Permit/[test/]).
    /// </summary>
    public string RecordedPathPrefix { get; set; } = string.Empty;

    /// <summary>
    /// When true (or when <see cref="Server"/> is absent) the client performs a no-op success
    /// upload instead of a live FTP transfer, keeping build/test offline-safe.
    /// </summary>
    public bool UseMock { get; set; }
}

