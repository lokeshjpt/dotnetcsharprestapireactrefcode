namespace PWA.PermitsApi.Application.DTOs;

public sealed record PaymentDto(
    string AppId,
    string PaymentType,
    string? AuthIdEncr,
    string? PaymentId,
    decimal PaidAmount,
    decimal FineAmount,
    decimal ServiceCharge,
    decimal AuthAmount,
    string? CheckNum,
    string StatusCode,
    string? PnRefNum = null,
    string? ReceiptNum = null,
    DateTime? PaidDate = null,
    string? AcctName = null);
