namespace PWA.PermitsApi.Application.DTOs;

/// <summary>
/// Staff "Add Work" on the application detail page (legacy UpdateAppServlet proc=wrktp → upd_work_info.jsp
/// → proc=wrku). A new APP_WORKS row is created from a work category + work type; the fee columns
/// (rate/unit/site max) are seeded from the WORK_TYPES lookup, the row starts at status PENDC, and the
/// driller / drilling-method / well-use details collected on the work-info form are persisted with it.
/// </summary>
public sealed record AddWorkRequest
{
    public string WorkCategory { get; init; } = string.Empty;
    public string WorkType { get; init; } = string.Empty;
    public string? WellUseType { get; init; }
    public string? DrillerName { get; init; }
    public string? DrillerLicenseNum { get; init; }
    public string? DrillMethodType { get; init; }
    public string? DrillMethodOtherDesc { get; init; }

    /// <summary>
    /// The wells captured on the "Add Work" form. Each becomes an APP_WORK_SPECS row (ids assigned
    /// sequentially). May be empty (the reviewer can add wells later via Edit).
    /// </summary>
    public IReadOnlyList<UpdateWorkSpecRequest> Specs { get; init; } = new List<UpdateWorkSpecRequest>();
}
