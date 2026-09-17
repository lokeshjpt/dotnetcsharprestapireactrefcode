namespace PWA.PermitsApi.Application.Configuration;

public sealed class EmailOptions
{
    public string SmtpServer { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 25;
    public string FromAddress { get; set; } = "Janu.Sundaram@alamedacountyca.gov";
    public string AuditEmail { get; set; } = string.Empty;

    /// <summary>
    /// When true (or when <see cref="SmtpServer"/> is absent) mail is logged but never sent,
    /// keeping build/test offline-safe.
    /// </summary>
    public bool UseMock { get; set; }

    /// <summary>
    /// Deployment environment tag (loc/dev/tst/stg/uat/prod). Mirrors the legacy
    /// <c>alco-env</c> system property: when it is a non-production environment the subject line is
    /// prefixed with "TEST - " so staff can tell test mail from production mail. Empty or "prod"
    /// means production (no prefix).
    /// </summary>
    public string EnvironmentName { get; set; } = string.Empty;

    /// <summary>
    /// Public applicant-facing base URL used to build the "Tracking" link in applicant emails.
    /// Legacy built <c>alco-inter-https + "pwapermitsecomm_app/TrackAppServlet?..."</c>; the rewrite
    /// points at the React public app's <c>/track</c> route. Trailing slash optional.
    /// </summary>
    public string PublicAppBaseUrl { get; set; } = "http://localhost:3000";

    /// <summary>Public contact mailbox shown in applicant emails (legacy <c>wells@acpwa.org</c>).</summary>
    public string ContactEmail { get; set; } = "wells@acpwa.org";

    /// <summary>Public program website linked from approval / cancellation emails.</summary>
    public string WebsiteUrl { get; set; } = "https://www.acpwa.org/programs-services/water/well-program.page?";

    /// <summary>
    /// True when the current environment is non-production and outgoing subjects should carry the
    /// "TEST - " prefix (loc/dev/tst/stg/uat).
    /// </summary>
    public bool IsNonProduction
    {
        get
        {
            var env = EnvironmentName?.Trim().ToLowerInvariant();
            return env is "loc" or "dev" or "tst" or "stg" or "uat" or "test" or "development" or "staging";
        }
    }
}
