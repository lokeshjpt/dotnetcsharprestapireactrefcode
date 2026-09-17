namespace PWA.PermitsApi.Application.Configuration;

/// <summary>
/// Intra-app (pwapermitsintra) specific settings that the ecomm rewrite must be
/// aware of even though it does not use all of them directly. Mirrors the extra
/// keys in the intra pwainit.properties superset.
/// </summary>
public sealed class IntraOptions
{
    /// <summary>The REAL IntelliPay charge endpoint (intra charges the vaulted card here).</summary>
    public string IntelliPayWebapiUrl { get; set; } = string.Empty;

    public string SsrsServer { get; set; } = string.Empty;

    public long MaxFileSizeUpload { get; set; }

    public string FtpServer { get; set; } = string.Empty;

    public string GenCondDoc { get; set; } = string.Empty;
}
