using Microsoft.Extensions.Logging;
using PWA.PermitsApi.Application.Common;
using PWA.PermitsApi.Application.DTOs;
using PWA.PermitsApi.Application.Interfaces;
using PWA.PermitsApi.Application.Interfaces.Integration;
using PWA.PermitsApi.Application.Interfaces.Repositories;
using PWA.PermitsApi.Application.Notifications;
using DomainApplication = PWA.PermitsApi.Domain.Models.Application;

namespace PWA.PermitsApi.Application.Services;

public sealed class ApprovalService : IApprovalService
{
    private readonly IApplicationRepository _applicationRepository;
    private readonly IWorkPermitRepository _workPermitRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IConditionsService _conditionsService;
    private readonly IPermitDocumentService _permitDocumentService;
    private readonly IFileTransferService _fileTransferService;
    private readonly IPaymentService _paymentService;
    private readonly IPermitNotificationService _notifications;
    private readonly ILogger<ApprovalService> _logger;

    public ApprovalService(
        IApplicationRepository applicationRepository,
        IWorkPermitRepository workPermitRepository,
        IPaymentRepository paymentRepository,
        IConditionsService conditionsService,
        IPermitDocumentService permitDocumentService,
        IFileTransferService fileTransferService,
        IPaymentService paymentService,
        IPermitNotificationService notifications,
        ILogger<ApprovalService> logger)
    {
        _applicationRepository = applicationRepository;
        _workPermitRepository = workPermitRepository;
        _paymentRepository = paymentRepository;
        _conditionsService = conditionsService;
        _permitDocumentService = permitDocumentService;
        _fileTransferService = fileTransferService;
        _paymentService = paymentService;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<ApprovalResult> ApproveAsync(string appId, ApprovalRequest request, CancellationToken cancellationToken = default)
    {
        var application = await _applicationRepository.GetByIdAsync(appId, cancellationToken)
            ?? throw new InvalidOperationException($"Application {appId} was not found.");

        // The approval email carries the permit/receipt PDF, so the payment MUST be finalized (marked
        // PAID) in the DB BEFORE that email is built — the emailed permit then shows the same paid
        // amount, receipt number and PAID status staff see when they print it later, for EVERY payment
        // method:
        //   • CC          — charge the vaulted card (assigns receipt + paid amount incl. any fine, PAID);
        //   • CHECK/CASH  — assign a receipt and mark the received payment PAID;
        //   • EXMPT       — already settled at $0 (fee waived); left as its exempt status.
        // ChargeAsync is idempotent for an already-PAID record, so the intra client's existing
        // post-approval CC charge call cannot double-charge. A CC decline THROWS here — aborting the
        // approval so NO permit email is sent for an application that is not paid in the DB (ChargeAsync
        // has already recorded PAYFL, audited the failure and notified the applicant of the decline).
        var paymentRecord = await _paymentRepository.GetByAppIdAsync(appId, cancellationToken);
        if (paymentRecord is not null && paymentRecord.PaymentType != "EXMPT")
        {
            try
            {
                await _paymentService.ChargeAsync(new ChargeRequest
                {
                    AppId = appId,
                    ApprovedBy = request.ApprovedBy,
                    FineAmount = request.FineAmount
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                // ChargeAsync already audited/notified any decline; flag so the global handler does
                // not send a duplicate audit, then abort the approval (no permit email for an unpaid app).
                ex.Data[AuditMarker.AuditEmailedKey] = true;
                throw;
            }
        }

        IReadOnlyList<string> permitNumbers;
        try
        {
            await _applicationRepository.UpdateApprovalAsync(appId, "APPRV", request.ApprovedBy, cancellationToken);
            // Legacy ProcessApprovalServlet also flips the work + work-spec rows to APPRV on approval.
            await _applicationRepository.UpdateWorkStatusesAsync(appId, "APPRV", request.ApprovedBy, cancellationToken);
            var permits = await _workPermitRepository.GeneratePermitsAsync(appId, DateTime.UtcNow.AddYears(1), cancellationToken);
            permitNumbers = permits.Select(p => p.PermitNumber).ToList();
        }
        catch (Exception ex)
        {
            // Audit-notify staff of the failure (best-effort), then surface the error to the caller.
            await _notifications.SendApprovalExceptionAuditAsync(appId, "approve", request.ApprovedBy, ex, cancellationToken);
            // Already audited here — flag so the global exception handler doesn't send a duplicate
            // when this rethrow propagates up to it.
            ex.Data[AuditMarker.AuditEmailedKey] = true;
            throw;
        }

        // Build the approval email attachments (permit PDF + uploaded site map), mirroring the legacy
        // intra ProcessApprovalServlet which attached both. Best-effort: a rendering/FTP failure must
        // not block the approval or the notification itself.
        var attachments = await BuildApprovalAttachmentsAsync(appId, application, cancellationToken);

        // Applicant approval notification with permit numbers (CCs contact/owner/client, BCC audit).
        await _notifications.SendApprovalAsync(application, permitNumbers, inspector: null, attachments, cancellationToken);

        _logger.LogInformation("Application {AppId} approved by {ApprovedBy}", appId, request.ApprovedBy);
        return new ApprovalResult(appId, true, "Application approved and permit numbers assigned.", permitNumbers.ToList());
    }

    private async Task<IReadOnlyList<EmailAttachment>> BuildApprovalAttachmentsAsync(
        string appId, DomainApplication application, CancellationToken cancellationToken)
    {
        var attachments = new List<EmailAttachment>();

        // 1) Permit PDF — the primary attachment ("your receipt and permit(s)").
        try
        {
            var payment = await _paymentRepository.GetByAppIdAsync(appId, cancellationToken);
            var permitInfo = await _applicationRepository.GetPermitInfoAsync(appId, cancellationToken);
            var conditions = await ResolveConditionsAsync(appId, cancellationToken);

            var model = new PermitDocumentModel(application, permitInfo, payment, conditions, Approved: true);
            var pdf = _permitDocumentService.GeneratePermitPdf(model);
            attachments.Add(new EmailAttachment($"Permit_{appId}.pdf", pdf, "application/pdf"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate the permit PDF for application {AppId}; approval email will be sent without it.", appId);
            await _notifications.SendSystemExceptionAuditAsync("ApprovalService.BuildPermitPdf", "Application " + appId, ex, cancellationToken);
        }

        // 2) Uploaded site map — best-effort download from FTP and attach (skipped in mock/dev).
        try
        {
            if (!string.IsNullOrWhiteSpace(application.SitemapFilename))
            {
                var content = await _fileTransferService.DownloadAsync(application.SitemapFilename, cancellationToken);
                if (content is { Length: > 0 })
                {
                    var fileName = Path.GetFileName(application.SitemapFilename)!;
                    attachments.Add(new EmailAttachment(fileName, content, GuessContentType(fileName)));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to attach the site map for application {AppId}; approval email will be sent without it.", appId);
            await _notifications.SendSystemExceptionAuditAsync("ApprovalService.AttachSitemap", "Application " + appId, ex, cancellationToken);
        }

        return attachments;
    }

    private async Task<IReadOnlyList<string>> ResolveConditionsAsync(string appId, CancellationToken cancellationToken)
    {
        var conditions = await _conditionsService.GetConditionsAsync(appId, cancellationToken);
        var texts = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var work in conditions.Works)
        {
            // Selected holds only the conditions persisted to the work, so a work still Pending
            // Conditions (nothing saved) naturally contributes nothing to the approval email.
            var labelByCode = work.Available.ToDictionary(c => c.Code, c => c.Label, StringComparer.OrdinalIgnoreCase);
            foreach (var c in work.Selected)
            {
                var text = !string.IsNullOrWhiteSpace(c.OtherDesc)
                    ? c.OtherDesc!
                    : (labelByCode.TryGetValue(c.ConditionType, out var label) ? label : c.ConditionType);
                if (!string.IsNullOrWhiteSpace(text) && seen.Add(text))
                {
                    texts.Add(text);
                }
            }
        }

        return texts;
    }

    private static string GuessContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".tif" or ".tiff" => "image/tiff",
            ".gif" => "image/gif",
            _ => "application/octet-stream"
        };
    }
}
