using PWA.PermitsApi.Application.DTOs;

namespace PWA.PermitsApi.Application.Interfaces.Repositories;

public interface IConditionsRepository
{
    /// <summary>
    /// Returns every work of the application (work_id, category/type, display label and its own
    /// status_code), ordered by work_id — one row per work requesting a permit.
    /// </summary>
    Task<IReadOnlyList<WorkForConditionsDto>> GetWorksAsync(string appId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every APP_WORK_CONDITIONS row for the application tagged with its owning work_id
    /// (ordered by work_id, cond_id) so the caller can group the applied conditions per work.
    /// </summary>
    Task<IReadOnlyList<WorkConditionRowDto>> GetAppliedConditionsAsync(string appId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the condition types configured for a work category/type (WORK_CONDITION_TYPES joined to
    /// CONDITION_TYPES) — the work-type-scoped master list the legacy retrieveWorkCondByWtype builds.
    /// </summary>
    Task<IReadOnlyList<ReferenceItemDto>> GetWorkConditionTypesAsync(string workCategory, string workType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the conditions applied to a single work: deletes that work's APP_WORK_CONDITIONS rows
    /// and inserts the supplied set with a sequential cond_id. Then mirrors the legacy per-work status
    /// transition — a work still at Pending Conditions (<c>PENDC</c>) is advanced to Pending Approval
    /// (<c>PEND</c>) when it now has at least one condition or <paramref name="noSpecials"/> is set.
    /// Runs in a single transaction.
    /// </summary>
    Task ReplaceWorkConditionsAsync(string appId, int workId, IReadOnlyList<ConditionSelectionDto> conditions, bool noSpecials, string updatedBy, CancellationToken cancellationToken = default);
}
