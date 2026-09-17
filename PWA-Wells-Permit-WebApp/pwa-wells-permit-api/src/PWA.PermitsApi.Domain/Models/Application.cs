namespace PWA.PermitsApi.Domain.Models;

public sealed class Application
{
    public string AppId { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public DateTime AddDate { get; set; }
    public string AddBy { get; set; } = string.Empty;

    // Project / site
    public string? SiteCityCode { get; set; }
    public string? SiteCityName { get; set; }
    public string? SiteLocation { get; set; }
    public string? SiteLat { get; set; }
    public string? SiteLong { get; set; }
    public DateTime? ProjStartDate { get; set; }
    public DateTime? ProjEndDate { get; set; }
    public string? SiteHazardRequired { get; set; }
    public string? SiteVisitType { get; set; }
    public string? SitemapFilename { get; set; }
    public DateTime? SitemapReceivedDate { get; set; }

    // Post-approval permit extensions (APPLICATION_INFO.extend_*)
    public DateTime? ExtendStartDate { get; set; }
    public DateTime? ExtendEndDate { get; set; }
    public int? ExtendCount { get; set; }
    public string? ExtendBy { get; set; }

    // Parties
    public Applicant Applicant { get; set; } = new();
    public ContactInfo Contact { get; set; } = new();
    public PartyInfo Owner { get; set; } = new();
    public PartyInfo Client { get; set; } = new();

    public IList<ApplicationWork> Works { get; set; } = new List<ApplicationWork>();

    // Hazardous-materials sub-form (APP_HAZARD_INFO + child tables). Null when not provided.
    public HazardInfo? Hazard { get; set; }

    // Uploaded document metadata (APP_DOCUMENT_LINKS)
    public IList<AppDocumentLink> Documents { get; set; } = new List<AppDocumentLink>();

    // Extra notification recipients (APP_EMAIL_CC)
    public IList<AppEmailCc> EmailCcs { get; set; } = new List<AppEmailCc>();

    // Free-text notes (APP_NOTES)
    public IList<AppNote> Notes { get; set; } = new List<AppNote>();
}
