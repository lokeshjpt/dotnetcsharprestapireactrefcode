namespace PWA.PermitsApi.Application.DTOs;

public sealed record SubmitDocumentRequest
{
    public string? FileName { get; init; }
    public long? FileSize { get; init; }
    public string? ContentType { get; init; }
    public string? DocumentType { get; init; }
}
