namespace PWA.PermitsApi.Domain.Models;

public sealed class WorkPermit
{
    public string PermitNumber { get; set; } = string.Empty;
    public string AppId { get; set; } = string.Empty;
    public int WorkId { get; set; }
    public int WorkSpecsId { get; set; }
    public DateTime? PermitIssuedDate { get; set; }
    public DateTime? PermitExpireDate { get; set; }
    public string StatusCode { get; set; } = string.Empty;
}
