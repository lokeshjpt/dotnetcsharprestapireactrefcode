using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using PWA.PermitsApi.Application.DTOs;
using PWA.PermitsApi.Application.Interfaces;
using PWA.PermitsApi.Application.Interfaces.Integration;

namespace PWA.PermitsApi.WebApi.Controllers;

[ApiController]
[Route("api/payment")]
public sealed class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IIntelliPayGateway _intelliPayGateway;

    public PaymentController(IPaymentService paymentService, IIntelliPayGateway intelliPayGateway)
    {
        _paymentService = paymentService;
        _intelliPayGateway = intelliPayGateway;
    }

    /// <summary>
    /// Returns the IntelliPay lightbox terminal script/style block to embed on the CC verify step.
    /// The cardholder enters the card in the lightbox popup which vaults for $0 client-side.
    /// </summary>
    [HttpGet("lightbox")]
    public async Task<ActionResult<object>> Lightbox(CancellationToken cancellationToken)
    {
        var scripts = await _intelliPayGateway.FetchLightboxScriptsAsync(cancellationToken);
        return Ok(new { scripts });
    }

    [HttpPost("preauth")]
    public async Task<ActionResult<PaymentDto>> PreAuthorize([FromBody] PreAuthRequest request, CancellationToken cancellationToken)
    {
        var payment = await _paymentService.PreAuthorizeAsync(request, cancellationToken);
        return Ok(payment);
    }

    [HttpPost("charge")]
    [Authorize]
    public async Task<ActionResult<PaymentDto>> Charge([FromBody] ChargeRequest request, CancellationToken cancellationToken)
    {
        // Charging is an intra staff action — record the signed-in Entra identity (UPN/email).
        var approvedBy = User.FindFirst(ClaimTypes.Name)?.Value;
        if (!string.IsNullOrWhiteSpace(approvedBy))
        {
            request = request with { ApprovedBy = approvedBy };
        }

        var payment = await _paymentService.ChargeAsync(request, cancellationToken);
        return Ok(payment);
    }

    [HttpGet("{appId}")]
    public async Task<ActionResult<PaymentDto>> GetByApplication(string appId, CancellationToken cancellationToken)
    {
        var payment = await _paymentService.GetByAppIdAsync(appId, cancellationToken);
        return payment is null ? NotFound() : Ok(payment);
    }

    [HttpPut("{appId}")]
    [Authorize]
    public async Task<ActionResult<PaymentDto>> UpdateDetails(string appId, [FromBody] UpdatePaymentRequest request, CancellationToken cancellationToken)
    {
        // Payment edits are an intra staff action — record the signed-in Entra identity for the audit column.
        var updatedBy = User.FindFirst(ClaimTypes.Name)?.Value;
        updatedBy = string.IsNullOrWhiteSpace(updatedBy) ? "intra-staff" : (updatedBy.Length > 100 ? updatedBy[..100] : updatedBy);
        try
        {
            var payment = await _paymentService.UpdateDetailsAsync(appId, request, updatedBy, cancellationToken);
            return Ok(payment);
        }
        catch (ArgumentException ex)
        {
            // Field/amount validation failure (e.g. amount received ≠ due, missing payer name).
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // Payment is locked after approval — surface as 409 Conflict rather than a 500.
            return Conflict(new { message = ex.Message });
        }
    }
}
