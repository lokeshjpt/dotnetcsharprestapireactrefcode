namespace PWA.PermitsApi.Domain.Models;

/// <summary>
/// Maps EEAOWN.X_INSPECTION_WORKBOOK (parent) plus the minimal child rows the intra app fills in
/// during a field inspection. Only the columns required for read/insert coverage are modelled here.
/// </summary>
public sealed class InspectionWorkbook
{
    // X_INSPECTION_WORKBOOK
    public string ApplicationId { get; set; } = string.Empty;
    public string? StatusCode { get; set; }
    public string? FinalApprovalBy { get; set; }
    public DateTime? FinalApprovalDate { get; set; }
    public string? AddBy { get; set; }
    public DateTime? AddTs { get; set; }

    public IList<InspectionWorkbookBoring> Borings { get; set; } = new List<InspectionWorkbookBoring>();
    public IList<InspectionWorkbookConstruction> Constructions { get; set; } = new List<InspectionWorkbookConstruction>();
    public IList<InspectionWorkbookDestruction> Destructions { get; set; } = new List<InspectionWorkbookDestruction>();
    public IList<InspectionWorkbookMonitoring> Monitorings { get; set; } = new List<InspectionWorkbookMonitoring>();
    public IList<InspectionWorkbookMonWellMeasure> MonWellMeasures { get; set; } = new List<InspectionWorkbookMonWellMeasure>();
}

/// <summary>Maps EEAOWN.X_INSPECTION_WORKBOOK_BORING.</summary>
public sealed class InspectionWorkbookBoring
{
    public string ApplicationId { get; set; } = string.Empty;
    public int WorkId { get; set; }
    public int WorkSpecsId { get; set; }
    public int WorkbookId { get; set; }
    public string? BorParcelUse { get; set; }
    public string? BorParcelUseOtherDesc { get; set; }
    public int? BorNumberOfBorings { get; set; }
    public decimal? BorBoreholeDiameterIn { get; set; }
    public decimal? BorBoringDepthFt { get; set; }
    public decimal? BorSealDepthFt { get; set; }
    public decimal? BorStaticWaterLevelFt { get; set; }
    public string? BorSealMaterialQuantityMix { get; set; }
}

/// <summary>Maps EEAOWN.X_INSPECTION_WORKBOOK_CONSTRUCTION (minimal columns).</summary>
public sealed class InspectionWorkbookConstruction
{
    public string ApplicationId { get; set; } = string.Empty;
    public int WorkId { get; set; }
    public int WorkSpecsId { get; set; }
    public int WorkbookId { get; set; }
    public int ConSeqId { get; set; }
    public string? ConSmallWaterSystemType { get; set; }
    public decimal? ConDepthFt { get; set; }
    public decimal? ConBoreholeDiameterIn { get; set; }
    public decimal? ConWellCasingDiameterIn { get; set; }
    public decimal? ConAnnularSealDepthFt { get; set; }
    public decimal? ConStaticWaterLevelFt { get; set; }
    public string? ConAnnularSealMaterial { get; set; }
}

/// <summary>Maps EEAOWN.X_INSPECTION_WORKBOOK_DESTRUCTION (minimal columns).</summary>
public sealed class InspectionWorkbookDestruction
{
    public string ApplicationId { get; set; } = string.Empty;
    public int WorkId { get; set; }
    public int WorkSpecsId { get; set; }
    public int WorkbookId { get; set; }
    public int DesSeqId { get; set; }
    public int? DesNumberOfWells { get; set; }
    public string? DesMethod { get; set; }
    public decimal? DesWellDiameterFt { get; set; }
    public decimal? DesWellDepthFt { get; set; }
    public decimal? DesSealDepthFt { get; set; }
    public string? DesSealingMaterial { get; set; }
}

/// <summary>Maps EEAOWN.X_INSPECTION_WORKBOOK_MONITORING (minimal columns).</summary>
public sealed class InspectionWorkbookMonitoring
{
    public string ApplicationId { get; set; } = string.Empty;
    public int WorkId { get; set; }
    public int WorkSpecsId { get; set; }
    public int WorkbookId { get; set; }
    public decimal? MonNumberOfWells { get; set; }
    public decimal? MonWellDepthFt { get; set; }
    public int? MonWellsDurationMo { get; set; }
    public int? MonWellsDurationYr { get; set; }
    public decimal? MonSealDepthFt { get; set; }
    public decimal? MonStaticWaterLevelFt { get; set; }
    public string? MonSealMaterialQuantityMix { get; set; }
}

/// <summary>Maps EEAOWN.X_INSPECTION_WORKBOOK_MON_WELL_MEASURES.</summary>
public sealed class InspectionWorkbookMonWellMeasure
{
    public string ApplicationId { get; set; } = string.Empty;
    public int WorkbookId { get; set; }
    public int SeqId { get; set; }
    public int? NumberOfWells { get; set; }
    public decimal? WellDepth { get; set; }
}
