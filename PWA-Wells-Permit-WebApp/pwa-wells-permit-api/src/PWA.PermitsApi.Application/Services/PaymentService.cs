using Microsoft.Extensions.Logging;
using PWA.PermitsApi.Application.Common;
using PWA.PermitsApi.Application.DTOs;
using PWA.PermitsApi.Application.Interfaces;
using PWA.PermitsApi.Application.Interfaces.Integration;
using PWA.PermitsApi.Application.Interfaces.Repositories;
using PWA.PermitsApi.Domain.Models;

namespace PWA.PermitsApi.Application.Services;

public sealed class PaymentService : IPaymentService
{
    // Application statuses that mean the permit is already approved — once here, staff may no longer
    // change the payment method/fine (mirrors the intra rule that payment is locked after approval).
    private static readonly HashSet<string> ApprovedStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "APPRV", "APRVD", "APPR", "APPROVED" };

    private readonly IPaymentRepository _paymentRepository;
    private readonly IApplicationRepository _applicationRepository;
    private readonly IIntelliPayGateway _intelliPayGateway;
    private readonly IPermitNotificationService _notifications;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(IPaymentRepository paymentRepository, IApplicationRepository applicationRepository, IIntelliPayGateway intelliPayGateway, IPermitNotificationService notifications, ILogger<PaymentService> logger)
    {
        _paymentRepository = paymentRepository;
        _applicationRepository = applicationRepository;
        _intelliPayGateway = intelliPayGateway;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<PaymentDto?> GetByAppIdAsync(string appId, CancellationToken cancellationToken = default)
    {
        var payment = await _paymentRepository.GetByAppIdAsync(appId, cancellationToken);
        return payment is null ? null : Map(payment);
    }

    public async Task<PaymentDto> PreAuthorizeAsync(PreAuthRequest request, CancellationToken cancellationToken = default)
    {
        var payment = await _paymentRepository.GetByAppIdAsync(request.AppId, cancellationToken) ?? new AppPayment
        {
            AppId = request.AppId,
            AddBy = request.RequestedBy
        };

        // CC $0 vault / pre-authorization only. The stored custid is used later by the intra app
        // to charge the real fee. Status stays PEND; paid_amount stays 0. auth_amount MUST remain
        // the real permit fee captured at submit (Java BeanAppPay keeps the fee, not the $0 vault
        // amount), so the intra app charges the correct amount against the vaulted custid.
        payment.PaymentType = request.PaymentType;
        payment.AuthIdEncr = request.CustomerId;
        if (payment.AuthAmount <= 0m)
        {
            payment.AuthAmount = request.AuthorizedAmount;
        }
        payment.PaidAmount = 0m;
        payment.FineAmount = 0m;
        payment.ServiceCharge = 0m;
        // Clear any prior IntelliPay authcode on a fresh vault (legacy updateCCPreAuth nulls
        // pn_ref_num_encr). Empty string signals the repository to null the column.
        payment.PnRefNum = string.Empty;
        payment.StatusCode = "PEND";

        _logger.LogInformation("Persisting pre-authorization (vault) for application {AppId}", request.AppId);
        var saved = await _paymentRepository.UpsertAsync(payment, cancellationToken);

        // Record the PCI-safe IntelliPay vault response as the first payment_history entry, matching
        // the legacy Java ecomm which stores the lightbox response snapshot at submit. Only for CC
        // with a confirmed custid; the row is guaranteed to exist after the upsert above.
        if (string.Equals(request.PaymentType, "CC", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(request.CustomerId))
        {
            var snapshot = IntelliPayJson.BuildVaultSnapshot(
                request.Response?.Call,
                request.CustomerId,
                request.Response?.Status,
                request.Response?.AuthCode,
                request.Response?.Amount,
                request.Response?.Fee ?? (request.AuthorizedAmount > 0m ? request.AuthorizedAmount.ToString("F2") : null),
                "CC",
                request.Response?.CardBrand,
                request.AppId,
                request.Response?.DeclineReason);
            await _paymentRepository.AppendPaymentHistoryAsync(request.AppId, snapshot, cancellationToken);

            // The applicant has now completed the IntelliPay lightbox and the card is vaulted, so this
            // is the point a credit-card application is actually submitted. Send the applicant
            // confirmation here rather than at record creation (ApplicationService defers CC
            // confirmations so no email is sent while the popup is still being filled). Best-effort:
            // a mail failure must never fail the vault the applicant just completed.
            //
            // Suppressed when the vault is an intra staff "change card" re-authorization
            // (SendConfirmationEmail = false): the application was already submitted and confirmed, so
            // re-vaulting a new card must not re-send the applicant confirmation email.
            if (request.SendConfirmationEmail)
            {
                try
                {
                    var confirmApp = await _applicationRepository.GetByIdAsync(request.AppId, cancellationToken);
                    if (confirmApp is not null)
                    {
                        var feeAmount = saved.AuthAmount > 0m ? saved.AuthAmount : request.AuthorizedAmount;
                        await _notifications.SendApplicationConfirmationAsync(confirmApp, feeAmount, "CC", null, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Applicant confirmation email failed for application {AppId} after CC vault", request.AppId);
                    await _notifications.SendSystemExceptionAuditAsync("PaymentService.PreAuthConfirmationEmail", "Application " + request.AppId, ex, cancellationToken);
                }
            }
        }

        return Map(saved);
    }

    public async Task<PaymentDto> ChargeAsync(ChargeRequest request, CancellationToken cancellationToken = default)
    {
        var payment = await _paymentRepository.GetByAppIdAsync(request.AppId, cancellationToken)
            ?? throw new InvalidOperationException($"No payment record was found for application {request.AppId}.");

        // Idempotency: a completed charge has already assigned a receipt and marked the payment PAID.
        // Never charge the gateway (or re-issue a receipt) a second time — the approval flow charges the
        // vaulted card server-side before emailing the permit, and the intra client then issues its own
        // post-approval charge for the same application. Return the finalized record unchanged.
        if (payment.StatusCode == "PAID" && !string.IsNullOrWhiteSpace(payment.ReceiptNum))
        {
            return Map(payment);
        }

        if (payment.PaymentType == "EXMPT")
        {
            payment.StatusCode = "PAID";
            payment.PaidAmount = 0m;
            return Map(await _paymentRepository.UpsertAsync(payment, cancellationToken));
        }

        // Recalculate the fee LIVE from the current works (fee × Σ drill_count per well) plus the
        // service charge and any fine — mirroring legacy chargeStoredCC, which always charges
        // getRecalculatedAppAmt rather than a stored value. This is what makes an added well or a fine
        // entered on the approval wizard actually reflected in the charge. The fine comes from the
        // request when supplied (so it applies even without a separate "Update Payment" click),
        // otherwise the already-stored fine is used.
        var application = await _applicationRepository.GetByIdAsync(request.AppId, cancellationToken);
        var fine = request.FineAmount ?? payment.FineAmount;
        var baseFee = application is not null ? FeeCalculator.BaseFee(application.Works) : payment.AuthAmount;
        var chargeTotal = FeeCalculator.RoundUp2(baseFee + payment.ServiceCharge + fine);
        // Persist the recomputed base + fine up front so they survive both approval and a decline.
        payment.AuthAmount = baseFee;
        payment.FineAmount = fine;

        if (payment.PaymentType == "CC")
        {
            // A vaulted IntelliPay customer id is required only for the real card charge; check/cash
            // reconciliation below needs no gateway credential.
            if (string.IsNullOrWhiteSpace(payment.AuthIdEncr))
            {
                throw new InvalidOperationException("A stored IntelliPay customer id is required before the card can be charged.");
            }

            // Real charge against the vaulted custid; returns the RAW IntelliPay JSON response so we
            // can mirror the legacy chargeStoredCC column writes exactly.
            var chargeResponse = await _intelliPayGateway.ChargeStoredCustomerAsync(request.AppId, payment.AuthIdEncr!, chargeTotal, cancellationToken);

            var approved = IntelliPayJson.ExtractField(chargeResponse, "response");
            if (!string.Equals(approved, "A", StringComparison.OrdinalIgnoreCase))
            {
                // Declined — persist PAYFL (no receipt/paid_date assigned) and surface the reason.
                var reason = IntelliPayJson.ExtractField(chargeResponse, "declinereason");
                payment.StatusCode = "PAYFL";
                await _paymentRepository.UpsertAsync(payment, cancellationToken);
                _logger.LogWarning("IntelliPay declined charge for application {AppId}: {Reason}", request.AppId, reason);

                // Audit the failed charge and notify the applicant of the decline (both best-effort).
                await _notifications.SendCcChargeAuditAsync(request.AppId, chargeTotal.ToString("F2"), success: false, failReason: reason, paymentId: null, authCode: null, cancellationToken);
                if (application is not null)
                {
                    await _notifications.SendPaymentDeclineAsync(application, cancellationToken);
                }

                throw new InvalidOperationException(string.IsNullOrWhiteSpace(reason)
                    ? "The card charge was declined by IntelliPay."
                    : $"The card charge was declined by IntelliPay: {reason}");
            }

            // Approved — capture identifiers (paymentid, fallback status; authcode).
            var paymentId = IntelliPayJson.ExtractField(chargeResponse, "paymentid");
            if (string.IsNullOrEmpty(paymentId))
            {
                paymentId = IntelliPayJson.ExtractField(chargeResponse, "status");
            }
            var authCode = IntelliPayJson.ExtractField(chargeResponse, "authcode");

            payment.PaymentId = paymentId;   // encrypted into payment_id
            payment.PnRefNum = authCode;     // encrypted into pn_ref_num_encr
            payment.PaidAmount = chargeTotal;
            payment.PaidDate = DateTime.Now;
            payment.ReceiptNum = await _paymentRepository.GetNextReceiptAsync(cancellationToken);
            payment.StatusCode = "PAID";
            // auth_amount now holds the freshly recomputed base fee; paid_amount holds base + service +
            // fine, exactly like legacy updatePaid(getRecalculatedAppAmt).

            var saved = await _paymentRepository.UpsertAsync(payment, cancellationToken);

            // Append PCI-stripped history entries oldest-first: the card_payment response, then the
            // payment_read details (source-tagged, matching legacy chargeStoredCC).
            await _paymentRepository.AppendPaymentHistoryAsync(request.AppId,
                IntelliPayJson.BuildHistoryEntry(chargeResponse, "card_payment"), cancellationToken);

            if (!string.IsNullOrEmpty(paymentId))
            {
                var detailsJson = await _intelliPayGateway.ReadPaymentDetailsAsync(paymentId, cancellationToken);
                await _paymentRepository.AppendPaymentHistoryAsync(request.AppId,
                    IntelliPayJson.BuildHistoryEntry(detailsJson, "payment_details", paymentId), cancellationToken);
            }

            _logger.LogInformation("Charged application {AppId}: receipt {Receipt}, status PAID", request.AppId, payment.ReceiptNum);

            // Successful CC charges/approvals are NOT audit-emailed (only payment errors/declines are);
            // the approved charge is recorded in payment history and the DB status (PAID) instead.

            return Map(saved);
        }

        // Non-CC (e.g. check/cash reconciled at approval): assign receipt + mark PAID, no gateway call.
        // Uses the same recalculated total (base + service + fine) so the received amount is correct.
        payment.PaidAmount = chargeTotal;
        payment.PaidDate = DateTime.Now;
        payment.ReceiptNum = await _paymentRepository.GetNextReceiptAsync(cancellationToken);
        payment.StatusCode = "PAID";
        return Map(await _paymentRepository.UpsertAsync(payment, cancellationToken));
    }

    public async Task<PaymentDto> UpdateDetailsAsync(string appId, UpdatePaymentRequest request, string updatedBy, CancellationToken cancellationToken = default)
    {
        // Once the application is approved the payment is locked — staff can no longer change the
        // method or fine (parity with the intra rule; the real charge/vault is already final).
        var application = await _applicationRepository.GetByIdAsync(appId, cancellationToken);
        if (application is not null && ApprovedStatuses.Contains(application.StatusCode))
        {
            throw new InvalidOperationException($"Payment for application {appId} cannot be changed after approval.");
        }

        // Load the existing record so the vaulted card / auth data and amounts are preserved; only the
        // staff-editable fields change. If no record exists yet (e.g. an exempt/check app that never
        // vaulted a card), create a minimal PEND record.
        var payment = await _paymentRepository.GetByAppIdAsync(appId, cancellationToken) ?? new AppPayment
        {
            AppId = appId,
            AddBy = updatedBy,
            StatusCode = "PEND"
        };

        var paymentType = string.IsNullOrWhiteSpace(request.PaymentType)
            ? payment.PaymentType
            : request.PaymentType.Trim().ToUpperInvariant();
        var fine = request.FineAmount ?? 0m;

        payment.PaymentType = paymentType;
        payment.FineAmount = fine;
        // Record the acting staff identity in the audit column (Upsert writes AddBy to update_by).
        payment.AddBy = updatedBy;

        // Amount due = live-recalculated base fee (fee × Σ wells) + service charge + fine, matching the
        // figure the approval wizard shows and the amount ChargeAsync would collect. Mirrors the legacy
        // intra UpdateAppServlet check: Amount Received must equal (authWorkAmt + serviceCharge + fine).
        var baseFee = application is not null ? FeeCalculator.BaseFee(application.Works) : payment.AuthAmount;
        var amountDue = FeeCalculator.RoundUp2(baseFee + payment.ServiceCharge + fine);

        switch (paymentType)
        {
            case "EXMPT":
                // Fee waived — clear any received-payment detail and mark exempt.
                payment.CheckNum = null;
                payment.AcctName = null;
                payment.PaidAmount = 0m;
                payment.StatusCode = "EXMPT";
                break;

            case "CHECK":
            {
                var checkNum = request.CheckNum?.Trim() ?? string.Empty;
                var acctName = request.AcctName?.Trim() ?? string.Empty;

                // All three are required for a check payment (legacy intra form marks Check Number
                // Received, Name on Account and Check Amount Received as mandatory).
                if (string.IsNullOrEmpty(checkNum))
                    throw new ArgumentException("Check Number Received is required for a check payment.");
                if (!checkNum.All(char.IsDigit))
                    throw new ArgumentException("Check Number must be numeric.");
                if (checkNum.Length > 20)
                    throw new ArgumentException("Check Number must be 20 characters or fewer.");
                if (string.IsNullOrEmpty(acctName))
                    throw new ArgumentException("Name on Account is required for a check payment.");
                if (acctName.Length > 50)
                    throw new ArgumentException("Name on Account must be 50 characters or fewer.");
                if (request.PaidAmount is null)
                    throw new ArgumentException("Check Amount Received is required for a check payment.");
                var paid = request.PaidAmount.Value;
                // Amount Received must equal the Amount Due (legacy UpdateAppServlet).
                if (FeeCalculator.RoundUp2(paid) != amountDue)
                    throw new ArgumentException($"Amount Received (${paid:F2}) is not the same as the Amount Due (${amountDue:F2}).");

                payment.CheckNum = checkNum;
                payment.AcctName = acctName;
                payment.PaidAmount = paid;
                // Check received in full with number + name on account => pending final reconciliation.
                payment.StatusCode = "PEND";
                break;
            }

            case "CASH":
            {
                var acctName = request.AcctName?.Trim() ?? string.Empty;

                // Name of Payer is mandatory for a cash payment (legacy BeanAppPay testNotNull).
                if (string.IsNullOrEmpty(acctName))
                    throw new ArgumentException("Name of Payer is required for a cash payment.");
                if (acctName.Length > 50)
                    throw new ArgumentException("Name of Payer must be 50 characters or fewer.");
                // Amount Received is mandatory for a cash payment (legacy BeanAppPay testNotNull).
                if (request.PaidAmount is null)
                    throw new ArgumentException("Amount Received is required for a cash payment.");
                var paid = request.PaidAmount.Value;
                if (FeeCalculator.RoundUp2(paid) != amountDue)
                    throw new ArgumentException($"Amount Received (${paid:F2}) is not the same as the Amount Due (${amountDue:F2}).");

                payment.CheckNum = null;
                payment.AcctName = acctName;
                payment.PaidAmount = paid;
                payment.StatusCode = "PEND";
                break;
            }

            default:
                // CC / MC / VISA — method + fine only; the vaulted card, auth data, amounts and status
                // are preserved (the real charge happens at approval via ChargeAsync).
                break;
        }

        _logger.LogInformation("Updating payment ({PaymentType}) for application {AppId} by {UpdatedBy}", paymentType, appId, updatedBy);
        var saved = await _paymentRepository.UpsertAsync(payment, cancellationToken);
        return Map(saved);
    }

    private static PaymentDto Map(AppPayment payment) => new(
        payment.AppId,
        payment.PaymentType,
        payment.AuthIdEncr,
        payment.PaymentId,
        payment.PaidAmount,
        payment.FineAmount,
        payment.ServiceCharge,
        payment.AuthAmount,
        payment.CheckNum,
        payment.StatusCode,
        payment.PnRefNum,
        payment.ReceiptNum,
        payment.PaidDate,
        payment.AcctName);
}
