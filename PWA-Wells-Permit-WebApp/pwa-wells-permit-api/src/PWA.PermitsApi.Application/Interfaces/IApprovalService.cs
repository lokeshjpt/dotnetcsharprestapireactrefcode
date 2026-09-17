using PWA.PermitsApi.Application.DTOs;

namespace PWA.PermitsApi.Application.Interfaces;

public interface IApprovalService
{
    Task<ApprovalResult> ApproveAsync(string appId, ApprovalRequest request, CancellationToken cancellationToken = default);
}
