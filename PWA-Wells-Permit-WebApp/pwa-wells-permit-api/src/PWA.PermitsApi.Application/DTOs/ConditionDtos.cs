namespace PWA.PermitsApi.Application.DTOs;

/// <summary>A single permit condition selection for a work (maps to an APP_WORK_CONDITIONS row).</summary>
public sealed record ConditionSelectionDto(string ConditionType, string? OtherDesc);

/// <summary>
/// Permit-conditions state for a single work of an application. Mirrors the legacy intra
/// <c>proc_work_conditions.jsp</c>, which edits conditions <b>per work</b>: it carries the
/// work-type-scoped condition master list to render as checkboxes (<see cref="Available"/>), the
/// conditions currently applied to THIS work (<see cref="Selected"/>, empty until the reviewer checks
/// and saves some), and the work's own status so the wizard can show each work's Pending Conditions /
/// Pending Approval state independently.
/// </summary>
/// <param name="WorkId">The APP_WORKS work_id this panel edits.</param>
/// <param name="WorkLabel">Display label ("Category - Type - Well Use") for the work.</param>
/// <param name="StatusCode">The work's own status_code (e.g. <c>PENDC</c>, <c>PEND</c>).</param>
public sealed record WorkConditionsDto(
    int WorkId,
    string WorkLabel,
    string StatusCode,
    IReadOnlyList<ReferenceItemDto> Available,
    IReadOnlyList<ConditionSelectionDto> Selected);

/// <summary>
/// Per-work permit-conditions payload for the Approval Wizard (one entry per work of the application,
/// so an application with multiple works requesting a permit shows one condition set per work).
/// </summary>
public sealed record ApplicationConditionsDto(IReadOnlyList<WorkConditionsDto> Works);

/// <summary>Request to replace the set of conditions applied to a single work.</summary>
public sealed record UpdateWorkConditionsRequest
{
    public IReadOnlyList<ConditionSelectionDto> Conditions { get; init; } = new List<ConditionSelectionDto>();

    /// <summary>
    /// Legacy "No Specials" path (the <c>proc_work_conditions.jsp</c> "No Specials" button): advance the
    /// work from Pending Conditions (<c>PENDC</c>) to Pending Approval (<c>PEND</c>) even when no
    /// conditions are supplied. When false, a work left with zero conditions stays <c>PENDC</c>.
    /// </summary>
    public bool NoSpecials { get; init; }
}

/// <summary>A raw APP_WORK_CONDITIONS row tagged with its owning work, used to group conditions per work.</summary>
public sealed record WorkConditionRowDto(int WorkId, string ConditionType, string? OtherDesc);

/// <summary>A work of an application with the display label and status needed to render its conditions panel.</summary>
public sealed record WorkForConditionsDto(int WorkId, string WorkCategory, string WorkType, string WorkLabel, string StatusCode);
