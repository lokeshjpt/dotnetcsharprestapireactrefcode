namespace PWA.PermitsApi.Application.DTOs;

public sealed record FileUploadResult(string FileName, string RemotePath, bool VirusScanPassed, string Message);
