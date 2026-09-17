namespace PWA.PermitsApi.Application.DTOs;

public sealed record ApprovalResult(string AppId, bool Success, string Message, IReadOnlyList<string> PermitNumbers);
