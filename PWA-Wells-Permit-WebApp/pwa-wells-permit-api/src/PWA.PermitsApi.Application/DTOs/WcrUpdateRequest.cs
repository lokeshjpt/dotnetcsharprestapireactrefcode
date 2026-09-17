namespace PWA.PermitsApi.Application.DTOs;

/// <summary>
/// Staff "Enter WCR" (Well Completion Report, legacy "DWR") data entry for a single
/// "Work Requesting Permit" section. Mirrors the legacy <c>upd_work_specs_dwr.jsp</c> grid:
/// each existing well-spec row (APP_WORK_SPECS) gets the State Well # and WCR #, plus the
/// optional construction Permit # / WCR # for destruction-category work. Only allowed once
/// the application has been approved (status APPRV). Rows are matched by WorkSpecsId.
/// </summary>
public sealed record WcrUpdateRequest
{
    /// <summary>Legacy "Copy WCR Number for all rows" checkbox → DWR_NUM_SHARED (Y/N).</summary>
    public bool DwrNumShared { get; init; }

    public IReadOnlyList<WcrSpecUpdate> Specs { get; init; } = new List<WcrSpecUpdate>();
}

public sealed record WcrSpecUpdate
{
    public int WorkSpecsId { get; init; }

    /// <summary>State Well # → STATE_WELL_ID.</summary>
    public string? StateWellId { get; init; }

    /// <summary>WCR # → COMPL_WELL_DWR_NUM.</summary>
    public string? ComplWellDwrNum { get; init; }

    /// <summary>Construction Permit # → PERMIT_NUM (destruction-category work only).</summary>
    public string? PermitNum { get; init; }

    /// <summary>Construction WCR # → DWR_NUM (destruction-category work only).</summary>
    public string? DwrNum { get; init; }
}
