namespace PWA.PermitsApi.Application.DTOs;

public sealed record ApplicationSearchRequest
{
    public string? AppId { get; init; }
    public string? ApplicantLastName { get; init; }
    public string? AppFirstName { get; init; }
    public string? Email { get; init; }
    public string? AppBusinessName { get; init; }
    public string? DrillerName { get; init; }
    public string? StatusCode { get; init; }
    public string? SiteCityName { get; init; }
    public DateTime? AddedAfter { get; init; }
    public DateTime? AddedBefore { get; init; }

    // Legacy "Search for Applications Submitted after May 2005" parity filters (search_form.jsp).
    // All optional; each is only applied when supplied so existing callers (ecomm Track, queue
    // lists) are unaffected. Permit/work filters resolve via EXISTS subqueries on the child tables.
    public string? PermitNum { get; init; }
    public DateTime? AppAddedOn { get; init; }         // exact "Date Applied"
    public int? AppYear { get; init; }                 // "Year Applied"
    public int? PermitYear { get; init; }              // "Permit Issued Year"
    public DateTime? PermitIssuedFrom { get; init; }
    public DateTime? PermitIssuedTo { get; init; }
    public string? ProjectLocation { get; init; }
    public string? WorkCategory { get; init; }
    public string? WorkType { get; init; }
    public string? DrillerLicense { get; init; }

    // "Approved Permits" processing list (process_paid_list.jsp) — approved-date range filters on
    // APP_PAYMENT_INFO.update_ts, distinct from the "Date Applied" range above.
    public DateTime? ApprovedFrom { get; init; }
    public DateTime? ApprovedTo { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;

    // Server-side sort. SortBy is a whitelisted key (see ApplicationRepository); unknown keys fall
    // back to the newest-first default. SortDir is "asc" or "desc".
    public string? SortBy { get; init; }
    public string? SortDir { get; init; }
}
