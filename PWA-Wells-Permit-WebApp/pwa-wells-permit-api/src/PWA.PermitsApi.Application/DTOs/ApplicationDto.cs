namespace PWA.PermitsApi.Application.DTOs;

public sealed record ApplicationDto
{
    public string AppId { get; init; } = string.Empty;
    public string StatusCode { get; init; } = string.Empty;
    public DateTime AddDate { get; init; }
    public string AddBy { get; init; } = string.Empty;

    // Project / site
    public string? SiteCityCode { get; init; }
    public string? SiteCityName { get; init; }
    public string? SiteLocation { get; init; }
    public string? SiteLat { get; init; }
    public string? SiteLong { get; init; }
    public DateTime? ProjStartDate { get; init; }
    public DateTime? ProjEndDate { get; init; }
    public string? SiteHazardRequired { get; init; }
    public string? SiteVisitType { get; init; }
    public string? SitemapFilename { get; init; }
    public DateTime? SitemapReceivedDate { get; init; }

    // Post-approval permit extensions (APPLICATION_INFO.extend_*)
    public DateTime? ExtendStartDate { get; init; }
    public DateTime? ExtendEndDate { get; init; }
    public int? ExtendCount { get; init; }
    public string? ExtendBy { get; init; }

    // Applicant
    public string? AppBusinessName { get; init; }
    public string? AppLastName { get; init; }
    public string? AppFirstName { get; init; }
    public string? AppEmailAddr { get; init; }
    public string? AppAddrStreet { get; init; }
    public string? AppAddrStreet2 { get; init; }
    public string? AppAddrCity { get; init; }
    public string? AppAddrState { get; init; }
    public string? AppAddrZip { get; init; }
    public string? AppPhone { get; init; }
    public string? AppFax { get; init; }

    // Contact
    public string? ContactLastName { get; init; }
    public string? ContactFirstName { get; init; }
    public string? ContactEmail { get; init; }
    public string? ContactPhone { get; init; }
    public string? ContactCell { get; init; }

    // Owner
    public string? OwnerLastName { get; init; }
    public string? OwnerFirstName { get; init; }
    public string? OwnerAddrStreet { get; init; }
    public string? OwnerAddrCity { get; init; }
    public string? OwnerAddrState { get; init; }
    public string? OwnerAddrZip { get; init; }
    public string? OwnerPhone { get; init; }
    public string? OwnerEmail { get; init; }

    // Client
    public string? ClientLastName { get; init; }
    public string? ClientFirstName { get; init; }
    public string? ClientAddrStreet { get; init; }
    public string? ClientAddrCity { get; init; }
    public string? ClientAddrState { get; init; }
    public string? ClientAddrZip { get; init; }
    public string? ClientPhone { get; init; }
    public string? ClientEmail { get; init; }

    public IReadOnlyList<ApplicationWorkDto> Works { get; init; } = new List<ApplicationWorkDto>();
    public IReadOnlyList<string> WorkTypes { get; init; } = new List<string>();

    // Hazardous-materials sub-form (null when none captured)
    public HazardDto? Hazard { get; init; }

    // Uploaded document metadata (APP_DOCUMENT_LINKS)
    public IReadOnlyList<DocumentLinkDto> Documents { get; init; } = new List<DocumentLinkDto>();

    // Extra notification recipients (APP_EMAIL_CC)
    public IReadOnlyList<string> EmailCcs { get; init; } = new List<string>();

    // Free-text notes (APP_NOTES)
    public IReadOnlyList<string> Notes { get; init; } = new List<string>();
}
