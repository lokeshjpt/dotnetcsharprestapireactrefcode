namespace PWA.PermitsApi.Application.Configuration;

/// <summary>
/// Per-client-IP rate limiting for the API. Throttles scripted/bot floods against the <b>open</b>
/// public endpoints — the anonymous routes that are not already behind the captcha / public-API-token
/// guard — using a sliding window.
/// </summary>
public sealed class RateLimitOptions
{
    /// <summary>When false, no limiting is applied (fail-open).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Maximum requests permitted per client IP within <see cref="WindowSeconds"/>.</summary>
    public int PermitLimit { get; set; } = 120;

    /// <summary>Length of the sliding window, in seconds.</summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>
    /// Number of segments the sliding window is divided into. Higher values give a smoother slide
    /// (finer permit-expiry granularity) at a small bookkeeping cost; the window should divide evenly
    /// by this value (e.g. 60s / 6 = 10s segments). Coerced to a minimum of 1.
    /// </summary>
    public int SegmentsPerWindow { get; set; } = 6;

    /// <summary>Requests queued once the limit is hit (0 = reject immediately with 429).</summary>
    public int QueueLimit { get; set; }
}
