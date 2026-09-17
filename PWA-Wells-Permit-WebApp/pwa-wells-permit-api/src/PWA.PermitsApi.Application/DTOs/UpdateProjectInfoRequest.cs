namespace PWA.PermitsApi.Application.DTOs;

/// <summary>
/// Staff edit of the "Project Information" section (APPLICATION_INFO project/site + owner + client columns).
/// </summary>
public sealed record UpdateProjectInfoRequest
{
    public string? SiteCityCode { get; init; }
    public string? SiteLocation { get; init; }
    public string? SiteLat { get; init; }
    public string? SiteLong { get; init; }
    public DateTime? ProjStartDate { get; init; }
    public DateTime? ProjEndDate { get; init; }

    public string? OwnerFirstName { get; init; }
    public string? OwnerLastName { get; init; }
    public string? OwnerAddrStreet { get; init; }
    public string? OwnerAddrCity { get; init; }
    public string? OwnerAddrState { get; init; }
    public string? OwnerAddrZip { get; init; }
    public string? OwnerPhone { get; init; }
    public string? OwnerEmail { get; init; }

    public string? ClientFirstName { get; init; }
    public string? ClientLastName { get; init; }
    public string? ClientAddrStreet { get; init; }
    public string? ClientAddrCity { get; init; }
    public string? ClientAddrState { get; init; }
    public string? ClientAddrZip { get; init; }
    public string? ClientPhone { get; init; }
    public string? ClientEmail { get; init; }
}
