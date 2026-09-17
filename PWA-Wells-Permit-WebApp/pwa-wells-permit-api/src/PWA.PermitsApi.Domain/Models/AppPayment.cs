namespace PWA.PermitsApi.Domain.Models;

public sealed class AppPayment
{
    public string AppId { get; set; } = string.Empty;
    public string PaymentType { get; set; } = string.Empty;
    public string? AuthIdEncr { get; set; }

    // Plaintext IntelliPay paymentid; persisted encrypted (EncryptByPassPhrase) into the
    // payment_id VARBINARY column, keyed on application_id — same pattern as auth_id_encr.
    public string? PaymentId { get; set; }

    // Plaintext IntelliPay authcode; persisted encrypted into the pn_ref_num_encr column.
    public string? PnRefNum { get; set; }

    // Sequential WR<YEAR>-XXXX receipt number (plaintext), assigned at charge/approval time.
    public string? ReceiptNum { get; set; }

    // Decrypted payment_history JSON array (read side only); entries are appended via
    // IPaymentRepository.AppendPaymentHistoryAsync, never written wholesale from here.
    public string? PaymentHistory { get; set; }

    public DateTime? PaidDate { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal FineAmount { get; set; }
    public decimal ServiceCharge { get; set; }
    public decimal AuthAmount { get; set; }
    public string? CheckNum { get; set; }
    public string? AcctName { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string AddBy { get; set; } = "public-portal";
}
