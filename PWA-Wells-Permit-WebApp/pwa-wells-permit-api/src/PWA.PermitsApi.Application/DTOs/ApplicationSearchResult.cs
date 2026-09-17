namespace PWA.PermitsApi.Application.DTOs;

public sealed record ApplicationSearchResult(IReadOnlyList<ApplicationDto> Items, int TotalCount);
