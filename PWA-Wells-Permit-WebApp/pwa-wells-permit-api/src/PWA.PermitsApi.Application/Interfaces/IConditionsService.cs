using PWA.PermitsApi.Application.DTOs;

namespace PWA.PermitsApi.Application.Interfaces;

public interface IConditionsService
{
    /// <summary>
    /// Returns the per-work permit-conditions payload for the application: one entry per work, each
    /// with its work-type-scoped condition master list, the conditions applied to that work, and the
    /// work's own status.
    /// </summary>
    Task<ApplicationConditionsDto> GetConditionsAsync(string appId, CancellationToken cancellationToken = default);

    /// <summary>Replaces the conditions applied to a single work, then returns the refreshed per-work payload.</summary>
    Task<ApplicationConditionsDto> UpdateWorkConditionsAsync(string appId, int workId, UpdateWorkConditionsRequest request, string updatedBy, CancellationToken cancellationToken = default);
}
