namespace PWA.PermitsApi.Application.DTOs;

public sealed record ApprovalRequest
{
    public string ApprovedBy { get; init; } = string.Empty;
    public string? Notes { get; init; }

    /// <summary>
    /// Fine assessed on the approval wizard. Persisted onto the payment record before the approval
    /// email is generated so the attached permit/receipt PDF's total reflects it (for CC apps the
    /// fine is otherwise only stored later, at charge time — after this email is already sent).
    /// </summary>
    public decimal? FineAmount { get; init; }
}
