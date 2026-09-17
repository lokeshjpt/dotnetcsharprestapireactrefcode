using PWA.PermitsApi.Application.DTOs;
using PWA.PermitsApi.Application.Interfaces.Integration;
using PWA.PermitsApi.Application.Interfaces.Repositories;
using PWA.PermitsApi.Domain.Models;
using DomainApplication = PWA.PermitsApi.Domain.Models.Application;

namespace PWA.PermitsApi.Tests;

internal sealed class FakeApplicationRepository : IApplicationRepository
{
    public DomainApplication? Created { get; private set; }
    public List<(AppDocumentLink Link, string AddedBy)> DocumentLinks { get; } = new();

    public Task<DomainApplication> CreateAsync(DomainApplication application, CancellationToken cancellationToken = default)
    {
        Created = application;
        return Task.FromResult(application);
    }

    public DomainApplication? ByIdResult { get; set; }
    public Task<DomainApplication?> GetByIdAsync(string appId, CancellationToken cancellationToken = default) => Task.FromResult(ByIdResult);
    public Task<IReadOnlyList<DomainApplication>> SearchAsync(ApplicationSearchRequest request, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DomainApplication>>(Array.Empty<DomainApplication>());
    public Task<int> CountAsync(ApplicationSearchRequest request, CancellationToken cancellationToken = default) => Task.FromResult(0);
    public Task UpdateStatusAsync(string appId, string statusCode, string updatedBy, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public (string AppId, string StatusCode, string ApprovedBy)? Approval { get; private set; }
    public Task UpdateApprovalAsync(string appId, string statusCode, string approvedBy, CancellationToken cancellationToken = default)
    {
        Approval = (appId, statusCode, approvedBy);
        return Task.CompletedTask;
    }

    public string? WorkStatus { get; private set; }
    public Task UpdateWorkStatusesAsync(string appId, string statusCode, string updatedBy, CancellationToken cancellationToken = default)
    {
        WorkStatus = statusCode;
        return Task.CompletedTask;
    }

    public Task<int> AddDocumentLinkAsync(AppDocumentLink link, string addedBy, CancellationToken cancellationToken = default)
    {
        DocumentLinks.Add((link, addedBy));
        return Task.FromResult(DocumentLinks.Count);
    }

    public List<(string AppId, string FileName, string UpdatedBy)> Sitemaps { get; } = new();
    public Task UpdateSitemapAsync(string appId, string sitemapFilename, string updatedBy, CancellationToken cancellationToken = default)
    {
        Sitemaps.Add((appId, sitemapFilename, updatedBy));
        return Task.CompletedTask;
    }

    public List<(string AppId, UpdateProjectInfoRequest Request, string UpdatedBy)> ProjectUpdates { get; } = new();
    public Task UpdateProjectInfoAsync(string appId, UpdateProjectInfoRequest request, string updatedBy, CancellationToken cancellationToken = default)
    {
        ProjectUpdates.Add((appId, request, updatedBy));
        return Task.CompletedTask;
    }

    public List<(string AppId, UpdateApplicantInfoRequest Request, string UpdatedBy)> ApplicantUpdates { get; } = new();
    public Task UpdateApplicantInfoAsync(string appId, UpdateApplicantInfoRequest request, string updatedBy, CancellationToken cancellationToken = default)
    {
        ApplicantUpdates.Add((appId, request, updatedBy));
        return Task.CompletedTask;
    }

    public List<(string AppId, UpdateHazardRequest Request, string UpdatedBy)> HazardUpdates { get; } = new();
    public Task UpdateHazardAsync(string appId, UpdateHazardRequest request, string updatedBy, CancellationToken cancellationToken = default)
    {
        HazardUpdates.Add((appId, request, updatedBy));
        return Task.CompletedTask;
    }

    public List<(string AppId, int WorkId, UpdateWorkRequest Request, string UpdatedBy)> WorkUpdates { get; } = new();
    public Task UpdateWorkAsync(string appId, int workId, UpdateWorkRequest request, string updatedBy, CancellationToken cancellationToken = default)
    {
        WorkUpdates.Add((appId, workId, request, updatedBy));
        return Task.CompletedTask;
    }

    public List<(string AppId, AddWorkRequest Request, string AddedBy)> WorkAdds { get; } = new();
    public Task AddWorkAsync(string appId, AddWorkRequest request, string addedBy, CancellationToken cancellationToken = default)
    {
        WorkAdds.Add((appId, request, addedBy));
        return Task.CompletedTask;
    }

    public List<(string AppId, int WorkId, string UpdatedBy)> WorkCancellations { get; } = new();
    public bool CancelWorkResult { get; set; } = true;
    public Task<bool> CancelWorkAsync(string appId, int workId, string updatedBy, CancellationToken cancellationToken = default)
    {
        WorkCancellations.Add((appId, workId, updatedBy));
        return Task.FromResult(CancelWorkResult);
    }

    public List<(string AppId, int WorkId)> WorkDeletions { get; } = new();
    public bool DeleteWorkResult { get; set; } = true;
    public Task<bool> DeleteWorkAsync(string appId, int workId, CancellationToken cancellationToken = default)
    {
        WorkDeletions.Add((appId, workId));
        return Task.FromResult(DeleteWorkResult);
    }

    public List<(string AppId, int WorkId, WcrUpdateRequest Request, string UpdatedBy)> WcrUpdates { get; } = new();
    public Task UpdateWcrAsync(string appId, int workId, WcrUpdateRequest request, string updatedBy, CancellationToken cancellationToken = default)
    {
        WcrUpdates.Add((appId, workId, request, updatedBy));
        return Task.CompletedTask;
    }

    public List<(string AppId, int WorkId, int WorkSpecsId, string Column, string FileName, string UpdatedBy)> SpecFileUpdates { get; } = new();
    public Task UpdateSpecFileAsync(string appId, int workId, int workSpecsId, string column, string fileName, string updatedBy, CancellationToken cancellationToken = default)
    {
        SpecFileUpdates.Add((appId, workId, workSpecsId, column, fileName, updatedBy));
        return Task.CompletedTask;
    }

    public List<(string AppId, UpdateApprovalDetailsRequest Request, string UpdatedBy)> ApprovalDetailUpdates { get; } = new();
    public Task UpdateApprovalDetailsAsync(string appId, UpdateApprovalDetailsRequest request, string updatedBy, CancellationToken cancellationToken = default)
    {
        ApprovalDetailUpdates.Add((appId, request, updatedBy));
        return Task.CompletedTask;
    }

    public List<(string AppId, string CancelledBy)> Cancellations { get; } = new();
    public Task CancelApplicationAsync(string appId, string cancelledBy, CancellationToken cancellationToken = default)
    {
        Cancellations.Add((appId, cancelledBy));
        return Task.CompletedTask;
    }

    public PermitInfoDto? PermitInfoResult { get; set; }
    public Task<PermitInfoDto?> GetPermitInfoAsync(string appId, CancellationToken cancellationToken = default)
        => Task.FromResult(PermitInfoResult);

    public decimal SiteExtraRate { get; set; } = 85m;
    public Task<decimal> GetSiteExtraRateAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(SiteExtraRate);

    public List<AppDocumentDto> Documents { get; } = new();
    public Task<IReadOnlyList<AppDocumentDto>> GetDocumentsAsync(string appId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<AppDocumentDto>>(Documents);

    public List<(string AppId, int SeqNum)> DeletedDocuments { get; } = new();
    public Task<bool> DeleteDocumentLinkAsync(string appId, int seqNum, CancellationToken cancellationToken = default)
    {
        DeletedDocuments.Add((appId, seqNum));
        return Task.FromResult(true);
    }

    public List<AppNoteDto> NotesList { get; } = new();
    public Task<IReadOnlyList<AppNoteDto>> GetNotesAsync(string appId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<AppNoteDto>>(NotesList);

    public List<(string AppId, string NotesText, string AddedBy)> AddedNotes { get; } = new();
    public Task<AppNoteDto> AddNoteAsync(string appId, string notesText, string addedBy, CancellationToken cancellationToken = default)
    {
        AddedNotes.Add((appId, notesText, addedBy));
        return Task.FromResult(new AppNoteDto(AddedNotes.Count, notesText, addedBy, DateTime.UtcNow));
    }

    public List<(string AppId, DateTime StartDate, DateTime EndDate, bool IsEdit, string UpdatedBy)> Extensions { get; } = new();
    public Task UpdateExtensionAsync(string appId, DateTime startDate, DateTime endDate, bool isEdit, string updatedBy, CancellationToken cancellationToken = default)
    {
        Extensions.Add((appId, startDate, endDate, isEdit, updatedBy));
        return Task.CompletedTask;
    }
}

internal sealed class FakePaymentRepository : IPaymentRepository
{
    public AppPayment? Saved { get; private set; }
    public AppPayment? Existing { get; set; }
    public string NextReceipt { get; set; } = "WR2026-0001";
    public List<string> HistoryEntries { get; } = new();
    public Task<AppPayment?> GetByAppIdAsync(string appId, CancellationToken cancellationToken = default) => Task.FromResult(Existing);
    public Task<AppPayment> UpsertAsync(AppPayment payment, CancellationToken cancellationToken = default)
    {
        Saved = payment;
        return Task.FromResult(payment);
    }

    public Task<string> GetNextReceiptAsync(CancellationToken cancellationToken = default) => Task.FromResult(NextReceipt);

    public Task AppendPaymentHistoryAsync(string appId, string entryJson, CancellationToken cancellationToken = default)
    {
        HistoryEntries.Add(entryJson);
        return Task.CompletedTask;
    }
}

internal sealed class FakeConditionsRepository : IConditionsRepository
{
    public List<WorkForConditionsDto> Works { get; } = new();
    public List<WorkConditionRowDto> Applied { get; set; } = new();

    /// <summary>Work-type-scoped master lists keyed by "category|type".</summary>
    public Dictionary<string, List<ReferenceItemDto>> AvailableByWorkType { get; } = new();

    public List<(string AppId, int WorkId, IReadOnlyList<ConditionSelectionDto> Conditions, bool NoSpecials, string UpdatedBy)> Replaces { get; } = new();
    public (string AppId, int WorkId, IReadOnlyList<ConditionSelectionDto> Conditions, bool NoSpecials, string UpdatedBy)? LastReplace
        => Replaces.Count == 0 ? null : Replaces[^1];

    public Task<IReadOnlyList<WorkForConditionsDto>> GetWorksAsync(string appId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<WorkForConditionsDto>>(Works);

    public Task<IReadOnlyList<WorkConditionRowDto>> GetAppliedConditionsAsync(string appId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<WorkConditionRowDto>>(Applied);

    public Task<IReadOnlyList<ReferenceItemDto>> GetWorkConditionTypesAsync(string workCategory, string workType, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ReferenceItemDto>>(
            AvailableByWorkType.TryGetValue($"{workCategory}|{workType}", out var list)
                ? list
                : new List<ReferenceItemDto>());

    public Task ReplaceWorkConditionsAsync(string appId, int workId, IReadOnlyList<ConditionSelectionDto> conditions, bool noSpecials, string updatedBy, CancellationToken cancellationToken = default)
    {
        Replaces.Add((appId, workId, conditions, noSpecials, updatedBy));
        Applied = Applied.Where(a => a.WorkId != workId)
            .Concat(conditions.Select(c => new WorkConditionRowDto(workId, c.ConditionType, c.OtherDesc)))
            .ToList();
        return Task.CompletedTask;
    }
}

internal sealed class FakeInspectionRepository : IInspectionRepository
{
    public Inspection? Added { get; private set; }
    public int? MaxSlotsPerDay { get; set; }
    public bool Throw { get; set; }
    public List<InspectionUnavailableDay> Unavailable { get; } = new();
    public List<Inspection> Assignments { get; } = new();

    public Task<IReadOnlyList<Inspection>> GetByApplicationAsync(string appId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Inspection>>(Array.Empty<Inspection>());

    public Task<Inspection> AddAsync(Inspection inspection, CancellationToken cancellationToken = default)
    {
        Added = inspection;
        Assignments.Add(inspection);
        return Task.FromResult(inspection);
    }

    public Task<Inspection?> UpdateAsync(Inspection inspection, CancellationToken cancellationToken = default) => Task.FromResult<Inspection?>(inspection);

    public Task<bool> DeleteAsync(DateTime inspectionDate, int slotId, CancellationToken cancellationToken = default)
    {
        var removed = Assignments.RemoveAll(a => a.InspectionDate.Date == inspectionDate.Date && a.SlotId == slotId);
        return Task.FromResult(removed > 0);
    }

    public Task<int?> GetMaxSlotsPerDayAsync(CancellationToken cancellationToken = default)
    {
        if (Throw)
        {
            throw new InvalidOperationException("DB down");
        }

        return Task.FromResult(MaxSlotsPerDay);
    }

    public Task<IReadOnlyList<InspectionUnavailableDay>> GetUnavailableDaysAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<InspectionUnavailableDay>>(Unavailable);

    public Task<IReadOnlyList<Inspection>> GetAssignmentsInRangeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Inspection>>(Assignments);

    // ---- Intra Inspections menu list screens (capture request + return canned result) ----

    public InspectionPendingSearchRequest? LastPendingRequest { get; private set; }
    public InspectionDueSearchRequest? LastWcrRequest { get; private set; }
    public InspectionDueSearchRequest? LastGeoLogRequest { get; private set; }
    public InspectionHoldSearchRequest? LastHoldRequest { get; private set; }
    public (DateTime From, DateTime To)? LastScheduledRange { get; private set; }

    public Task<InspectionPendingSearchResult> SearchPendingInspectionsAsync(InspectionPendingSearchRequest request, CancellationToken cancellationToken = default)
    {
        LastPendingRequest = request;
        var item = new InspectionPendingItemDto("1700000000001", "0000001-01", "Acme Wells", "Jane Doe", "1 Main St", "Oakland", "01/01/2026", "02/01/2026",
            new List<InspectionScheduleLineDto> { new("1700000000001", "01/15/2026", "9:00 AM", 4, "Sam Insp", "IPEND", "Pending") });
        return Task.FromResult(new InspectionPendingSearchResult(new List<InspectionPendingItemDto> { item }, 1));
    }

    public Task<InspectionDueSearchResult> SearchPendingWcrAsync(InspectionDueSearchRequest request, CancellationToken cancellationToken = default)
    {
        LastWcrRequest = request;
        var item = new InspectionDueItemDto("1700000000002", "0000002-01", "Acme Wells", "Jane Doe", "2 Main St", "Oakland", 4, "Sam Insp", "01/20/2026");
        return Task.FromResult(new InspectionDueSearchResult(new List<InspectionDueItemDto> { item }, 1));
    }

    public Task<InspectionDueSearchResult> SearchPendingGeoLogAsync(InspectionDueSearchRequest request, CancellationToken cancellationToken = default)
    {
        LastGeoLogRequest = request;
        var item = new InspectionDueItemDto("1700000000003", "0000003-01", "Acme Wells", "Jane Doe", "3 Main St", "Oakland", 4, "Sam Insp", "01/25/2026");
        return Task.FromResult(new InspectionDueSearchResult(new List<InspectionDueItemDto> { item }, 1));
    }

    public Task<InspectionHoldSearchResult> SearchHoldListAsync(InspectionHoldSearchRequest request, CancellationToken cancellationToken = default)
    {
        LastHoldRequest = request;
        var item = new InspectionHoldItemDto("1700000000004", "0000004-01", "Acme Wells", "Jane Doe", "4 Main St", "Oakland", "01/01/2026", "02/01/2026", 4, "Sam Insp", "HOLD");
        return Task.FromResult(new InspectionHoldSearchResult(new List<InspectionHoldItemDto> { item }, 1));
    }

    public Task<IReadOnlyList<InspectionScheduleLineDto>> GetScheduledInspectionsAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        LastScheduledRange = (from, to);
        var line = new InspectionScheduleLineDto("1700000000001", "01/15/2026", "9:00 AM", 4, "Sam Insp", "IPEND", "Pending");
        return Task.FromResult<IReadOnlyList<InspectionScheduleLineDto>>(new List<InspectionScheduleLineDto> { line });
    }
}

internal sealed class FakeEmailService : IEmailService
{
    public List<(string To, string Subject, string Body)> Sent { get; } = new();
    public List<PWA.PermitsApi.Application.Notifications.EmailNotification> Notifications { get; } = new();
    public List<(string Subject, string Body)> Audits { get; } = new();
    public bool Throw { get; set; }

    public Task SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (Throw)
        {
            throw new InvalidOperationException("mail down");
        }
        Sent.Add((toAddress, subject, body));
        return Task.CompletedTask;
    }

    public Task SendProjectNotificationAsync(string projectId, string? subjectOverride, string body, CancellationToken cancellationToken = default)
    {
        Sent.Add((projectId, subjectOverride ?? string.Empty, body));
        return Task.CompletedTask;
    }

    public Task SendNotificationAsync(PWA.PermitsApi.Application.Notifications.EmailNotification notification, CancellationToken cancellationToken = default)
    {
        if (Throw)
        {
            throw new InvalidOperationException("mail down");
        }
        Notifications.Add(notification);
        Sent.Add((string.Join(",", notification.To), notification.Subject, notification.HtmlBody));
        return Task.CompletedTask;
    }

    public Task SendAuditAsync(string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        if (Throw)
        {
            throw new InvalidOperationException("mail down");
        }
        Audits.Add((subject, htmlBody));
        return Task.CompletedTask;
    }
}

internal sealed class FakePermitNotificationService : IPermitNotificationService
{
    public List<(string Scenario, string AppId)> Sent { get; } = new();

    public IReadOnlyList<PWA.PermitsApi.Application.Notifications.EmailAttachment>? LastApprovalAttachments { get; private set; }

    public Task SendApplicationConfirmationAsync(DomainApplication application, decimal authAmount, string paymentType, string? checkNum, CancellationToken cancellationToken = default)
    {
        Sent.Add(("confirmation", application.AppId));
        return Task.CompletedTask;
    }

    public Task SendSitemapReceivedAsync(DomainApplication application, CancellationToken cancellationToken = default)
    {
        Sent.Add(("sitemap", application.AppId));
        return Task.CompletedTask;
    }

    public Task SendApprovalAsync(DomainApplication application, IReadOnlyList<string> permitNumbers, PWA.PermitsApi.Application.Notifications.InspectorContact? inspector, IReadOnlyList<PWA.PermitsApi.Application.Notifications.EmailAttachment>? attachments = null, CancellationToken cancellationToken = default)
    {
        Sent.Add(("approval", application.AppId));
        LastApprovalAttachments = attachments;
        return Task.CompletedTask;
    }

    public Task SendPaymentDeclineAsync(DomainApplication application, CancellationToken cancellationToken = default)
    {
        Sent.Add(("decline", application.AppId));
        return Task.CompletedTask;
    }

    public Task SendApplicationCancelledAsync(DomainApplication application, string cancelledBy, CancellationToken cancellationToken = default)
    {
        Sent.Add(("cancelled", application.AppId));
        return Task.CompletedTask;
    }

    public Task SendInspectionScheduledAsync(DomainApplication application, IReadOnlyList<string> permitNumbers, DateTime? inspectionDateTime, PWA.PermitsApi.Application.Notifications.InspectorContact? inspector, CancellationToken cancellationToken = default)
    {
        Sent.Add(("inspection", application.AppId));
        return Task.CompletedTask;
    }

    public Task SendCcPreAuthAuditAsync(DomainApplication application, decimal authAmount, string? customerId, CancellationToken cancellationToken = default)
    {
        Sent.Add(("ccPreAuthAudit", application.AppId));
        return Task.CompletedTask;
    }

    public Task SendPaymentErrorAuditAsync(string appId, string paymentType, string? applicantName, string? email, bool dbCommitted, string error, CancellationToken cancellationToken = default)
    {
        Sent.Add(("paymentErrorAudit", appId));
        return Task.CompletedTask;
    }

    public Task SendSitemapScanAuditAsync(string appId, string reason, CancellationToken cancellationToken = default)
    {
        Sent.Add(("sitemapScanAudit", appId));
        return Task.CompletedTask;
    }

    public Task SendApprovalExceptionAuditAsync(string appId, string process, string user, Exception exception, CancellationToken cancellationToken = default)
    {
        Sent.Add(("approvalExceptionAudit", appId));
        return Task.CompletedTask;
    }

    public Task SendCcChargeAuditAsync(string appId, string amount, bool success, string? failReason, string? paymentId, string? authCode, CancellationToken cancellationToken = default)
    {
        Sent.Add((success ? "ccChargeApprovedAudit" : "ccChargeFailedAudit", appId));
        return Task.CompletedTask;
    }

    public Task SendSystemExceptionAuditAsync(string source, string? context, Exception exception, CancellationToken cancellationToken = default)
    {
        Sent.Add(("systemExceptionAudit", context ?? source));
        return Task.CompletedTask;
    }

    public Task SendHttpErrorAuditAsync(string method, string path, int statusCode, string reason, string? user, string? clientIp, string? correlationId, CancellationToken cancellationToken = default)
    {
        Sent.Add(("httpErrorAudit", $"{statusCode} {method} {path} {clientIp}"));
        return Task.CompletedTask;
    }

    public Task SendUnauthorizedAccessAuditAsync(string? user, string? clientIp, string method, string path, string? correlationId, CancellationToken cancellationToken = default)
    {
        Sent.Add(("unauthorizedAccessAudit", $"{user} {clientIp} {method} {path}"));
        return Task.CompletedTask;
    }
}

internal sealed class FakeIntelliPayGateway : IIntelliPayGateway
{
    public int VaultCalls { get; private set; }
    public string? ChargeResponse { get; set; }
    public string DetailsResponse { get; set; } = "{\"paymentid\":\"PID123\",\"status\":\"settled\"}";
    public string? LastReadPaymentId { get; private set; }
    public decimal? LastChargeAmount { get; private set; }

    public Task<IntelliPayVaultResult> VaultZeroDollarAsync(string applicationId, CancellationToken cancellationToken = default)
    {
        VaultCalls++;
        return Task.FromResult(new IntelliPayVaultResult($"CUST{applicationId[^Math.Min(8, applicationId.Length)..]}", true));
    }

    public Task<string> ChargeStoredCustomerAsync(string appId, string customerId, decimal amount, CancellationToken cancellationToken = default)
    {
        LastChargeAmount = amount;
        return Task.FromResult(ChargeResponse
            ?? $"{{\"response\":\"A\",\"paymentid\":\"PID123\",\"authcode\":\"AUTH99\",\"amount\":\"{amount:F2}\",\"invoice\":\"{appId}\"}}");
    }

    public Task<string> ReadPaymentDetailsAsync(string paymentId, CancellationToken cancellationToken = default)
    {
        LastReadPaymentId = paymentId;
        return Task.FromResult(DetailsResponse);
    }

    public Task<string> FetchLightboxScriptsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult("<!-- mock lightbox -->");
}

internal sealed class FakeVirusScanner : IVirusScanner
{
    public bool Clean { get; set; } = true;
    public bool ThrowOnScan { get; set; }
    public Task<bool> ScanAsync(string fileName, byte[] content, CancellationToken cancellationToken = default)
    {
        if (ThrowOnScan)
        {
            throw new System.Net.Http.HttpRequestException("ICAP unreachable");
        }
        return Task.FromResult(Clean);
    }
}

internal sealed class FakeFileTransferService : IFileTransferService
{
    public bool ThrowOnUpload { get; set; }
    public int UploadCalls { get; private set; }
    public byte[]? DownloadResult { get; set; }
    public int DownloadCalls { get; private set; }
    public Task<string> UploadAsync(string fileName, byte[] content, CancellationToken cancellationToken = default)
    {
        UploadCalls++;
        if (ThrowOnUpload)
        {
            throw new IOException("ftp down");
        }

        return Task.FromResult($"ftp://mock/{fileName}");
    }

    public Task<byte[]?> DownloadAsync(string fileName, CancellationToken cancellationToken = default)
    {
        DownloadCalls++;
        return Task.FromResult(DownloadResult);
    }
}

internal sealed class FakePermitDocumentService : PWA.PermitsApi.Application.Interfaces.Integration.IPermitDocumentService
{
    public int Calls { get; private set; }
    public PWA.PermitsApi.Application.Notifications.PermitDocumentModel? LastModel { get; private set; }
    public byte[] Pdf { get; set; } = System.Text.Encoding.ASCII.GetBytes("%PDF-1.7 fake");

    public byte[] GeneratePermitPdf(PWA.PermitsApi.Application.Notifications.PermitDocumentModel model)
    {
        Calls++;
        LastModel = model;
        return Pdf;
    }
}
