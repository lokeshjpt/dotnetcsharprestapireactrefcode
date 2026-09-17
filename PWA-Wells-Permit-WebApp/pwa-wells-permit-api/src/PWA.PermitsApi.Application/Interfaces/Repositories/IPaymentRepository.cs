using PWA.PermitsApi.Domain.Models;

namespace PWA.PermitsApi.Application.Interfaces.Repositories;

public interface IPaymentRepository
{
    Task<AppPayment?> GetByAppIdAsync(string appId, CancellationToken cancellationToken = default);
    Task<AppPayment> UpsertAsync(AppPayment payment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the next sequential receipt number (WR&lt;YEAR&gt;-XXXX) for the current year, based on
    /// the max receipt_num already assigned this year. Mirrors legacy getNextReceipt/getMaxReceiptNum.
    /// </summary>
    Task<string> GetNextReceiptAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends a single PCI-stripped JSON entry to the encrypted payment_history array (oldest-first),
    /// creating the array if it does not yet exist. Mirrors legacy BeanAppPay.updatePaymentHistory.
    /// </summary>
    Task AppendPaymentHistoryAsync(string appId, string entryJson, CancellationToken cancellationToken = default);
}
