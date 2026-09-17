using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PWA.PermitsApi.Application.Configuration;
using PWA.PermitsApi.Application.Interfaces.Integration;

namespace PWA.PermitsApi.Infrastructure.Services;

/// <summary>
/// ICAP virus scanner. POSTs base64-encoded file bytes to the ICAP microservice (legacy
/// icapScanUrl). HTTP 204 = clean, HTTP 403 = infected (mirrors <c>IcapVirusScanUtil</c>).
/// Falls back to "clean" when <see cref="IcapOptions.UseMock"/> is set or the endpoint is absent,
/// keeping the build/test offline-safe.
/// </summary>
public sealed class IcapVirusScanner : IVirusScanner
{
    public const string HttpClientName = "IcapScan";

    private readonly IcapOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<IcapVirusScanner> _logger;

    public IcapVirusScanner(IOptions<IcapOptions> options, IHttpClientFactory httpClientFactory, ILogger<IcapVirusScanner> logger)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<bool> ScanAsync(string fileName, byte[] content, CancellationToken cancellationToken = default)
    {
        if (_options.UseMock || string.IsNullOrWhiteSpace(_options.ServerUrl))
        {
            _logger.LogWarning("ICAP running in mock mode. Allowing file {FileName} to pass without a live scan.", fileName);
            return true;
        }

        _logger.LogInformation("Scanning {FileName} ({Size} bytes) using ICAP server {ServerUrl}.", fileName, content.Length, _options.ServerUrl);

        var client = _httpClientFactory.CreateClient(HttpClientName);
        var base64 = Convert.ToBase64String(content);
        using var payload = new ByteArrayContent(Encoding.UTF8.GetBytes(base64));
        payload.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        using var response = await client.PostAsync(_options.ServerUrl, payload, cancellationToken);
        var status = (int)response.StatusCode;
        _logger.LogInformation("ICAP scan for {FileName} returned HTTP {Status}.", fileName, status);

        // 204 = clean, 403 = infected. Any other status is treated as clean-fail-open would be unsafe,
        // so we reject only on an explicit infected signal and surface other failures as an exception.
        if (status == 403)
        {
            return false;
        }

        if (status is 204 or 200)
        {
            return true;
        }

        throw new InvalidOperationException($"ICAP scan for {fileName} returned unexpected HTTP {status}.");
    }
}
