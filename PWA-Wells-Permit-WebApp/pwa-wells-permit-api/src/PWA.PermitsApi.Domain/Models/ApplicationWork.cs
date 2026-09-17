namespace PWA.PermitsApi.Domain.Models;

public sealed class ApplicationWork
{
    public int WorkId { get; set; }
    public string AppId { get; set; } = string.Empty;
    public string WorkCategory { get; set; } = string.Empty;
    public string WorkType { get; set; } = string.Empty;
    public string? WellUseType { get; set; }
    public string? WorkCategoryDesc { get; set; }
    public string? WorkTypeDesc { get; set; }
    public string? WellUseDesc { get; set; }
    public string? DrillMethodName { get; set; }
    public string? DrillerName { get; set; }
    public string? DrillerLicenseNum { get; set; }
    public string? DrillMethodType { get; set; }
    public string? DrillMethodOtherDesc { get; set; }
    public decimal? WorkFeeRate { get; set; }
    public string? WorkFeeUnit { get; set; }
    public int? WorkSiteMax { get; set; }
    public decimal? WorkSiteExtraRate { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public IList<ApplicationWorkSpec> Specs { get; set; } = new List<ApplicationWorkSpec>();
}
