namespace PWA.PermitsApi.Application.DTOs;

/// <summary>
/// Staff edit of the "Applicant Information" section (APPLICATION_INFO applicant + contact columns).
/// </summary>
public sealed record UpdateApplicantInfoRequest
{
    public string? AppBusinessName { get; init; }
    public string? AppFirstName { get; init; }
    public string? AppLastName { get; init; }
    public string? AppEmailAddr { get; init; }
    public string? AppAddrStreet { get; init; }
    public string? AppAddrStreet2 { get; init; }
    public string? AppAddrCity { get; init; }
    public string? AppAddrState { get; init; }
    public string? AppAddrZip { get; init; }
    public string? AppPhone { get; init; }
    public string? AppFax { get; init; }

    public string? ContactFirstName { get; init; }
    public string? ContactLastName { get; init; }
    public string? ContactEmail { get; init; }
    public string? ContactPhone { get; init; }
    public string? ContactCell { get; init; }

    /// <summary>Additional CC notification recipients (APP_EMAIL_CC "Other Emails" rows).</summary>
    public IReadOnlyList<string> EmailCcs { get; init; } = new List<string>();
}
