using PWA.PermitsApi.Domain.Models;

namespace PWA.PermitsApi.Application.Interfaces.Repositories;

public interface IWorkPermitRepository
{
    Task<IReadOnlyList<WorkPermit>> GeneratePermitsAsync(string appId, DateTime permitExpiryDate, CancellationToken cancellationToken = default);
}
