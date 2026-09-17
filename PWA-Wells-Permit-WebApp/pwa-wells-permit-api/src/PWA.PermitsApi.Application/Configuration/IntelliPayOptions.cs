namespace PWA.PermitsApi.Application.Configuration;

public sealed class IntelliPayOptions
{
    /// <summary>Autoterminal endpoint (legacy intellipayUrl) used for the ecomm $0 vault/pre-auth.</summary>
    public string ApiUrl { get; set; } = string.Empty;

    /// <summary>Real charge endpoint (legacy intellipayWebapiUrl) used by intra to charge the vaulted card.</summary>
    public string WebApiUrl { get; set; } = string.Empty;

    public string MerchantKey { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// When true (or when <see cref="ApiUrl"/>/credentials are absent) the gateway runs in
    /// offline mock mode and never performs a live HTTP call. Keeps build/test offline-safe.
    /// </summary>
    public bool UseMock { get; set; }
}
