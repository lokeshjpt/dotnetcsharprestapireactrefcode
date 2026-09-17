namespace PWA.PermitsApi.Application.DTOs;

/// <summary>
/// A staff-facing row on the "Upload Documents" screen: an APP_DOCUMENT_LINKS record joined to
/// DOCUMENT_TYPES for the human-readable type description, plus the audit columns. Mirrors the
/// legacy intra BeanAppDocuments list (retrieveDocumentLinksByApp).
/// </summary>
public sealed record AppDocumentDto(
    int SeqNum,
    string? DocumentType,
    string? DocumentDesc,
    string? OtherTypeDesc,
    string? FileName,
    string? AddBy,
    DateTime? AddTs);

/// <summary>
/// A staff-facing row on the "View/Add Notes" screen: an INSPECTION_NOTES record. Mirrors the legacy
/// intra BeanInspectionNotes list, newest first.
/// </summary>
public sealed record AppNoteDto(
    int SeqNum,
    string? NotesText,
    string? AddBy,
    DateTime? AddTs);

/// <summary>Request body for adding a free-text note to an application (INSPECTION_NOTES).</summary>
public sealed record AddNoteRequest(string? NotesText);
