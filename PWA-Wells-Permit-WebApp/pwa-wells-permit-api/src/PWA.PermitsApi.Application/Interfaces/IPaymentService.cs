using PWA.PermitsApi.Application.DTOs;

namespace PWA.PermitsApi.Application.Interfaces;

public interface IPaymentService
{
    Task<PaymentDto?> GetByAppIdAsync(string appId, CancellationToken cancellationToken = default);
    Task<PaymentDto> PreAuthorizeAsync(PreAuthRequest request, CancellationToken cancellationToken = default);
    Task<PaymentDto> ChargeAsync(ChargeRequest request, CancellationToken cancellationToken = default);

    /// <summary>Staff edit of the payment method + fine amount (Approval Wizard "Update Payment"); preserves vault/auth data.</summary>
    Task<PaymentDto> UpdateDetailsAsync(string appId, UpdatePaymentRequest request, string updatedBy, CancellationToken cancellationToken = default);
}
