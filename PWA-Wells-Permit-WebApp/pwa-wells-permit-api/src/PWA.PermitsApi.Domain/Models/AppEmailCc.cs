namespace PWA.PermitsApi.Domain.Models;

public sealed class AppEmailCc
{
    public string AppId { get; set; } = string.Empty;
    public int EmailCcId { get; set; }
    public string EmailAddrCc { get; set; } = string.Empty;
}
