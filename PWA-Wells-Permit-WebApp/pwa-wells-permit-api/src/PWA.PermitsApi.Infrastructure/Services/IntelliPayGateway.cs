using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PWA.PermitsApi.Application.Configuration;
using PWA.PermitsApi.Application.Interfaces.Integration;

namespace PWA.PermitsApi.Infrastructure.Services;

/// <summary>
/// IntelliPay gateway. Ecomm vaults a card for $0 against the autoterminal endpoint; intra later
/// charges the vaulted custid against the webapi endpoint. Both fall back to an offline mock when
/// <see cref="IntelliPayOptions.UseMock"/> is set or the endpoint/credentials are absent, so the
/// build and tests never require a live IntelliPay connection.
/// </summary>
public sealed class IntelliPayGateway : IIntelliPayGateway
{
    public const string HttpClientName = "IntelliPay";

    private readonly IntelliPayOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<IntelliPayGateway> _logger;

    public IntelliPayGateway(IOptions<IntelliPayOptions> options, IHttpClientFactory httpClientFactory, ILogger<IntelliPayGateway> logger)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    private bool UseVaultMock =>
        _options.UseMock
        || string.IsNullOrWhiteSpace(_options.ApiUrl)
        || string.IsNullOrWhiteSpace(_options.MerchantKey)
        || string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<IntelliPayVaultResult> VaultZeroDollarAsync(string applicationId, CancellationToken cancellationToken = default)
    {
        if (UseVaultMock)
        {
            var suffix = applicationId.Length <= 8 ? applicationId : applicationId[^8..];
            var mockCustId = $"MOCK{suffix}";
            _logger.LogWarning("IntelliPay running in mock mode. Returning vaulted custid {CustomerId} for application {AppId} without a live $0 authorization.", mockCustId, applicationId);
            return new IntelliPayVaultResult(mockCustId, true);
        }

        // Live mode: the card cannot be vaulted server-side without cardholder entry. The real $0
        // vault happens inside the IntelliPay lightbox popup on the payment page, which returns the
        // custid client-side; it is then persisted via PreAuthorizeAsync. So at submit time we leave
        // the custid pending (null) — matching the legacy flow where custid arrives with the callback.
        _logger.LogInformation("IntelliPay live mode: deferring $0 vault for application {AppId} to the client lightbox.", applicationId);
        await Task.CompletedTask;
        return new IntelliPayVaultResult(null, false);
    }

    public async Task<string> FetchLightboxScriptsAsync(CancellationToken cancellationToken = default)
    {
        if (UseVaultMock)
        {
            _logger.LogWarning("IntelliPay running in mock mode. Returning stub lightbox script block.");
            return "<!-- IntelliPay mock lightbox: credentials not configured or UseMock=true -->";
        }

        var form = new Dictionary<string, string>
        {
            ["merchantkey"] = _options.MerchantKey,
            ["apikey"] = _options.ApiKey
        };

        _logger.LogInformation("Fetching IntelliPay lightbox scripts from {ApiUrl}", _options.ApiUrl);
        return await PostAsync(_options.ApiUrl, form, cancellationToken);
    }

    public async Task<string> ChargeStoredCustomerAsync(string appId, string customerId, decimal amount, CancellationToken cancellationToken = default)
    {
        if (_options.UseMock
            || string.IsNullOrWhiteSpace(_options.WebApiUrl)
            || string.IsNullOrWhiteSpace(_options.MerchantKey)
            || string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            var offlineId = $"MOCK-{appId}-{DateTime.UtcNow:yyyyMMddHHmmss}";
            _logger.LogWarning("IntelliPay WebApiUrl not configured (mock mode). Returning mock approved charge response {TransactionId}", offlineId);
            return $"{{\"response\":\"A\",\"paymentid\":\"{offlineId}\",\"authcode\":\"MOCKAUTH\",\"amount\":\"{amount:F2}\",\"invoice\":\"{appId}\"}}";
        }

        var form = new Dictionary<string, string>
        {
            ["merchantkey"] = _options.MerchantKey,
            ["apikey"] = _options.ApiKey,
            ["method"] = "card_payment",
            ["custid"] = customerId,
            ["amount"] = amount.ToString("F2"),
            ["invoice"] = appId
        };

        _logger.LogInformation("Charging stored IntelliPay customer {CustomerId} for application {AppId} against {WebApiUrl} amount {Amount}", customerId, appId, _options.WebApiUrl, amount);
        return await PostAsync(_options.WebApiUrl, form, cancellationToken);
    }

    public async Task<string> ReadPaymentDetailsAsync(string paymentId, CancellationToken cancellationToken = default)
    {
        if (_options.UseMock
            || string.IsNullOrWhiteSpace(_options.WebApiUrl)
            || string.IsNullOrWhiteSpace(_options.MerchantKey)
            || string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("IntelliPay WebApiUrl not configured (mock mode). Returning mock payment details for {PaymentId}", paymentId);
            return $"{{\"paymentid\":\"{paymentId}\",\"status\":\"settled\"}}";
        }

        var form = new Dictionary<string, string>
        {
            ["merchantkey"] = _options.MerchantKey,
            ["apikey"] = _options.ApiKey,
            ["method"] = "payment_read",
            ["paymentid"] = paymentId
        };

        _logger.LogInformation("Reading IntelliPay payment details for {PaymentId} against {WebApiUrl}", paymentId, _options.WebApiUrl);
        return await PostAsync(_options.WebApiUrl, form, cancellationToken);
    }

    private async Task<string> PostAsync(string endpoint, IDictionary<string, string> form, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var content = new FormUrlEncodedContent(form);
        using var response = await client.PostAsync(endpoint, content, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
