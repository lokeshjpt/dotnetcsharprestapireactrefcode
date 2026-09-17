namespace PWA.PermitsApi.Domain.Models;

public sealed class PartyInfo
{
    public string? LastName { get; set; }
    public string? FirstName { get; set; }
    public string? AddrStreet { get; set; }
    public string? AddrCity { get; set; }
    public string? AddrState { get; set; }
    public string? AddrZip { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}
