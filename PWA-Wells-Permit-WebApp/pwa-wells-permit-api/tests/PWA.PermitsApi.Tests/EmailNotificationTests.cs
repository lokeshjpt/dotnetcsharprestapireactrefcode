using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PWA.PermitsApi.Application.Configuration;
using PWA.PermitsApi.Application.DTOs;
using PWA.PermitsApi.Application.Notifications;
using PWA.PermitsApi.Application.Services;
using PWA.PermitsApi.Domain.Models;
using DomainApplication = PWA.PermitsApi.Domain.Models.Application;

namespace PWA.PermitsApi.Tests;

/// <summary>Minimal work-permit fake for approval wiring tests.</summary>
internal sealed class FakeWorkPermitRepository : PWA.PermitsApi.Application.Interfaces.Repositories.IWorkPermitRepository
{
    public List<WorkPermit> Permits { get; set; } = new()
    {
        new WorkPermit { PermitNumber = "WP-0001", AppId = "1234567890123", WorkId = 1 }
    };

    public Task<IReadOnlyList<WorkPermit>> GeneratePermitsAsync(string appId, DateTime permitExpiryDate, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<WorkPermit>>(Permits);
}

public sealed class EmailNotificationWiringTests
{
    private static SubmitApplicationRequest CcRequest(string paymentType = "CC") => new()
    {
        AppFirstName = "Tina",
        AppLastName = "Lopez",
        AppEmailAddr = "tina@example.com",
        SiteLocation = "123 Main St",
        SiteCityCode = "OAK",
        SiteCityName = "Oakland",
        PaymentType = paymentType,
        Works = new List<SubmitWorkRequest>
        {
            new()
            {
                WorkCategory = "con",
                WorkType = "conwell",
                WorkFeeRate = 250m,
                WorkFeeUnit = "EA",
                Specs = new List<SubmitWorkSpecRequest> { new() { OwnerWellNum = "W-1", DrillCount = 1 } }
            }
        }
    };

    [Fact]
    public async Task SubmitAsync_Cc_DefersConfirmationUntilVault()
    {
        // For a credit-card application, submit only creates the record and opens the IntelliPay
        // lightbox — the applicant confirmation must NOT be sent yet (it is deferred to the post-vault
        // PaymentService.PreAuthorizeAsync so no email goes out while the popup is being filled).
        var notifications = new FakePermitNotificationService();
        var service = new ApplicationService(
            new FakeApplicationRepository(), new FakePaymentRepository(), new FakeInspectionRepository(),
            new FakeIntelliPayGateway(), notifications, NullLogger<ApplicationService>.Instance);

        await service.SubmitAsync(CcRequest());

        Assert.DoesNotContain(notifications.Sent, s => s.Scenario == "confirmation");
        Assert.DoesNotContain(notifications.Sent, s => s.Scenario == "ccPreAuthAudit");
    }

    [Fact]
    public async Task SubmitAsync_Check_SendsConfirmationOnly()
    {
        var notifications = new FakePermitNotificationService();
        var service = new ApplicationService(
            new FakeApplicationRepository(), new FakePaymentRepository(), new FakeInspectionRepository(),
            new FakeIntelliPayGateway(), notifications, NullLogger<ApplicationService>.Instance);

        var request = CcRequest("CHECK");
        await service.SubmitAsync(request);

        Assert.Contains(notifications.Sent, s => s.Scenario == "confirmation");
        Assert.DoesNotContain(notifications.Sent, s => s.Scenario == "ccPreAuthAudit");
    }

    [Fact]
    public async Task ApproveAsync_SendsApprovalNotificationWithPermitPdfAttachment()
    {
        var repo = new FakeApplicationRepository
        {
            ByIdResult = new DomainApplication
            {
                AppId = "1234567890123",
                StatusCode = "PEND",
                Applicant = new Applicant { AppEmailAddr = "tina@example.com" }
            }
        };
        var notifications = new FakePermitNotificationService();
        var docService = new FakePermitDocumentService();
        var service = new ApprovalService(
            repo, new FakeWorkPermitRepository(), new FakePaymentRepository(),
            new ConditionsService(new FakeConditionsRepository(), NullLogger<ConditionsService>.Instance),
            docService, new FakeFileTransferService(),
            new PaymentService(new FakePaymentRepository(), repo, new FakeIntelliPayGateway(), notifications, NullLogger<PaymentService>.Instance),
            notifications, NullLogger<ApprovalService>.Instance);

        var result = await service.ApproveAsync("1234567890123", new ApprovalRequest { ApprovedBy = "staff" });

        Assert.True(result.Success);
        Assert.Contains(notifications.Sent, s => s.Scenario == "approval");
        Assert.Equal(1, docService.Calls);
        Assert.NotNull(notifications.LastApprovalAttachments);
        Assert.Contains(notifications.LastApprovalAttachments!, a => a.FileName == "Permit_1234567890123.pdf" && a.ContentType == "application/pdf");
    }

    [Fact]
    public async Task ApproveAsync_AttachesUploadedSitemapWhenAvailable()
    {
        var repo = new FakeApplicationRepository
        {
            ByIdResult = new DomainApplication
            {
                AppId = "1234567890123",
                StatusCode = "PEND",
                SitemapFilename = @"\\share\pwa\1234567890123_20260101_map.pdf",
                Applicant = new Applicant { AppEmailAddr = "tina@example.com" }
            }
        };
        var notifications = new FakePermitNotificationService();
        var ftp = new FakeFileTransferService { DownloadResult = System.Text.Encoding.ASCII.GetBytes("%PDF-1.4 sitemap") };
        var service = new ApprovalService(
            repo, new FakeWorkPermitRepository(), new FakePaymentRepository(),
            new ConditionsService(new FakeConditionsRepository(), NullLogger<ConditionsService>.Instance),
            new FakePermitDocumentService(), ftp,
            new PaymentService(new FakePaymentRepository(), repo, new FakeIntelliPayGateway(), notifications, NullLogger<PaymentService>.Instance),
            notifications, NullLogger<ApprovalService>.Instance);

        await service.ApproveAsync("1234567890123", new ApprovalRequest { ApprovedBy = "staff" });

        Assert.Equal(1, ftp.DownloadCalls);
        Assert.NotNull(notifications.LastApprovalAttachments);
        Assert.Contains(notifications.LastApprovalAttachments!, a => a.FileName == "1234567890123_20260101_map.pdf");
    }

    [Fact]
    public async Task ApproveAsync_ChargesCcAndMarksPaidBeforeEmail()
    {
        // The emailed permit must reflect the finalized payment, so for a CC application the vaulted
        // card is charged (receipt assigned, paid amount incl. fine, status PAID) BEFORE the approval
        // email is built — matching what staff see when they print the permit later. If the charge
        // could not be finalized the approval would throw and no permit email would be sent.
        var repo = new FakeApplicationRepository
        {
            ByIdResult = new DomainApplication
            {
                AppId = "1234567890123",
                StatusCode = "PEND",
                Applicant = new Applicant { AppEmailAddr = "tina@example.com" }
            }
        };
        var paymentRepo = new FakePaymentRepository
        {
            NextReceipt = "WR2026-0099",
            Existing = new AppPayment { AppId = "1234567890123", PaymentType = "CC", AuthIdEncr = "87654321", StatusCode = "PEND", AuthAmount = 660m, FineAmount = 0m }
        };
        var gateway = new FakeIntelliPayGateway { ChargeResponse = "{\"response\":\"A\",\"paymentid\":\"PID1\",\"authcode\":\"AC1\"}" };
        var notifications = new FakePermitNotificationService();
        var paymentService = new PaymentService(paymentRepo, repo, gateway, notifications, NullLogger<PaymentService>.Instance);
        var service = new ApprovalService(
            repo, new FakeWorkPermitRepository(), paymentRepo,
            new ConditionsService(new FakeConditionsRepository(), NullLogger<ConditionsService>.Instance),
            new FakePermitDocumentService(), new FakeFileTransferService(), paymentService, notifications, NullLogger<ApprovalService>.Instance);

        await service.ApproveAsync("1234567890123", new ApprovalRequest { ApprovedBy = "staff", FineAmount = 445m });

        // Payment finalized in the DB before the email: PAID, receipt assigned, fine + paid amount set.
        Assert.NotNull(paymentRepo.Saved);
        Assert.Equal("PAID", paymentRepo.Saved!.StatusCode);
        Assert.Equal("WR2026-0099", paymentRepo.Saved!.ReceiptNum);
        Assert.Equal(445m, paymentRepo.Saved!.FineAmount);
        // Paid amount = recomputed base (no works → 0) + service (0) + fine (445).
        Assert.Equal(445m, paymentRepo.Saved!.PaidAmount);
        Assert.Contains(notifications.Sent, s => s.Scenario == "approval");
    }

    [Fact]
    public async Task ChargeAsync_CcApproved_DoesNotSendAudit()
    {
        // Successful/approved CC charges are not audit-emailed — only payment errors/declines are.
        var repo = new FakePaymentRepository
        {
            NextReceipt = "WR2026-0042",
            Existing = new AppPayment { AppId = "1234567890123", PaymentType = "CC", AuthIdEncr = "87654321", AuthAmount = 660m, StatusCode = "PEND" }
        };
        var gateway = new FakeIntelliPayGateway { ChargeResponse = "{\"response\":\"A\",\"paymentid\":\"PID777\",\"authcode\":\"AC12\"}" };
        var notifications = new FakePermitNotificationService();
        var service = new PaymentService(repo, new FakeApplicationRepository(), gateway, notifications, NullLogger<PaymentService>.Instance);

        await service.ChargeAsync(new ChargeRequest { AppId = "1234567890123", CaptureAmount = 660m });

        Assert.DoesNotContain(notifications.Sent, s => s.Scenario == "ccChargeApprovedAudit");
    }

    [Fact]
    public async Task ChargeAsync_CcDeclined_SendsFailedAuditAndCustomerDecline()
    {
        var repo = new FakePaymentRepository
        {
            Existing = new AppPayment { AppId = "1234567890123", PaymentType = "CC", AuthIdEncr = "87654321", AuthAmount = 660m, StatusCode = "PEND" }
        };
        var gateway = new FakeIntelliPayGateway { ChargeResponse = "{\"response\":\"D\",\"declinereason\":\"Insufficient funds\"}" };
        var appRepo = new FakeApplicationRepository
        {
            ByIdResult = new DomainApplication { AppId = "1234567890123", Applicant = new Applicant { AppEmailAddr = "tina@example.com" } }
        };
        var notifications = new FakePermitNotificationService();
        var service = new PaymentService(repo, appRepo, gateway, notifications, NullLogger<PaymentService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ChargeAsync(new ChargeRequest { AppId = "1234567890123", CaptureAmount = 660m }));

        Assert.Contains(notifications.Sent, s => s.Scenario == "ccChargeFailedAudit");
        Assert.Contains(notifications.Sent, s => s.Scenario == "decline");
    }

    [Fact]
    public async Task UploadSitemapAsync_SendsSitemapReceived()
    {
        var appRepo = new FakeApplicationRepository
        {
            ByIdResult = new DomainApplication { AppId = "1700000000001", Applicant = new Applicant { AppEmailAddr = "tina@example.com" } }
        };
        var notifications = new FakePermitNotificationService();
        var service = new FileUploadService(
            new FakeVirusScanner { Clean = true }, new FakeFileTransferService(), appRepo, notifications,
            Options.Create(new FtpOptions()), NullLogger<FileUploadService>.Instance);

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("%PDF-1.4 test"));
        await service.UploadSitemapAsync("1700000000001", "sitemap.pdf", "application/pdf", stream, "public-portal");

        Assert.Contains(notifications.Sent, s => s.Scenario == "sitemap");
    }

    [Fact]
    public async Task UploadSitemapAsync_Infected_SendsScanAudit()
    {
        var appRepo = new FakeApplicationRepository();
        var notifications = new FakePermitNotificationService();
        var service = new FileUploadService(
            new FakeVirusScanner { Clean = false }, new FakeFileTransferService(), appRepo, notifications,
            Options.Create(new FtpOptions()), NullLogger<FileUploadService>.Instance);

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("bad"));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UploadSitemapAsync("1700000000001", "sitemap.pdf", "application/pdf", stream, "public-portal"));

        Assert.Contains(notifications.Sent, s => s.Scenario == "sitemapScanAudit");
    }
}

public sealed class EmailMessagesTests
{
    private static DomainApplication SampleApp() => new()
    {
        AppId = "1234567890123",
        AddDate = new DateTime(2026, 1, 5),
        StatusCode = "PENDS",
        SiteCityName = "Oakland",
        SiteLocation = "123 Main St",
        ProjStartDate = new DateTime(2026, 2, 1),
        ProjEndDate = new DateTime(2026, 3, 1),
        Applicant = new Applicant { AppFirstName = "Tina", AppLastName = "Lopez", AppEmailAddr = "tina@example.com" },
        Works = new List<ApplicationWork>
        {
            new()
            {
                WorkId = 1, WorkCategory = "con", WorkType = "conwell", DrillerName = "Ace", DrillerLicenseNum = "L123",
                WorkFeeRate = 250m, WorkFeeUnit = "EA", StatusCode = "PENDC",
                Specs = new List<ApplicationWorkSpec> { new() { DrillCount = 2, StatusCode = "PEND" } }
            }
        }
    };

    [Fact]
    public void ApplicationConfirmation_HasSubjectAndKeyContent()
    {
        var content = EmailMessages.ApplicationConfirmation(SampleApp(), 500m, "CC", null, "http://localhost:3000/track?email=x&appid=y", "wells@acpwa.org");
        Assert.Equal("Alameda County PWA Permits Application Confirmation", content.Subject);
        Assert.Contains("1234567890123", content.HtmlBody);
        Assert.Contains("Application Received", content.HtmlBody);
        Assert.Contains("$500.00", content.HtmlBody);
        Assert.Contains("wells@acpwa.org", content.HtmlBody);
    }

    [Fact]
    public void SitemapReceived_HasSubject()
    {
        var content = EmailMessages.SitemapReceived(SampleApp(), "http://localhost:3000/track", "wells@acpwa.org");
        Assert.Equal("Alameda County PWA Wells Permits Application Sitemap Received", content.Subject);
        Assert.Contains("site map", content.HtmlBody);
    }

    [Fact]
    public void ApprovalNotification_ShowsPermitNumbers()
    {
        var content = EmailMessages.ApprovalNotification(SampleApp(), new[] { "WP-1", "WP-2" }, null, "http://x/track", "http://site", "wells@acpwa.org");
        Assert.Equal("Alameda County Well Permit Approval Notification", content.Subject);
        Assert.Contains("WP-1, WP-2", content.HtmlBody);
        Assert.Contains("approved", content.HtmlBody);
    }

    [Fact]
    public void CcPreAuthAudit_IsPciSafe()
    {
        var content = EmailMessages.CcPreAuthAudit(SampleApp(), 500m, "CUST8888");
        Assert.Contains("IntelliPay CC Pre-Auth Submitted", content.Subject);
        Assert.Contains("CUST8888", content.HtmlBody);
        // Must never leak PCI-sensitive fields.
        foreach (var forbidden in new[] { "cardnumdisplay", "nonce", "hmac", "methodhint", "receiptelements" })
        {
            Assert.DoesNotContain(forbidden, content.HtmlBody);
        }
    }

    [Fact]
    public void CcChargeAudit_FailedShowsReason()
    {
        var content = EmailMessages.CcChargeAudit("1234567890123", "660.00", success: false, failReason: "Insufficient funds", paymentId: null, authCode: null);
        Assert.Contains("CC Charge FAILED", content.Subject);
        Assert.Contains("Insufficient funds", content.HtmlBody);
    }

    [Fact]
    public void HtmlEncoding_EscapesUserContent()
    {
        var app = SampleApp();
        app.SiteLocation = "<script>alert('x')</script>";
        var content = EmailMessages.SitemapReceived(app, "http://x/track", "wells@acpwa.org");
        Assert.DoesNotContain("<script>alert", content.HtmlBody);
        Assert.Contains("&lt;script&gt;", content.HtmlBody);
    }

    [Fact]
    public void SystemExceptionAudit_IncludesFullStackTrace()
    {
        Exception caught;
        try
        {
            throw new InvalidOperationException("kaboom");
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        var content = EmailMessages.SystemExceptionAudit("PaymentService.PreAuth", "App 123", caught);
        Assert.Contains("Stack Trace:", content.HtmlBody);
        Assert.Contains("kaboom", content.HtmlBody);
        // The full trace (ex.ToString()) names the throwing method, not just a frame count.
        Assert.Contains(nameof(SystemExceptionAudit_IncludesFullStackTrace), content.HtmlBody);
    }

    [Fact]
    public void ApprovalExceptionAudit_IncludesFullStackTrace()
    {
        Exception caught;
        try
        {
            throw new InvalidOperationException("approval boom");
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        var content = EmailMessages.ApprovalExceptionAudit("1234567890123", "approve", "staff@acgov.org", caught);
        Assert.Contains("Stack Trace:", content.HtmlBody);
        Assert.Contains("approval boom", content.HtmlBody);
        Assert.Contains(nameof(ApprovalExceptionAudit_IncludesFullStackTrace), content.HtmlBody);
    }

    private static PermitNotificationService RealNotifier(FakeEmailService email, string environmentName, string auditEmail) =>
        new(
            email,
            Options.Create(new EmailOptions
            {
                EnvironmentName = environmentName,
                AuditEmail = auditEmail,
                ContactEmail = "wells@acpwa.org",
                WebsiteUrl = "http://site",
                PublicAppBaseUrl = "http://localhost:3000"
            }),
            Options.Create(new IcapOptions()),
            NullLogger<PermitNotificationService>.Instance);

    [Fact]
    public async Task RoutineNotification_NonProd_CopiesAuditListViaCc()
    {
        // The audit distribution list is CC'd on routine applicant application emails so staff have
        // oversight of all application correspondence (every environment).
        var email = new FakeEmailService();
        var notifier = RealNotifier(email, "dev", "a@x.gov,b@y.gov");

        await notifier.SendApplicationConfirmationAsync(SampleApp(), 500m, "CC", null);

        var notification = Assert.Single(email.Notifications);
        Assert.Contains("tina@example.com", notification.To);
        Assert.Contains("a@x.gov,b@y.gov", notification.Cc);
        Assert.Empty(notification.Bcc);
    }

    [Fact]
    public async Task RoutineNotification_Prod_DoesNotCopyAuditList()
    {
        // In production the audit inboxes are NOT copied on applicant application emails; they only
        // receive error/exception notifications (which go directly TO the audit list, all envs).
        var email = new FakeEmailService();
        var notifier = RealNotifier(email, "prod", "a@x.gov,b@y.gov");

        await notifier.SendApplicationConfirmationAsync(SampleApp(), 500m, "CC", null);

        var notification = Assert.Single(email.Notifications);
        Assert.Contains("tina@example.com", notification.To);
        Assert.DoesNotContain("a@x.gov,b@y.gov", notification.Cc);
        Assert.Empty(notification.Bcc);
    }

    [Fact]
    public async Task SitemapReceived_CopiesOtherEmailAddressesViaCc()
    {
        // The applicant's "Other Email Addresses" (APP_EMAIL_CC) must be CC'd on the sitemap-received
        // confirmation, regardless of environment — legacy ecomm/intra parity.
        var email = new FakeEmailService();
        var notifier = RealNotifier(email, "prod", "audit@x.gov");
        var app = SampleApp();
        app.EmailCcs = new List<AppEmailCc>
        {
            new() { EmailAddrCc = "cc1@example.com" },
            new() { EmailAddrCc = "cc2@example.com" }
        };

        await notifier.SendSitemapReceivedAsync(app);

        var notification = Assert.Single(email.Notifications);
        Assert.Contains("tina@example.com", notification.To);
        Assert.Contains("cc1@example.com", notification.Cc);
        Assert.Contains("cc2@example.com", notification.Cc);
    }

    [Fact]
    public async Task ErrorAuditNotification_Prod_AddressesAuditListInTo()
    {
        // Error/exception audit notifications always go TO the audit list, even in production.
        var email = new FakeEmailService();
        var notifier = RealNotifier(email, "prod", "a@x.gov,b@y.gov");

        await notifier.SendApprovalExceptionAuditAsync("1234567890123", "approval", "staff@acgov.org", new InvalidOperationException("boom"));

        var audit = Assert.Single(email.Audits);
        Assert.Contains("1234567890123", audit.Body);
    }

    [Fact]
    public async Task SystemExceptionAudit_AddressesAuditListInTo_AllEnvs()
    {
        // Any genuine system exception (global handler or per-catch) must email the audit list, all envs.
        var email = new FakeEmailService();
        var notifier = RealNotifier(email, "prod", "a@x.gov,b@y.gov");

        await notifier.SendSystemExceptionAuditAsync("PaymentService.PreAuth", "Application 1234567890123", new InvalidOperationException("boom"));

        var audit = Assert.Single(email.Audits);
        Assert.Contains("PaymentService.PreAuth", audit.Body);
        Assert.Contains("boom", audit.Body);
    }

    [Fact]
    public async Task HttpErrorAudit_AddressesAuditList_WithEnvPrefixInNonProd()
    {
        // 401/403 (and other non-validation error statuses) that never throw are audited via this path.
        var email = new FakeEmailService();
        var notifier = RealNotifier(email, "dev", "a@x.gov,b@y.gov");

        await notifier.SendHttpErrorAuditAsync("GET", "/api/inspections", 403, "Forbidden", "staff@acgov.org", "203.0.113.7", "corr-123");

        var audit = Assert.Single(email.Audits);
        Assert.StartsWith("DEV - ", audit.Subject);
        Assert.Contains("403", audit.Body);
        Assert.Contains("/api/inspections", audit.Body);
        // The audit body must show the acting user's email id.
        Assert.Contains("staff@acgov.org", audit.Body);
        // The audit body must show the caller's IP address.
        Assert.Contains("IP Address", audit.Body);
        Assert.Contains("203.0.113.7", audit.Body);
    }

    [Fact]
    public async Task HttpErrorAudit_ShowsAnonymousUser_WhenNoIdentityResolved()
    {
        var email = new FakeEmailService();
        var notifier = RealNotifier(email, "prod", "a@x.gov,b@y.gov");

        await notifier.SendHttpErrorAuditAsync("GET", "/api/auth/me", 401, "Unauthorized", null, null, null);

        var audit = Assert.Single(email.Audits);
        Assert.Contains("User", audit.Body);
        Assert.Contains("(anonymous)", audit.Body);
        // No IP resolved -> shown as unknown rather than omitted.
        Assert.Contains("IP Address", audit.Body);
        Assert.Contains("(unknown)", audit.Body);
    }

    [Fact]
    public async Task HttpErrorAudit_RateLimited429_IncludesIpAddress()
    {
        // 429 from the rate limiter is audited via the same path and must carry the caller's IP.
        var email = new FakeEmailService();
        var notifier = RealNotifier(email, "prod", "a@x.gov,b@y.gov");

        await notifier.SendHttpErrorAuditAsync("GET", "/api/ref/cities", 429, "Too Many Requests", null, "198.51.100.42", null);

        var audit = Assert.Single(email.Audits);
        Assert.Contains("429", audit.Body);
        Assert.Contains("/api/ref/cities", audit.Body);
        Assert.Contains("IP Address", audit.Body);
        Assert.Contains("198.51.100.42", audit.Body);
    }

    [Fact]
    public async Task Subject_NonProd_PrefixesEnvironmentName()
    {
        // Non-production emails prefix the subject with the uppercased environment name.
        var email = new FakeEmailService();
        var notifier = RealNotifier(email, "dev", "a@x.gov,b@y.gov");

        await notifier.SendApplicationConfirmationAsync(SampleApp(), 500m, "CC", null);

        var notification = Assert.Single(email.Notifications);
        Assert.StartsWith("DEV - ", notification.Subject);
    }

    [Fact]
    public async Task Subject_Prod_HasNoEnvironmentPrefix()
    {
        // Production subjects are never prefixed.
        var email = new FakeEmailService();
        var notifier = RealNotifier(email, "prod", "a@x.gov,b@y.gov");

        await notifier.SendApplicationConfirmationAsync(SampleApp(), 500m, "CC", null);

        var notification = Assert.Single(email.Notifications);
        Assert.StartsWith("Alameda County", notification.Subject);
    }

    [Fact]
    public async Task Body_NonProd_ShowsEnvironmentWarningBanner()
    {
        // Non-production applicant emails carry a visible warning banner in the body with the
        // uppercased environment label, and never leak the raw placeholder marker.
        var email = new FakeEmailService();
        var notifier = RealNotifier(email, "uat", "a@x.gov,b@y.gov");

        await notifier.SendApplicationConfirmationAsync(SampleApp(), 500m, "CC", null);

        var notification = Assert.Single(email.Notifications);
        Assert.Contains("UAT ENVIRONMENT", notification.HtmlBody);
        Assert.Contains("non-production", notification.HtmlBody);
        Assert.DoesNotContain(EmailTemplate.EnvironmentBannerMarker, notification.HtmlBody);
    }

    [Fact]
    public async Task Body_Prod_HasNoEnvironmentWarningBanner()
    {
        // Production applicant emails show no warning banner and no leftover placeholder marker.
        var email = new FakeEmailService();
        var notifier = RealNotifier(email, "prod", "a@x.gov,b@y.gov");

        await notifier.SendApplicationConfirmationAsync(SampleApp(), 500m, "CC", null);

        var notification = Assert.Single(email.Notifications);
        Assert.DoesNotContain("ENVIRONMENT &mdash; TEST EMAIL", notification.HtmlBody);
        Assert.DoesNotContain("non-production", notification.HtmlBody);
        Assert.DoesNotContain(EmailTemplate.EnvironmentBannerMarker, notification.HtmlBody);
    }

    [Fact]
    public async Task AuditBody_NonProd_ShowsEnvironmentWarningBanner()
    {
        // Audit/exception emails also carry the non-production warning banner in their body.
        var email = new FakeEmailService();
        var notifier = RealNotifier(email, "tst", "a@x.gov,b@y.gov");

        await notifier.SendSystemExceptionAuditAsync("PaymentService.PreAuth", "Application 1234567890123", new InvalidOperationException("boom"));

        var audit = Assert.Single(email.Audits);
        Assert.Contains("TST ENVIRONMENT", audit.Body);
        Assert.DoesNotContain(EmailTemplate.EnvironmentBannerMarker, audit.Body);
    }
}
