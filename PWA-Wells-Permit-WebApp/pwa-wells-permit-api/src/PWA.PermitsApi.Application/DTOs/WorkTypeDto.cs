namespace PWA.PermitsApi.Application.DTOs;

public sealed record WorkTypeDto(string Code, string Label, decimal FeeRate, string FeeUnit, int SiteMax, decimal SiteExtraRate);
