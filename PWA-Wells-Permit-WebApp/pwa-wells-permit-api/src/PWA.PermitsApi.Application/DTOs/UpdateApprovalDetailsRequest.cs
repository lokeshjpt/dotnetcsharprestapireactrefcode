namespace PWA.PermitsApi.Application.DTOs;

/// <summary>
/// Staff edit of the approval-workflow columns on APPLICATION_INFO from the Approval Wizard:
/// the site-visit type (INSPECT / REVIEW) and the received site-map date. Either field may be
/// null/omitted; only the provided fields are meaningful per call from the relevant wizard step.
/// </summary>
public sealed record UpdateApprovalDetailsRequest
{
    public string? SiteVisitType { get; init; }
    public DateTime? SitemapReceivedDate { get; init; }
}
