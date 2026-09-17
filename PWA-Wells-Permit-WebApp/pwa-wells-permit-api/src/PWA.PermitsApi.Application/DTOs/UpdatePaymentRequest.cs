namespace PWA.PermitsApi.Application.DTOs;

/// <summary>
/// Staff edit of the payment record from the Approval Wizard "Update Payment" action: sets the
/// payment method and any fine amount included. The vaulted card / auth data is preserved.
///
/// For non-card methods the staff also records the amount received and payer details, mirroring the
/// legacy intra UpdateAppServlet (proc=payu) form:
///   CHECK  — <see cref="CheckNum"/> (Check Number Received), <see cref="AcctName"/> (Name on
///            Account) and <see cref="PaidAmount"/> (Check Amount Received).
///   CASH   — <see cref="AcctName"/> (Name of Payer) and <see cref="PaidAmount"/> (Cash Amount
///            Received).
///   EXMPT  — none (fee waived).
/// </summary>
public sealed record UpdatePaymentRequest
{
    public string PaymentType { get; init; } = string.Empty;
    public decimal? FineAmount { get; init; }

    /// <summary>Check number received (CHECK only). Numeric, up to 20 chars.</summary>
    public string? CheckNum { get; init; }

    /// <summary>Name on Account (CHECK) / Name of Payer (CASH). Up to 50 chars.</summary>
    public string? AcctName { get; init; }

    /// <summary>Amount received for CHECK/CASH; must equal the amount due.</summary>
    public decimal? PaidAmount { get; init; }
}
