namespace PWA.PermitsApi.Application.DTOs;

public sealed record ChargeRequest
{
    public string AppId { get; init; } = string.Empty;
    public decimal CaptureAmount { get; init; }
    public string ApprovedBy { get; init; } = string.Empty;

    // Optional fine/adjustment entered by staff on the approval wizard. When provided it is persisted
    // to fine_amount and folded into the recalculated charge, so the fine is applied even when staff
    // did not click the separate "Update Payment" button first (legacy proc_approval submits the fine
    // together with the approval). Null means "leave the stored fine unchanged".
    public decimal? FineAmount { get; init; }
}
