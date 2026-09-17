using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PWA.PermitsApi.Application.Configuration;
using PWA.PermitsApi.Application.Interfaces.Integration;

namespace PWA.PermitsApi.Infrastructure.Services;

/// <summary>
/// Google reCAPTCHA verifier. POSTs the client's response token together with the configured secret
/// to the reCAPTCHA siteverify endpoint and reports whether the provider accepted it. Any missing
/// token, non-success verdict, transport error, or malformed response resolves to <c>false</c> (deny)
/// — the guard only lets a request through on an explicit success.
/// </summary>
public sealed class GoogleReCaptchaVerifier : ICaptchaVerifier
{
    public const string HttpClientName = "GoogleReCaptcha";

    private readonly CaptchaOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GoogleReCaptchaVerifier> _logger;

    public GoogleReCaptchaVerifier(IOptions<CaptchaOptions> options, IHttpClientFactory httpClientFactory, ILogger<GoogleReCaptchaVerifier> logger)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<bool> VerifyAsync(string? token, string? remoteIp = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            // Guard is disabled; nothing to verify against. Callers gate on CaptchaOptions.IsEnabled,
            // but stay defensive here so a stray call never falsely "passes".
            return false;
        }

        var form = new List<KeyValuePair<string, string>>
        {
            new("secret", _options.SecretKey),
            new("response", token),
        };
        if (!string.IsNullOrWhiteSpace(remoteIp))
        {
            form.Add(new("remoteip", remoteIp));
        }

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var content = new FormUrlEncodedContent(form);
            using var response = await client.PostAsync(_options.VerifyUrl, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Captcha verify returned HTTP {Status}.", (int)response.StatusCode);
                return false;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var success = doc.RootElement.TryGetProperty("success", out var successEl)
                && successEl.ValueKind == JsonValueKind.True;

            if (!success)
            {
                var codes = doc.RootElement.TryGetProperty("error-codes", out var errEl)
                    ? errEl.ToString()
                    : "(none)";
                _logger.LogWarning("Captcha verification failed. error-codes={ErrorCodes}", codes);
            }

            return success;
        }
        catch (Exception ex)
        {
            // Network/timeout/parse failures must not let a request through, but also must be logged so
            // an outage of the captcha provider is visible rather than silently blocking all traffic.
            _logger.LogWarning(ex, "Captcha verification call failed.");
            return false;
        }
    }
}
