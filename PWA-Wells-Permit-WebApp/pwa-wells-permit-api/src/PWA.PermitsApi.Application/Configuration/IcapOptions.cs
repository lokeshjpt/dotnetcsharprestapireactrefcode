namespace PWA.PermitsApi.Application.Configuration;

public sealed class IcapOptions
{
    /// <summary>ICAP virus-scan microservice endpoint (legacy icapScanUrl).</summary>
    public string ServerUrl { get; set; } = string.Empty;
    public int Port { get; set; } = 1344;

    /// <summary>
    /// When true (or when <see cref="ServerUrl"/> is absent) the scanner returns clean without
    /// performing a live HTTP call, keeping build/test offline-safe.
    /// </summary>
    public bool UseMock { get; set; }
}
