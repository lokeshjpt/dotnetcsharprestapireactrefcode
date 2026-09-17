namespace PWA.PermitsApi.Application.DTOs;

public sealed record PreAuthRequest
{
    public string AppId { get; init; } = string.Empty;
    public string CustomerId { get; init; } = string.Empty;
    public string PaymentType { get; init; } = "CC";
    public decimal AuthorizedAmount { get; init; }
    public string RequestedBy { get; init; } = "public-portal";

    /// <summary>
    /// Whether to send the applicant "Application Confirmation" email once the card is vaulted.
    /// True for the ecomm applicant submission (the vault is the real submit); false for the intra
    /// staff "change card" re-vault, where the application was already submitted/confirmed and the
    /// applicant must not be emailed again. Defaults to true so the public portal is unchanged.
    /// </summary>
    public bool SendConfirmationEmail { get; init; } = true;

    /// <summary>
    /// PCI-safe snapshot of the IntelliPay lightbox vault response, captured for payment_history at
    /// submit (mirrors the legacy Java ecomm ProcessAppServlet snapshot). PCI-sensitive fields
    /// (nonce, hmac, methodhint, cardnumdisplay) are never accepted or stored.
    /// </summary>
    public IntelliPayVaultSnapshot? Response { get; init; }
}

public sealed record IntelliPayVaultSnapshot
{
    public string? Call { get; init; }
    public string? Status { get; init; }
    public string? AuthCode { get; init; }
    public string? CardBrand { get; init; }
    public string? DeclineReason { get; init; }
    public string? Amount { get; init; }
    public string? Fee { get; init; }
}
