namespace PWA.PermitsApi.Application.Interfaces.Integration;

public interface IIntelliPayGateway
{
    /// <summary>
    /// Ecomm-side $0 vault/pre-authorization against the IntelliPay autoterminal endpoint.
    /// account = applicationId, amount = 0. Leaves the application payment at status PEND — the
    /// real fee is charged later by the intra app. Returns the vaulted custid.
    /// </summary>
    Task<IntelliPayVaultResult> VaultZeroDollarAsync(string applicationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ecomm-side fetch of the IntelliPay lightbox terminal script/style block from the
    /// autoterminal endpoint (POST merchantkey+apikey). The block is embedded in the CC verify
    /// step; the cardholder enters the card inside the lightbox popup which vaults for $0 and
    /// returns the custid client-side. Mirrors the legacy IntelliPayLightboxService.
    /// </summary>
    Task<string> FetchLightboxScriptsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Intra-side real charge (method=card_payment) against the IntelliPay webapi endpoint
    /// (intellipayWebapiUrl), charging the previously-vaulted custid for the actual permit fee.
    /// Returns the RAW JSON response so the caller can extract paymentid/authcode/response and
    /// build the PCI-stripped payment_history entry (mirrors legacy IntelliPayService.chargeStoredCard).
    /// </summary>
    Task<string> ChargeStoredCustomerAsync(string appId, string customerId, decimal amount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Intra-side payment details read (method=payment_read) for a completed charge. Returns the
    /// RAW JSON response, appended (PCI-stripped) to payment_history. Mirrors legacy
    /// IntelliPayService.readPaymentDetails.
    /// </summary>
    Task<string> ReadPaymentDetailsAsync(string paymentId, CancellationToken cancellationToken = default);
}
