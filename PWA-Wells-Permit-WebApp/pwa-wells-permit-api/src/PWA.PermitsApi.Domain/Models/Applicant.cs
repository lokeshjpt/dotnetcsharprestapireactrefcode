namespace PWA.PermitsApi.Domain.Models;

public sealed class Applicant
{
    public string? AppBusinessName { get; set; }
    public string? AppLastName { get; set; }
    public string? AppFirstName { get; set; }
    public string? AppEmailAddr { get; set; }
    public string? AppAddrStreet { get; set; }
    public string? AppAddrStreet2 { get; set; }
    public string? AppAddrCity { get; set; }
    public string? AppAddrState { get; set; }
    public string? AppAddrZip { get; set; }
    public string? AppPhone { get; set; }
    public string? AppFax { get; set; }
}
