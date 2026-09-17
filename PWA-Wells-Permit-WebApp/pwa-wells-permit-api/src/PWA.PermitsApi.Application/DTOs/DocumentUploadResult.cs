namespace PWA.PermitsApi.Application.DTOs;

/// <summary>Result of a document upload into APP_DOCUMENT_LINKS.</summary>
public sealed record DocumentUploadResult(int SeqNum, string FileName, string DocumentType);
