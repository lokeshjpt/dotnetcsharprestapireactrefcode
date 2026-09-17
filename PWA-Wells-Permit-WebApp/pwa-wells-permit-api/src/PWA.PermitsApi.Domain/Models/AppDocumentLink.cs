namespace PWA.PermitsApi.Domain.Models;

public sealed class AppDocumentLink
{
    public string AppId { get; set; } = string.Empty;
    public int SeqNum { get; set; }
    public string? DocumentType { get; set; }
    public string? OtherTypeDesc { get; set; }
    public string? DocumentFilename { get; set; }
}
