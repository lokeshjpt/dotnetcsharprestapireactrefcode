using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using PWA.PermitsApi.Application.DTOs;
using PWA.PermitsApi.Application.Interfaces;

namespace PWA.PermitsApi.WebApi.Controllers;

[ApiController]
[Route("api/approval")]
[Authorize]
public sealed class ApprovalController : ControllerBase
{
    private readonly IApprovalService _approvalService;

    public ApprovalController(IApprovalService approvalService)
    {
        _approvalService = approvalService;
    }

    [HttpPost("{appId}/approve")]
    public async Task<ActionResult<ApprovalResult>> Approve(string appId, [FromBody] ApprovalRequest request, CancellationToken cancellationToken)
    {
        // Trust the signed-in Entra identity (full UPN/email) over any value in the body.
        var approvedBy = User.FindFirst(ClaimTypes.Name)?.Value;
        if (!string.IsNullOrWhiteSpace(approvedBy))
        {
            request = request with { ApprovedBy = approvedBy };
        }

        var result = await _approvalService.ApproveAsync(appId, request, cancellationToken);
        return Ok(result);
    }
}
