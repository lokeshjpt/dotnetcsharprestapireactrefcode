namespace PWA.PermitsApi.Domain.Models;

public sealed class ApplicationWorkSpec
{
    public int WorkSpecsId { get; set; }
    public int WorkId { get; set; }
    public string AppId { get; set; } = string.Empty;
    public string? OwnerWellNum { get; set; }
    public int? DrillCount { get; set; }
    public decimal? HoleDiamIn { get; set; }
    public decimal? CasingDiamIn { get; set; }
    public decimal? SealDepthFt { get; set; }
    public decimal? MaxDepthFt { get; set; }
    public string? Latitude { get; set; }
    public string? Longitude { get; set; }
    public string? StateWellId { get; set; }
    public string? DwrNum { get; set; }
    public string? PermitNum { get; set; }

    // Well Completion Report (WCR / legacy "DWR") + geotechnical log fields entered by staff after
    // approval from the "Enter WCR" / "Enter GeoLog" screens.
    public string? ComplWellDwrNum { get; set; }
    public string? DwrNumShared { get; set; }
    public string? DwrImage { get; set; }
    public string? GeologFile { get; set; }

    public string StatusCode { get; set; } = string.Empty;
}
