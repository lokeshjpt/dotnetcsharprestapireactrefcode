using Microsoft.Extensions.Logging;
using PWA.PermitsApi.Application.Common;
using PWA.PermitsApi.Application.DTOs;
using PWA.PermitsApi.Application.Interfaces;
using PWA.PermitsApi.Application.Interfaces.Integration;
using PWA.PermitsApi.Application.Interfaces.Repositories;
using PWA.PermitsApi.Domain.Models;
using DomainApplication = PWA.PermitsApi.Domain.Models.Application;

namespace PWA.PermitsApi.Application.Services;

public sealed class ApplicationService : IApplicationService
{
    private readonly IApplicationRepository _applicationRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IInspectionRepository _inspectionRepository;
    private readonly IIntelliPayGateway _intelliPayGateway;
    private readonly IPermitNotificationService _notifications;
    private readonly ILogger<ApplicationService> _logger;

    public ApplicationService(
        IApplicationRepository applicationRepository,
        IPaymentRepository paymentRepository,
        IInspectionRepository inspectionRepository,
        IIntelliPayGateway intelliPayGateway,
        IPermitNotificationService notifications,
        ILogger<ApplicationService> logger)
    {
        _applicationRepository = applicationRepository;
        _paymentRepository = paymentRepository;
        _inspectionRepository = inspectionRepository;
        _intelliPayGateway = intelliPayGateway;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<ApplicationDto?> GetByIdAsync(string appId, CancellationToken cancellationToken = default)
    {
        var application = await _applicationRepository.GetByIdAsync(appId, cancellationToken);
        return application is null ? null : Map(application);
    }

    public Task<IReadOnlyList<AppDocumentDto>> GetDocumentsAsync(string appId, CancellationToken cancellationToken = default)
        => _applicationRepository.GetDocumentsAsync(appId, cancellationToken);

    public Task<bool> DeleteDocumentAsync(string appId, int seqNum, CancellationToken cancellationToken = default)
        => _applicationRepository.DeleteDocumentLinkAsync(appId, seqNum, cancellationToken);

    public Task<IReadOnlyList<AppNoteDto>> GetNotesAsync(string appId, CancellationToken cancellationToken = default)
        => _applicationRepository.GetNotesAsync(appId, cancellationToken);

    public Task<AppNoteDto> AddNoteAsync(string appId, string notesText, string addedBy, CancellationToken cancellationToken = default)
        => _applicationRepository.AddNoteAsync(appId, notesText, addedBy, cancellationToken);

    public async Task<ApplicationDto?> UpdateExtensionAsync(string appId, UpdateExtensionRequest request, string updatedBy, CancellationToken cancellationToken = default)
    {
        if (request.ExtensionStartDate is null)
        {
            throw new ArgumentException("Extension Start Date is required.");
        }
        if (request.ExtensionEndDate is null)
        {
            throw new ArgumentException("Extension End Date is required.");
        }
        if (request.ExtensionEndDate.Value.Date < request.ExtensionStartDate.Value.Date)
        {
            throw new ArgumentException("Extension End Date must be greater than or same as Extension Start Date.");
        }

        await _applicationRepository.UpdateExtensionAsync(
            appId, request.ExtensionStartDate.Value, request.ExtensionEndDate.Value, request.IsEdit, updatedBy, cancellationToken);
        return await GetByIdAsync(appId, cancellationToken);
    }

    public async Task<ApplicationSearchResult> SearchAsync(ApplicationSearchRequest request, CancellationToken cancellationToken = default)
    {
        var items = await _applicationRepository.SearchAsync(request, cancellationToken);
        var totalCount = await _applicationRepository.CountAsync(request, cancellationToken);
        var mapped = items.Select(Map).ToList();
        return new ApplicationSearchResult(mapped, totalCount);
    }

    public async Task<ApplicationDto?> UpdateProjectInfoAsync(string appId, UpdateProjectInfoRequest request, string updatedBy, CancellationToken cancellationToken = default)
    {
        request = request with
        {
            OwnerPhone = PhoneNormalizer.DigitsOnly(request.OwnerPhone),
            ClientPhone = PhoneNormalizer.DigitsOnly(request.ClientPhone)
        };
        await _applicationRepository.UpdateProjectInfoAsync(appId, request, updatedBy, cancellationToken);
        return await GetByIdAsync(appId, cancellationToken);
    }

    public async Task<ApplicationDto?> UpdateApplicantInfoAsync(string appId, UpdateApplicantInfoRequest request, string updatedBy, CancellationToken cancellationToken = default)
    {
        request = request with
        {
            AppPhone = PhoneNormalizer.DigitsOnly(request.AppPhone),
            AppFax = PhoneNormalizer.DigitsOnly(request.AppFax),
            ContactPhone = PhoneNormalizer.DigitsOnly(request.ContactPhone),
            ContactCell = PhoneNormalizer.DigitsOnly(request.ContactCell)
        };
        await _applicationRepository.UpdateApplicantInfoAsync(appId, request, updatedBy, cancellationToken);
        return await GetByIdAsync(appId, cancellationToken);
    }

    public async Task<ApplicationDto?> UpdateHazardAsync(string appId, UpdateHazardRequest request, string updatedBy, CancellationToken cancellationToken = default)
    {
        request = request with
        {
            ConsultantPhone = PhoneNormalizer.DigitsOnly(request.ConsultantPhone),
            ConsultantCell = PhoneNormalizer.DigitsOnly(request.ConsultantCell),
            SafetyOfficerPhone = PhoneNormalizer.DigitsOnly(request.SafetyOfficerPhone),
            SafetyOfficerCell = PhoneNormalizer.DigitsOnly(request.SafetyOfficerCell),
            InfoProvidedByPhone = PhoneNormalizer.DigitsOnly(request.InfoProvidedByPhone)
        };
        await _applicationRepository.UpdateHazardAsync(appId, request, updatedBy, cancellationToken);
        return await GetByIdAsync(appId, cancellationToken);
    }

    public async Task<ApplicationDto?> UpdateWorkAsync(string appId, int workId, UpdateWorkRequest request, string updatedBy, CancellationToken cancellationToken = default)
    {
        await _applicationRepository.UpdateWorkAsync(appId, workId, request, updatedBy, cancellationToken);
        return await GetByIdAsync(appId, cancellationToken);
    }

    public async Task<ApplicationDto?> AddWorkAsync(string appId, AddWorkRequest request, string addedBy, CancellationToken cancellationToken = default)
    {
        var application = await _applicationRepository.GetByIdAsync(appId, cancellationToken);
        if (application is null)
        {
            return null;
        }

        // A cancelled or already-approved application is terminal — its work list is fixed.
        if (NonCancellableAppStatuses.Contains(application.StatusCode))
        {
            throw new InvalidOperationException($"Work cannot be added while the application status is {application.StatusCode}.");
        }

        await _applicationRepository.AddWorkAsync(appId, request, addedBy, cancellationToken);
        return await GetByIdAsync(appId, cancellationToken);
    }

    public async Task<ApplicationDto?> CancelWorkAsync(string appId, int workId, string updatedBy, CancellationToken cancellationToken = default)
    {
        var application = await _applicationRepository.GetByIdAsync(appId, cancellationToken);
        if (application is null)
        {
            return null;
        }

        if (NonCancellableAppStatuses.Contains(application.StatusCode))
        {
            throw new InvalidOperationException($"Work cannot be cancelled while the application status is {application.StatusCode}.");
        }

        var cancelled = await _applicationRepository.CancelWorkAsync(appId, workId, updatedBy, cancellationToken);
        return cancelled ? await GetByIdAsync(appId, cancellationToken) : null;
    }

    public async Task<ApplicationDto?> DeleteWorkAsync(string appId, int workId, string updatedBy, CancellationToken cancellationToken = default)
    {
        var application = await _applicationRepository.GetByIdAsync(appId, cancellationToken);
        if (application is null)
        {
            return null;
        }

        if (NonCancellableAppStatuses.Contains(application.StatusCode))
        {
            throw new InvalidOperationException($"Work cannot be deleted while the application status is {application.StatusCode}.");
        }

        // Never delete the last remaining work — every application must keep at least one work item.
        if (application.Works.Count <= 1)
        {
            throw new InvalidOperationException("The last remaining work cannot be deleted.");
        }

        var deleted = await _applicationRepository.DeleteWorkAsync(appId, workId, cancellationToken);
        return deleted ? await GetByIdAsync(appId, cancellationToken) : null;
    }

    public async Task<ApplicationDto?> UpdateWcrAsync(string appId, int workId, WcrUpdateRequest request, string updatedBy, CancellationToken cancellationToken = default)
    {
        await EnsureApprovedAsync(appId, "Well Completion Report", cancellationToken);
        await _applicationRepository.UpdateWcrAsync(appId, workId, request, updatedBy, cancellationToken);
        return await GetByIdAsync(appId, cancellationToken);
    }

    // The "Enter WCR" / "Enter GeoLog" screens are only reachable once the application is approved,
    // mirroring the legacy search_detail.jsp guard (buttons render only when statusCode == "APPRV").
    private async Task EnsureApprovedAsync(string appId, string action, CancellationToken cancellationToken)
    {
        var application = await _applicationRepository.GetByIdAsync(appId, cancellationToken)
            ?? throw new KeyNotFoundException($"Application {appId} was not found.");
        if (!string.Equals(application.StatusCode, "APPRV", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{action} entry is only allowed after the application is approved (current status {application.StatusCode}).");
        }
    }

    public async Task<ApplicationDto?> UpdateApprovalDetailsAsync(string appId, UpdateApprovalDetailsRequest request, string updatedBy, CancellationToken cancellationToken = default)
    {
        await _applicationRepository.UpdateApprovalDetailsAsync(appId, request, updatedBy, cancellationToken);
        return await GetByIdAsync(appId, cancellationToken);
    }

    // Status codes that block cancellation, mirroring the legacy search_detail.jsp button guard:
    // an approved or already-cancelled application, or one whose payment is PAID / PAYFL, cannot be cancelled.
    private static readonly HashSet<string> NonCancellableAppStatuses = new(StringComparer.OrdinalIgnoreCase) { "CAN", "APPRV" };
    private static readonly HashSet<string> NonCancellablePayStatuses = new(StringComparer.OrdinalIgnoreCase) { "PAID", "PAYFL" };

    public async Task<ApplicationDto?> CancelAsync(string appId, string cancelledBy, CancellationToken cancellationToken = default)
    {
        var application = await _applicationRepository.GetByIdAsync(appId, cancellationToken);
        if (application is null)
        {
            return null;
        }

        if (NonCancellableAppStatuses.Contains(application.StatusCode))
        {
            throw new InvalidOperationException($"Application {appId} cannot be cancelled while its status is {application.StatusCode}.");
        }

        var payment = await _paymentRepository.GetByAppIdAsync(appId, cancellationToken);
        if (payment is not null && NonCancellablePayStatuses.Contains(payment.StatusCode))
        {
            throw new InvalidOperationException($"Application {appId} cannot be cancelled while its payment status is {payment.StatusCode}.");
        }

        await _applicationRepository.CancelApplicationAsync(appId, cancelledBy, cancellationToken);

        // Best-effort applicant notification — a mail failure must not undo the cancellation.
        await _notifications.SendApplicationCancelledAsync(application, cancelledBy, cancellationToken);

        return await GetByIdAsync(appId, cancellationToken);
    }

    public Task<PermitInfoDto?> GetPermitInfoAsync(string appId, CancellationToken cancellationToken = default)
        => _applicationRepository.GetPermitInfoAsync(appId, cancellationToken);

    public async Task<ApplicationDto> SubmitAsync(SubmitApplicationRequest request, CancellationToken cancellationToken = default)
    {
        var appId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

        // Authoritative site-extra rate (WORK_TYPES system/siteExtra). Charged per well drilled beyond
        // a tiered "site" work type's site_max; looked up server-side so the client cannot override it.
        var siteExtraRate = await _applicationRepository.GetSiteExtraRateAsync(cancellationToken);

        var works = new List<ApplicationWork>();
        var workIndex = 1;
        foreach (var work in request.Works)
        {
            var isSiteUnit = string.Equals(work.WorkFeeUnit?.Trim(), "site", StringComparison.OrdinalIgnoreCase);
            var workSiteMax = work.WorkSiteMax ?? 0;
            var domainWork = new ApplicationWork
            {
                WorkId = workIndex,
                AppId = appId,
                WorkCategory = work.WorkCategory,
                WorkType = work.WorkType,
                WellUseType = work.WellUseType,
                DrillerName = work.DrillerName,
                DrillerLicenseNum = work.DrillerLicenseNum,
                DrillMethodType = work.DrillMethodType,
                DrillMethodOtherDesc = work.DrillMethodOtherDesc,
                WorkFeeRate = work.WorkFeeRate ?? 0m,
                WorkFeeUnit = work.WorkFeeUnit,
                WorkSiteMax = workSiteMax,
                WorkSiteExtraRate = (isSiteUnit && workSiteMax > 0) ? siteExtraRate : 0m,
                StatusCode = "PENDC"
            };

            var specIndex = 1;
            foreach (var spec in work.Specs)
            {
                domainWork.Specs.Add(new ApplicationWorkSpec
                {
                    WorkSpecsId = specIndex,
                    WorkId = workIndex,
                    AppId = appId,
                    OwnerWellNum = spec.OwnerWellNum,
                    DrillCount = spec.DrillCount is > 0 ? spec.DrillCount : 1,
                    HoleDiamIn = spec.HoleDiamIn,
                    CasingDiamIn = spec.CasingDiamIn,
                    SealDepthFt = spec.SealDepthFt,
                    MaxDepthFt = spec.MaxDepthFt,
                    Latitude = spec.Latitude,
                    Longitude = spec.Longitude,
                    StateWellId = spec.StateWellId,
                    DwrNum = spec.DwrNum,
                    PermitNum = spec.PermitNum,
                    StatusCode = "PEND"
                });
                specIndex++;
            }

            works.Add(domainWork);
            workIndex++;
        }

        var application = new DomainApplication
        {
            AppId = appId,
            StatusCode = "PENDS",
            AddDate = DateTime.UtcNow,
            AddBy = request.AddBy,
            SiteCityCode = request.SiteCityCode,
            SiteCityName = request.SiteCityName,
            SiteLocation = request.SiteLocation,
            SiteLat = request.SiteLat,
            SiteLong = request.SiteLong,
            ProjStartDate = request.ProjStartDate,
            ProjEndDate = request.ProjEndDate,
            SiteHazardRequired = request.SiteHazardRequired,
            SitemapFilename = request.SitemapFilename,
            Applicant = new Applicant
            {
                AppBusinessName = request.AppBusinessName,
                AppLastName = request.AppLastName,
                AppFirstName = request.AppFirstName,
                AppEmailAddr = request.AppEmailAddr,
                AppAddrStreet = request.AppAddrStreet,
                AppAddrStreet2 = request.AppAddrStreet2,
                AppAddrCity = request.AppAddrCity,
                AppAddrState = request.AppAddrState,
                AppAddrZip = request.AppAddrZip,
                AppPhone = PhoneNormalizer.DigitsOnly(request.AppPhone),
                AppFax = PhoneNormalizer.DigitsOnly(request.AppFax)
            },
            Contact = new ContactInfo
            {
                ContactLastName = request.ContactLastName,
                ContactFirstName = request.ContactFirstName,
                ContactEmail = request.ContactEmail,
                ContactPhone = PhoneNormalizer.DigitsOnly(request.ContactPhone),
                ContactCell = PhoneNormalizer.DigitsOnly(request.ContactCell)
            },
            Owner = new PartyInfo
            {
                LastName = request.OwnerLastName,
                FirstName = request.OwnerFirstName,
                AddrStreet = request.OwnerAddrStreet,
                AddrCity = request.OwnerAddrCity,
                AddrState = request.OwnerAddrState,
                AddrZip = request.OwnerAddrZip,
                Phone = PhoneNormalizer.DigitsOnly(request.OwnerPhone),
                Email = request.OwnerEmail
            },
            Client = new PartyInfo
            {
                LastName = request.ClientLastName,
                FirstName = request.ClientFirstName,
                AddrStreet = request.ClientAddrStreet,
                AddrCity = request.ClientAddrCity,
                AddrState = request.ClientAddrState,
                AddrZip = request.ClientAddrZip,
                Phone = PhoneNormalizer.DigitsOnly(request.ClientPhone),
                Email = request.ClientEmail
            },
            Works = works
        };

        application.Hazard = BuildHazard(appId, request.Hazard);
        application.Documents = BuildDocuments(appId, request.Documents);
        application.EmailCcs = BuildEmailCcs(appId, request.EmailCcs);
        application.Notes = BuildNotes(appId, request.AddBy, request.Notes);

        _logger.LogInformation("Submitting application {AppId} for {ApplicantEmail}", appId, request.AppEmailAddr);
        var created = await _applicationRepository.CreateAsync(application, cancellationToken);

        // Public submit mirrors legacy ProcessAppServlet status codes:
        //   application -> PENDS, works -> PENDC, work specs -> PEND.
        // Payment status by type: EXMPT -> EXMPT (paid 0); CASH and CHECK w/o check# -> PENDP
        //   ("Pending Payment"); CC (and CHECK w/ #) -> PEND ("Pending Approval").
        // CC card is vaulted for $0 here; the real fee is charged later by the intra app.
        var authAmount = CalculateAuthAmount(works);

        // For CC, vault the card for $0 (pre-authorization). The returned custid is stored encrypted
        // in AUTH_ID_ENCR by the payment repository. Runs in mock mode offline (guarded in the gateway).
        string? vaultedCustId = null;
        if (string.Equals(request.PaymentType, "CC", StringComparison.OrdinalIgnoreCase))
        {
            var vault = await _intelliPayGateway.VaultZeroDollarAsync(appId, cancellationToken);
            vaultedCustId = vault.CustomerId;
            _logger.LogInformation("Vaulted card for application {AppId} (approved={Approved}); status remains PEND", appId, vault.Approved);
        }

        var paymentType = string.IsNullOrWhiteSpace(request.PaymentType) ? "CC" : request.PaymentType;
        var checkNum = request.CheckNum ?? string.Empty;
        string paymentStatus;
        if (string.Equals(paymentType, "EXMPT", StringComparison.OrdinalIgnoreCase))
        {
            paymentStatus = "EXMPT";
        }
        else if (string.Equals(paymentType, "CASH", StringComparison.OrdinalIgnoreCase))
        {
            // Public cash applications collect no payer name or amount up front — they stay
            // "Pending Payment" (PENDP) until intra staff record the received cash via Update Payment,
            // which advances the payment to PEND ("Pending Approval").
            paymentStatus = "PENDP";
        }
        else if (string.Equals(paymentType, "CHECK", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(checkNum))
        {
            paymentStatus = "PENDP";
        }
        else
        {
            paymentStatus = "PEND";
        }

        var payment = new AppPayment
        {
            AppId = appId,
            PaymentType = paymentType,
            AuthIdEncr = vaultedCustId,
            AuthAmount = authAmount,
            PaidAmount = 0m,
            FineAmount = 0m,
            ServiceCharge = 0m,
            CheckNum = string.IsNullOrEmpty(checkNum) ? null : checkNum,
            AcctName = request.CheckAcctName,
            StatusCode = paymentStatus,
            AddBy = request.AddBy
        };
        await _paymentRepository.UpsertAsync(payment, cancellationToken);

        // Persist an inspection slot selection when the applicant chose one on the wizard.
        if (request.InspectionDate.HasValue && request.InspectionSlotId.HasValue)
        {
            var inspection = new Inspection
            {
                InspectionDate = request.InspectionDate.Value.Date,
                SlotId = request.InspectionSlotId.Value,
                AppId = appId,
                InspectionDateTime = request.InspectionDate.Value,
                StatusCode = "IRSRV"
            };
            _logger.LogInformation("Persisting selected inspection slot {SlotId} on {Date:yyyy-MM-dd} for application {AppId}", inspection.SlotId, inspection.InspectionDate, appId);
            await _inspectionRepository.AddAsync(inspection, cancellationToken);
        }

        // Applicant confirmation email (best-effort). For a credit-card application the applicant has
        // NOT yet completed the IntelliPay lightbox at this point — this call only creates the record
        // and opens the popup — so emailing now would confirm before the card is even entered. The CC
        // confirmation is therefore deferred to PaymentService.PreAuthorizeAsync, which runs once the
        // card is vaulted (matching legacy ProcessAppServlet timing). CHECK and fee-exempt applications
        // have no lightbox step, so their confirmation is sent here on submit.
        if (!string.Equals(paymentType, "CC", StringComparison.OrdinalIgnoreCase))
        {
            // Reload the persisted graph so the confirmation email shows work/well-use descriptions
            // (the submit request only carries codes). Fall back to the in-memory graph on any error.
            var confirmApp = application;
            try
            {
                var reloaded = await _applicationRepository.GetByIdAsync(appId, cancellationToken);
                if (reloaded is not null) confirmApp = reloaded;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Reload for confirmation email failed for application {AppId}; using in-memory graph", appId);
            }
            await _notifications.SendApplicationConfirmationAsync(confirmApp, authAmount, paymentType, checkNum, cancellationToken);
        }

        return Map(created);
    }

    // Permit base fee stored in auth_amount at submit. Uses the shared legacy-parity calculator so
    // the fee multiplies by the number of wells (Σ drill_count) — not the number of spec rows — and
    // matches the amount later recharged at approval. Legacy BeanAppWrk keys off the "site" unit; any
    // other unit (e.g. "well") is charged per-well.
    private static decimal CalculateAuthAmount(IEnumerable<ApplicationWork> works) => FeeCalculator.BaseFee(works);

    private static HazardInfo? BuildHazard(string appId, SubmitHazardRequest? request)
    {
        if (request is null || !request.Present)
        {
            return null;
        }

        var hazard = new HazardInfo
        {
            AppId = appId,
            ConsultantFirstName = request.ConsultantFirstName,
            ConsultantLastName = request.ConsultantLastName,
            ConsultantPhone = PhoneNormalizer.DigitsOnly(request.ConsultantPhone),
            ConsultantCell = PhoneNormalizer.DigitsOnly(request.ConsultantCell),
            SafetyOfficerFirstName = request.SafetyOfficerFirstName,
            SafetyOfficerLastName = request.SafetyOfficerLastName,
            SafetyOfficerPhone = PhoneNormalizer.DigitsOnly(request.SafetyOfficerPhone),
            SafetyOfficerCell = PhoneNormalizer.DigitsOnly(request.SafetyOfficerCell),
            FacilityType = request.FacilityType,
            SiteSafetyMeetingDateTime = CombineDateTime(request.SiteSafetyMeetingDate, request.SiteSafetyMeetingTime),
            PpeLevelA = NormalizeYn(request.PpeLevelA),
            PpeLevelB = NormalizeYn(request.PpeLevelB),
            PpeLevelC = NormalizeYn(request.PpeLevelC),
            PpeLevelD = NormalizeYn(request.PpeLevelD),
            EquipHardHatFlag = NormalizeFlag(request.EquipHardHatFlag),
            EquipSafetyShoesFlag = NormalizeFlag(request.EquipSafetyShoesFlag),
            EquipOrangeVestFlag = NormalizeFlag(request.EquipOrangeVestFlag),
            EquipHearingProtFlag = NormalizeFlag(request.EquipHearingProtFlag),
            EquipSafetyEyewearFlag = NormalizeFlag(request.EquipSafetyEyewearFlag),
            EquipClothingFlag = NormalizeFlag(request.EquipClothingFlag),
            EquipClothingDesc = request.EquipClothingDesc,
            EquipRespiratorFlag = NormalizeFlag(request.EquipRespiratorFlag),
            EquipRespiratorDesc = request.EquipRespiratorDesc,
            EquipCartridgeFlag = NormalizeFlag(request.EquipCartridgeFlag),
            EquipCartridgeDesc = request.EquipCartridgeDesc,
            EquipGlovesFlag = NormalizeFlag(request.EquipGlovesFlag),
            EquipGlovesDesc = request.EquipGlovesDesc,
            EquipOtherFlag = NormalizeFlag(request.EquipOtherFlag),
            EquipOtherDesc = request.EquipOtherDesc,
            InfoProvidedByCompanyName = request.InfoProvidedByCompanyName,
            InfoProvidedByLastName = request.InfoProvidedByLastName,
            InfoProvidedByFirstName = request.InfoProvidedByFirstName,
            InfoProvidedByTitle = request.InfoProvidedByTitle,
            InfoProvidedByPhone = PhoneNormalizer.DigitsOnly(request.InfoProvidedByPhone),
            Acknowledgement = request.Acknowledgement ? "Y" : "N"
        };

        hazard.Contaminations = request.Contaminants
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var seq = 1;
        foreach (var substance in request.Substances)
        {
            hazard.Substances.Add(new HazardSubstance
            {
                AppId = appId,
                HazardSeq = seq++,
                ConcentrationsPpm = substance.Concentration,
                PelPpm = substance.PelPpm,
                HealthEffects = substance.HealthEffects
            });
        }

        return hazard;
    }

    private static DateTime? CombineDateTime(DateTime? date, string? time)
    {
        if (!date.HasValue)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(time) && TimeSpan.TryParse(time, out var parsed))
        {
            return date.Value.Date.Add(parsed);
        }

        return date.Value;
    }

    // PPE checkbox -> 'Y'/'N'
    private static string NormalizeYn(string? value)
        => string.Equals(value, "Y", StringComparison.OrdinalIgnoreCase) ? "Y" : "N";

    // Equipment tri-state -> 'R' (required), 'A' (available), or null
    private static string? NormalizeFlag(string? value)
    {
        if (string.Equals(value, "R", StringComparison.OrdinalIgnoreCase)) return "R";
        if (string.Equals(value, "A", StringComparison.OrdinalIgnoreCase)) return "A";
        return null;
    }

    private static List<AppDocumentLink> BuildDocuments(string appId, IReadOnlyList<SubmitDocumentRequest> documents)
    {
        var links = new List<AppDocumentLink>();
        var seq = 1;
        foreach (var document in documents)
        {
            if (string.IsNullOrWhiteSpace(document.FileName))
            {
                continue;
            }

            links.Add(new AppDocumentLink
            {
                AppId = appId,
                SeqNum = seq++,
                DocumentType = document.DocumentType,
                DocumentFilename = document.FileName
            });
        }

        return links;
    }

    private static List<AppEmailCc> BuildEmailCcs(string appId, IReadOnlyList<string> emailCcs)
    {
        var results = new List<AppEmailCc>();
        var id = 1;
        foreach (var email in emailCcs)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                continue;
            }

            results.Add(new AppEmailCc
            {
                AppId = appId,
                EmailCcId = id++,
                EmailAddrCc = email.Trim()
            });
        }

        return results;
    }

    private static List<AppNote> BuildNotes(string appId, string addBy, IReadOnlyList<string> notes)
    {
        var results = new List<AppNote>();
        var id = 1;
        foreach (var note in notes)
        {
            if (string.IsNullOrWhiteSpace(note))
            {
                continue;
            }

            results.Add(new AppNote
            {
                AppId = appId,
                NoteId = id++,
                AddBy = addBy,
                AddTs = DateTime.UtcNow,
                NotesText = note
            });
        }

        return results;
    }

    private static ApplicationDto Map(DomainApplication application) => new()
    {
        AppId = application.AppId,
        StatusCode = application.StatusCode,
        AddDate = application.AddDate,
        AddBy = application.AddBy,
        SiteCityCode = application.SiteCityCode,
        SiteCityName = application.SiteCityName,
        SiteLocation = application.SiteLocation,
        SiteLat = application.SiteLat,
        SiteLong = application.SiteLong,
        ProjStartDate = application.ProjStartDate,
        ProjEndDate = application.ProjEndDate,
        SiteHazardRequired = application.SiteHazardRequired,
        SiteVisitType = application.SiteVisitType,
        SitemapFilename = application.SitemapFilename,
        SitemapReceivedDate = application.SitemapReceivedDate,
        ExtendStartDate = application.ExtendStartDate,
        ExtendEndDate = application.ExtendEndDate,
        ExtendCount = application.ExtendCount,
        ExtendBy = application.ExtendBy,
        AppBusinessName = application.Applicant.AppBusinessName,
        AppLastName = application.Applicant.AppLastName,
        AppFirstName = application.Applicant.AppFirstName,
        AppEmailAddr = application.Applicant.AppEmailAddr,
        AppAddrStreet = application.Applicant.AppAddrStreet,
        AppAddrStreet2 = application.Applicant.AppAddrStreet2,
        AppAddrCity = application.Applicant.AppAddrCity,
        AppAddrState = application.Applicant.AppAddrState,
        AppAddrZip = application.Applicant.AppAddrZip,
        AppPhone = application.Applicant.AppPhone,
        AppFax = application.Applicant.AppFax,
        ContactLastName = application.Contact.ContactLastName,
        ContactFirstName = application.Contact.ContactFirstName,
        ContactEmail = application.Contact.ContactEmail,
        ContactPhone = application.Contact.ContactPhone,
        ContactCell = application.Contact.ContactCell,
        OwnerLastName = application.Owner.LastName,
        OwnerFirstName = application.Owner.FirstName,
        OwnerAddrStreet = application.Owner.AddrStreet,
        OwnerAddrCity = application.Owner.AddrCity,
        OwnerAddrState = application.Owner.AddrState,
        OwnerAddrZip = application.Owner.AddrZip,
        OwnerPhone = application.Owner.Phone,
        OwnerEmail = application.Owner.Email,
        ClientLastName = application.Client.LastName,
        ClientFirstName = application.Client.FirstName,
        ClientAddrStreet = application.Client.AddrStreet,
        ClientAddrCity = application.Client.AddrCity,
        ClientAddrState = application.Client.AddrState,
        ClientAddrZip = application.Client.AddrZip,
        ClientPhone = application.Client.Phone,
        ClientEmail = application.Client.Email,
        Works = application.Works.Select(MapWork).ToList(),
        WorkTypes = application.Works.Select(w => w.WorkType).ToList(),
        Hazard = MapHazard(application.Hazard),
        Documents = application.Documents
            .Select(d => new DocumentLinkDto(d.SeqNum, d.DocumentType, d.OtherTypeDesc, d.DocumentFilename))
            .ToList(),
        EmailCcs = application.EmailCcs.Select(c => c.EmailAddrCc).ToList(),
        Notes = application.Notes.Select(n => n.NotesText ?? string.Empty).ToList()
    };

    private static HazardDto? MapHazard(HazardInfo? hazard)
    {
        if (hazard is null)
        {
            return null;
        }

        return new HazardDto
        {
            ConsultantFirstName = hazard.ConsultantFirstName,
            ConsultantLastName = hazard.ConsultantLastName,
            ConsultantPhone = hazard.ConsultantPhone,
            ConsultantCell = hazard.ConsultantCell,
            SafetyOfficerFirstName = hazard.SafetyOfficerFirstName,
            SafetyOfficerLastName = hazard.SafetyOfficerLastName,
            SafetyOfficerPhone = hazard.SafetyOfficerPhone,
            SafetyOfficerCell = hazard.SafetyOfficerCell,
            FacilityType = hazard.FacilityType,
            SiteSafetyMeetingDateTime = hazard.SiteSafetyMeetingDateTime,
            AddTs = hazard.AddTs,
            PpeLevelA = hazard.PpeLevelA,
            PpeLevelB = hazard.PpeLevelB,
            PpeLevelC = hazard.PpeLevelC,
            PpeLevelD = hazard.PpeLevelD,
            EquipHardHatFlag = hazard.EquipHardHatFlag,
            EquipSafetyShoesFlag = hazard.EquipSafetyShoesFlag,
            EquipOrangeVestFlag = hazard.EquipOrangeVestFlag,
            EquipHearingProtFlag = hazard.EquipHearingProtFlag,
            EquipSafetyEyewearFlag = hazard.EquipSafetyEyewearFlag,
            EquipClothingFlag = hazard.EquipClothingFlag,
            EquipClothingDesc = hazard.EquipClothingDesc,
            EquipRespiratorFlag = hazard.EquipRespiratorFlag,
            EquipRespiratorDesc = hazard.EquipRespiratorDesc,
            EquipCartridgeFlag = hazard.EquipCartridgeFlag,
            EquipCartridgeDesc = hazard.EquipCartridgeDesc,
            EquipGlovesFlag = hazard.EquipGlovesFlag,
            EquipGlovesDesc = hazard.EquipGlovesDesc,
            EquipOtherFlag = hazard.EquipOtherFlag,
            EquipOtherDesc = hazard.EquipOtherDesc,
            InfoProvidedByCompanyName = hazard.InfoProvidedByCompanyName,
            InfoProvidedByLastName = hazard.InfoProvidedByLastName,
            InfoProvidedByFirstName = hazard.InfoProvidedByFirstName,
            InfoProvidedByTitle = hazard.InfoProvidedByTitle,
            InfoProvidedByPhone = hazard.InfoProvidedByPhone,
            Acknowledgement = hazard.Acknowledgement,
            Contaminants = hazard.Contaminations.ToList(),
            Substances = hazard.Substances
                .Select(s => new HazardSubstanceDto(s.ConcentrationsPpm, s.PelPpm, s.HealthEffects))
                .ToList()
        };
    }

    private static ApplicationWorkDto MapWork(ApplicationWork work) => new(
        work.WorkId,
        work.WorkCategory,
        work.WorkType,
        work.WellUseType,
        work.WorkCategoryDesc,
        work.WorkTypeDesc,
        work.WellUseDesc,
        work.DrillMethodName,
        work.DrillerName,
        work.DrillerLicenseNum,
        work.DrillMethodType,
        work.DrillMethodOtherDesc,
        work.WorkFeeRate,
        work.WorkFeeUnit,
        work.WorkSiteMax,
        work.WorkSiteExtraRate,
        work.StatusCode,
        work.Specs.Select(MapSpec).ToList());

    private static ApplicationWorkSpecDto MapSpec(ApplicationWorkSpec spec) => new(
        spec.WorkSpecsId,
        spec.WorkId,
        spec.OwnerWellNum,
        spec.DrillCount,
        spec.HoleDiamIn,
        spec.CasingDiamIn,
        spec.SealDepthFt,
        spec.MaxDepthFt,
        spec.Latitude,
        spec.Longitude,
        spec.StateWellId,
        spec.DwrNum,
        spec.PermitNum,
        spec.ComplWellDwrNum,
        spec.DwrNumShared,
        spec.DwrImage,
        spec.GeologFile,
        spec.StatusCode);
}
