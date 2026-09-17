using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using PWA.PermitsApi.Application.Common;
using DomainApplication = PWA.PermitsApi.Domain.Models.Application;

namespace PWA.PermitsApi.Application.Notifications;

/// <summary>
/// Builds the subject + HTML body of every Wells Permits email scenario, ported faithfully from the
/// legacy Java servlets (ecomm <c>ProcessAppServlet</c>/<c>ProcessFileUploadServlet</c> and intra
/// <c>ProcessApprovalServlet</c>/<c>UpdateAppServlet</c>/<c>ProcessInspectionServlet</c>).
/// <para>
/// All of the static markup/copy now lives in the branded HTML templates embedded under
/// <c>Notifications/Templates</c> (the shared <c>Layout.html</c> shell plus one file per scenario).
/// Each method here only computes the dynamic values — ids, dates, links and the pre-rendered
/// data-driven fragments (detail tables, work line-items) — and hands them to
/// <see cref="EmailTemplateStore"/> to substitute the <c>{{Placeholder}}</c> tokens.
/// </para>
/// Subjects here are the base subject — the "TEST - " environment prefix is applied by the
/// notification service.
/// </summary>
public static class EmailMessages
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    private static string Money(decimal value) => value.ToString("C2", Us);

    private static string Date(DateTime? value) => value.HasValue ? value.Value.ToString("MM/dd/yyyy", Us) : string.Empty;

    private static string DateTimeStr(DateTime? value) => value.HasValue ? value.Value.ToString("MM/dd/yyyy h:mm tt", Us) : string.Empty;

    private static string PersonName(string? first, string? last) =>
        string.Join(" ", new[] { first, last }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();

    // Human-readable "Category - Type" work label, preferring the lookup descriptions and falling
    // back to the raw codes when a description isn't available.
    private static string WorkLabel(PWA.PermitsApi.Domain.Models.ApplicationWork work)
    {
        var cat = string.IsNullOrWhiteSpace(work.WorkCategoryDesc) ? work.WorkCategory : work.WorkCategoryDesc!.Trim();
        var type = string.IsNullOrWhiteSpace(work.WorkTypeDesc) ? work.WorkType : work.WorkTypeDesc!.Trim();
        return string.Join(" - ", new[] { cat, type }.Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    // Renders a scenario body template, substituting the supplied {{Placeholder}} tokens.
    private static string Body(string template, params (string Key, string? Value)[] tokens)
    {
        var map = new Dictionary<string, string?>(tokens.Length, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in tokens) map[key] = value;
        return EmailTemplateStore.Render(template, map);
    }

    // ------------------------------------------------------------------ Applicant-facing ------

    /// <summary>ecomm ProcessAppServlet.sendEmail — application received confirmation.</summary>
    public static EmailContent ApplicationConfirmation(DomainApplication app, decimal authAmount, string paymentType, string? checkNum, string trackingLink, string contactEmail)
    {
        var headerDetails = EmailTemplate.DetailsTable(new[]
        {
            ("Application Confirmation ID", app.AppId),
            ("Submit Date", DateTime.Now.ToString("MM/dd/yyyy h:mm tt", Us)),
            ("Project Site City / Location", $"{app.SiteCityName} / {app.SiteLocation}".Trim(' ', '/')),
            ("Project Start Date", Date(app.ProjStartDate)),
            ("Completion Date", Date(app.ProjEndDate)),
        });

        var isCheckNoNum = string.Equals(paymentType, "CHECK", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(checkNum);
        var checkBlock = isCheckNoNum
            ? EmailTemplate.Paragraph(
                  $"You've selected to pay by check. Please mail your check payment for <strong>{Money(authAmount)}</strong> to the "
                  + "following address, with your Application Confirmation ID written on the front of the check:")
              + EmailTemplate.Paragraph(
                  "Alameda County Public Works Agency<br>Water Resources Section<br>399 Elmhurst Street<br>Hayward, CA 94544-1395")
            : string.Empty;

        var applicantDetails = EmailTemplate.DetailsTable(new[]
        {
            ("City of Project Site", app.SiteCityName ?? string.Empty),
            ("Site Location", app.SiteLocation ?? string.Empty),
            ("Applicant", PersonName(app.Applicant.AppFirstName, app.Applicant.AppLastName) is { Length: > 0 } n
                ? (string.IsNullOrWhiteSpace(app.Applicant.AppBusinessName) ? n : $"{app.Applicant.AppBusinessName} - {n}")
                : app.Applicant.AppBusinessName ?? string.Empty),
        });

        var rows = new StringBuilder();
        var alt = false;
        foreach (var work in app.Works)
        {
            var wells = FeeCalculator.CountWells(work);
            var cost = FeeCalculator.WorkCalcAmount(work);
            var driller = PersonName(null, work.DrillerName);
            if (!string.IsNullOrWhiteSpace(work.DrillerLicenseNum))
            {
                driller = $"{work.DrillerName} - Lic# {work.DrillerLicenseNum}";
            }
            var feeText = $"{Money(work.WorkFeeRate ?? 0m)} per {work.WorkFeeUnit}";
            var bg = alt ? "#ffffff" : "#f7f9fc";
            rows.Append("<tr>")
                .Append(Cell(WorkLabel(work), bg))
                .Append(Cell(driller, bg))
                .Append(Cell(wells.ToString(Us), bg))
                .Append(Cell(feeText, bg))
                .Append(CellRight(Money(cost), bg))
                .Append("</tr>");
            alt = !alt;
        }
        rows.Append("<tr><td colspan=\"4\" align=\"right\" style=\"padding:10px 12px;font-weight:700;color:#003366;border-top:2px solid #e3e8ee;\">Application Total:</td>")
            .Append($"<td align=\"right\" style=\"padding:10px 12px;font-weight:700;color:#003366;border-top:2px solid #e3e8ee;\">{EmailTemplate.Encode(Money(authAmount))}</td></tr>");
        var worksTable = EmailTemplate.RawTable(rows.ToString(), "Work Type", "Driller", "# of Wells", "Fees", "Cost");

        var body = Body("ApplicationConfirmation.html",
            ("HeaderDetails", headerDetails),
            ("CheckPaymentBlock", checkBlock),
            ("TrackingLink", EmailTemplate.EncodeAttr(trackingLink)),
            ("TrackingLinkText", EmailTemplate.Encode(trackingLink)),
            ("ContactEmail", EmailTemplate.Encode(contactEmail)),
            ("ApplicantDetails", applicantDetails),
            ("WorksTable", worksTable));

        var html = EmailTemplate.Wrap("Application Received", "Your Alameda County PWA well permit application has been received.", EmailTemplate.AccentBlue, body);
        return new EmailContent("Alameda County PWA Permits Application Confirmation", html);
    }

    /// <summary>ecomm ProcessFileUploadServlet.sendEmail — site map received confirmation.</summary>
    public static EmailContent SitemapReceived(DomainApplication app, string trackingLink, string contactEmail)
    {
        var details = EmailTemplate.DetailsTable(new[]
        {
            ("Application ID", app.AppId),
            ("Application Date", Date(app.AddDate)),
            ("Project Location", app.SiteLocation ?? string.Empty),
            ("Project Site City", app.SiteCityName ?? string.Empty),
            ("Project Start Date", Date(app.ProjStartDate)),
            ("Completion Date", Date(app.ProjEndDate)),
        });

        var body = Body("SitemapReceived.html",
            ("Details", details),
            ("TrackingLink", EmailTemplate.EncodeAttr(trackingLink)),
            ("TrackingLinkText", EmailTemplate.Encode(trackingLink)),
            ("ContactEmail", EmailTemplate.Encode(contactEmail)));

        var html = EmailTemplate.Wrap("Site Map Received", "We have received the site map for your well permit application.", EmailTemplate.AccentBlue, body);
        return new EmailContent("Alameda County PWA Wells Permits Application Sitemap Received", html);
    }

    /// <summary>intra ProcessApprovalServlet.sendEmail — approval notification (permit issued).</summary>
    public static EmailContent ApprovalNotification(
        DomainApplication app,
        IReadOnlyList<string> permitNumbers,
        InspectorContact? inspector,
        string trackingLink,
        string websiteUrl,
        string contactEmail)
    {
        var permitStr = permitNumbers.Count > 0 ? string.Join(", ", permitNumbers) : "(pending assignment)";
        var (siteVisitText, siteVisitRole) = ResolveSiteVisit(app.SiteVisitType);

        var details = EmailTemplate.DetailsTable(new[]
        {
            ("Application ID", app.AppId),
            ("Application Submitted On", Date(app.AddDate)),
            ("Project Site City / Location", $"{app.SiteCityName} / {app.SiteLocation}".Trim(' ', '/')),
            ("Project Start Date", Date(app.ProjStartDate)),
            ("Completion Date", Date(app.ProjEndDate)),
        });

        var permitCallout = EmailTemplate.Callout(
            $"<strong>Permit Number(s) Issued:</strong> {EmailTemplate.Encode(permitStr)}<br>"
            + $"Valid from {EmailTemplate.Encode(Date(app.ProjStartDate))} to {EmailTemplate.Encode(Date(app.ProjEndDate))}.",
            EmailTemplate.AccentBlue);

        var inspectorCallout = string.Empty;
        if (inspector is not null)
        {
            var contactLine = new StringBuilder();
            contactLine.Append($"<strong>{siteVisitText} is REQUIRED.</strong><br>")
                       .Append($"To avoid possible delay of your project, you must contact your assigned {siteVisitRole}, ");
            if (!string.IsNullOrWhiteSpace(inspector.Email))
            {
                contactLine.Append($"<a href=\"mailto:{EmailTemplate.Encode(inspector.Email)}\">{EmailTemplate.Encode(inspector.Name)}</a> at {EmailTemplate.Encode(inspector.Email)}");
            }
            else
            {
                contactLine.Append(EmailTemplate.Encode(inspector.Name));
            }
            if (!string.IsNullOrWhiteSpace(inspector.Phone))
            {
                contactLine.Append($" or {EmailTemplate.Encode(inspector.Phone)}");
            }
            contactLine.Append(", no later than 5 days before <u>the Project Start Date listed on your permit</u> to schedule your ")
                       .Append(EmailTemplate.Encode(siteVisitText)).Append(".");
            inspectorCallout = EmailTemplate.Callout(contactLine.ToString(), EmailTemplate.AccentAmber);
        }

        var body = Body("ApprovalNotification.html",
            ("Details", details),
            ("PermitCallout", permitCallout),
            ("InspectorCallout", inspectorCallout),
            ("WebsiteUrl", EmailTemplate.Encode(websiteUrl)),
            ("ContactEmail", EmailTemplate.Encode(contactEmail)));

        var html = EmailTemplate.Wrap("Permit Approved", "Your Alameda County well permit application has been approved.", EmailTemplate.AccentBlue, body);
        return new EmailContent("Alameda County Well Permit Approval Notification", html);
    }

    /// <summary>intra ProcessApprovalServlet.sendEmailFail — credit-card decline notification.</summary>
    public static EmailContent PaymentDecline(DomainApplication app, string? auditNote)
    {
        var auditNoteHtml = string.IsNullOrWhiteSpace(auditNote)
            ? string.Empty
            : EmailTemplate.Callout(auditNote, EmailTemplate.AccentRed);

        var details = EmailTemplate.DetailsTable(new[]
        {
            ("Application ID", app.AppId),
            ("Project Site City / Location", $"{app.SiteCityName} / {app.SiteLocation}".Trim(' ', '/')),
            ("Project Start Date", Date(app.ProjStartDate)),
            ("Completion Date", Date(app.ProjEndDate)),
        });

        var body = Body("PaymentDecline.html",
            ("AuditNote", auditNoteHtml),
            ("Details", details));

        var html = EmailTemplate.Wrap("Credit Card Declined", "We were unable to authorize the card for your permit application.", EmailTemplate.AccentRed, body);
        return new EmailContent("Alameda County PWA - Credit Card Decline Notification", html);
    }

    /// <summary>intra UpdateAppServlet.sendCancelEmail — application cancelled notification.</summary>
    public static EmailContent ApplicationCancelled(DomainApplication app, string cancelledBy, string websiteUrl)
    {
        var details = EmailTemplate.DetailsTable(new[]
        {
            ("Application ID", app.AppId),
            ("Cancelled By", cancelledBy),
            ("Cancelled On", DateTime.Now.ToString("MM/dd/yyyy h:mm tt", Us)),
        });

        var body = Body("ApplicationCancelled.html",
            ("Details", details),
            ("WebsiteUrl", EmailTemplate.Encode(websiteUrl)));

        var html = EmailTemplate.Wrap("Application Cancelled", "Your Alameda County PWA well permit application has been cancelled.", EmailTemplate.AccentRed, body);
        return new EmailContent("Alameda County PWA Online Wells Permits Application Cancelled", html);
    }

    /// <summary>intra ProcessInspectionServlet.sendInspectionEmailtoCustomers — site visit scheduled.</summary>
    public static EmailContent InspectionScheduled(
        DomainApplication app,
        IReadOnlyList<string> permitNumbers,
        DateTime? inspectionDateTime,
        InspectorContact? inspector)
    {
        var siteVisitLabel = string.IsNullOrWhiteSpace(app.SiteVisitType) ? "Site Visit" : app.SiteVisitType!;
        var (siteVisitText, _) = ResolveSiteVisit(app.SiteVisitType);
        var permitStr = permitNumbers.Count > 0 ? string.Join(", ", permitNumbers) : "(pending assignment)";

        var details = EmailTemplate.DetailsTable(new[]
        {
            ("Application ID", app.AppId),
            ("Application Submitted On", Date(app.AddDate)),
            ("Project Site City / Location", $"{app.SiteCityName} / {app.SiteLocation}".Trim(' ', '/')),
            ("Project Start Date", Date(app.ProjStartDate)),
            ("Completion Date", Date(app.ProjEndDate)),
            ("Permit Number(s) Issued", permitStr),
            ($"{siteVisitLabel} Scheduled", DateTimeStr(inspectionDateTime)),
        });

        var inspectorCallout = string.Empty;
        if (inspector is not null)
        {
            var line = new StringBuilder($"If you have any questions regarding the scheduled {EmailTemplate.Encode(siteVisitLabel)}, contact ");
            if (!string.IsNullOrWhiteSpace(inspector.Email))
            {
                line.Append($"<a href=\"mailto:{EmailTemplate.Encode(inspector.Email)}\">{EmailTemplate.Encode(inspector.Name)}</a> at {EmailTemplate.Encode(inspector.Email)}");
            }
            else
            {
                line.Append(EmailTemplate.Encode(inspector.Name));
            }
            if (!string.IsNullOrWhiteSpace(inspector.Phone))
            {
                line.Append($" or {EmailTemplate.Encode(inspector.Phone)}");
            }
            line.Append('.');
            inspectorCallout = EmailTemplate.Callout(line.ToString(), EmailTemplate.AccentBlue);
        }

        var body = Body("InspectionScheduled.html",
            ("SiteVisitLower", EmailTemplate.Encode(siteVisitText.ToLower(Us))),
            ("Details", details),
            ("InspectorCallout", inspectorCallout));

        var html = EmailTemplate.Wrap($"{siteVisitLabel} Scheduled", $"Your {siteVisitLabel.ToLower(Us)} has been scheduled.", EmailTemplate.AccentBlue, body);
        return new EmailContent($"Alameda County Well Permit {siteVisitLabel} Notification", html);
    }

    // ------------------------------------------------------------------ Audit / exception ------

    /// <summary>ecomm ProcessAppServlet.sendAuditEmail — CC $0 pre-auth submitted (PCI-safe).</summary>
    public static EmailContent CcPreAuthAudit(DomainApplication app, decimal authAmount, string? customerId)
    {
        var details = EmailTemplate.DetailsTable(new[]
        {
            ("Application ID", app.AppId),
            ("Submit Date/Time", DateTime.Now.ToString("MM/dd/yyyy h:mm tt", Us)),
            ("Applicant", PersonName(app.Applicant.AppFirstName, app.Applicant.AppLastName)),
            ("Email", app.Applicant.AppEmailAddr ?? string.Empty),
            ("Project Site", $"{app.SiteCityName} / {app.SiteLocation}".Trim(' ', '/')),
            ("Fee Amount (auth_amount)", Money(authAmount)),
            ("Charged at Lightbox", "$0.00 (card stored for admin charge)"),
            ("IntelliPay Customer ID", customerId ?? string.Empty),
            ("Payment Status", "PEND — awaiting admin charge via IntelliPay REST API"),
        });

        var body = Body("CcPreAuthAudit.html", ("Details", details));
        var html = EmailTemplate.Wrap("CC Pre-Authorization Submitted", "IntelliPay card vaulting audit notification.", EmailTemplate.AccentBlue, body);
        return new EmailContent($"IntelliPay CC Pre-Auth Submitted – App ID: {app.AppId}", html);
    }

    /// <summary>ecomm ProcessAppServlet payment-failure catch — payment processing error audit.</summary>
    public static EmailContent PaymentErrorAudit(string appId, string paymentType, string? applicantName, string? email, bool dbCommitted, string error)
    {
        var details = EmailTemplate.DetailsTable(new[]
        {
            ("Application ID", appId),
            ("Payment Type", paymentType),
            ("Applicant", applicantName ?? string.Empty),
            ("Email", email ?? string.Empty),
            ("DB Committed", dbCommitted ? "true" : "false"),
            ("Error", error),
            ("Time", DateTime.Now.ToString("MM/dd/yyyy h:mm tt", Us)),
        });

        var body = Body("PaymentErrorAudit.html", ("Details", details));
        var html = EmailTemplate.Wrap("Payment Processing Error", "Automated payment error notification.", EmailTemplate.AccentRed, body);
        return new EmailContent($"PAYMENT ERROR – App ID: {appId}", html);
    }

    /// <summary>ecomm ProcessFileUploadServlet.sendAuditEmail — sitemap virus scan / FTP audit.</summary>
    public static EmailContent SitemapScanAudit(string appId, string reason, string environment, string icapEndpoint)
    {
        var details = EmailTemplate.DetailsTable(new[]
        {
            ("Application ID", appId),
            ("Scan Result", reason),
            ("Environment", environment),
            ("Time", DateTime.Now.ToString("MM/dd/yyyy h:mm tt", Us)),
            ("ICAP Endpoint", icapEndpoint),
        });

        var body = Body("SitemapScanAudit.html", ("Details", details));
        var html = EmailTemplate.Wrap("Site Map Virus Scan Notification", "Automated site map scan / delivery audit.", EmailTemplate.AccentAmber, body);
        return new EmailContent($"[PWA Ecomm] Sitemap Virus Scan Notification - {reason} (App {appId})", html);
    }

    /// <summary>intra ProcessApprovalServlet.sendApprovalExceptionEmail — approval exception audit.</summary>
    public static EmailContent ApprovalExceptionAudit(string appId, string process, string user, Exception ex)
    {
        var details = EmailTemplate.DetailsTable(new[]
        {
            ("Application ID", appId),
            ("Process", process),
            ("Staff User", user),
            ("Exception Class", ex.GetType().FullName ?? ex.GetType().Name),
            ("Message", ex.Message),
        });

        var body = Body("ApprovalExceptionAudit.html",
            ("Details", details),
            ("StackTrace", EmailTemplate.Encode(ex.ToString())));

        var html = EmailTemplate.Wrap("Exception in Approval", "Automated approval exception notification.", EmailTemplate.AccentRed, body);
        return new EmailContent($"[PWA Permits] Exception in Approval - App {appId} [{process}]", html);
    }

    /// <summary>
    /// Generic system-exception audit — emailed to the audit list for any error the application
    /// catches (or that reaches the global exception handler). <paramref name="source"/> names the
    /// originating component/operation; <paramref name="context"/> is optional extra detail (e.g.
    /// request path, application id).
    /// </summary>
    public static EmailContent SystemExceptionAudit(string source, string? context, Exception ex)
    {
        var rows = new List<(string, string)> { ("Source", source) };
        if (!string.IsNullOrWhiteSpace(context)) rows.Add(("Context", context!));
        rows.Add(("Exception Class", ex.GetType().FullName ?? ex.GetType().Name));
        rows.Add(("Message", ex.Message));
        if (ex.InnerException is not null)
        {
            rows.Add(("Inner Exception", ex.InnerException.GetType().Name + ": " + ex.InnerException.Message));
        }
        rows.Add(("Time", DateTime.Now.ToString("MM/dd/yyyy h:mm tt", Us)));

        var body = Body("SystemExceptionAudit.html",
            ("Details", EmailTemplate.DetailsTable(rows)),
            ("StackTrace", EmailTemplate.Encode(ex.ToString())));

        var html = EmailTemplate.Wrap("System Exception", "Automated system exception notification.", EmailTemplate.AccentRed, body);
        return new EmailContent($"PWA Permits - System Exception - {source}", html);
    }

    /// <summary>
    /// Audit email for an error HTTP <b>status code</b> that was returned without throwing (e.g. a
    /// 401/403 produced by the authentication/authorization middleware, or another non-validation
    /// 4xx/5xx). There is no exception/stack trace — just request context.
    /// </summary>
    public static EmailContent HttpErrorAudit(string method, string path, int statusCode, string reason, string? user, string? clientIp, string? correlationId)
    {
        var rows = new List<(string, string)>
        {
            ("Status Code", $"{statusCode} {reason}"),
            ("Request", $"{method} {path}"),
            ("User", string.IsNullOrWhiteSpace(user) ? "(anonymous)" : user!),
            ("IP Address", string.IsNullOrWhiteSpace(clientIp) ? "(unknown)" : clientIp!)
        };
        if (!string.IsNullOrWhiteSpace(correlationId)) rows.Add(("Correlation Id", correlationId!));
        rows.Add(("Time", DateTime.Now.ToString("MM/dd/yyyy h:mm tt", Us)));

        var body = Body("HttpErrorAudit.html",
            ("StatusCode", statusCode.ToString(Us)),
            ("Reason", EmailTemplate.Encode(reason)),
            ("Details", EmailTemplate.DetailsTable(rows)));

        var html = EmailTemplate.Wrap("Request Error", "Automated HTTP error notification.", EmailTemplate.AccentRed, body);
        return new EmailContent($"PWA Permits - HTTP {statusCode} - {method} {path}", html);
    }

    /// <summary>
    /// Audit email raised when an authenticated Entra user who is NOT on the application allowlist is
    /// blocked (403) from an intra endpoint. Carries just the identity and request context.
    /// </summary>
    public static EmailContent UnauthorizedAccessAudit(string? user, string? clientIp, string method, string path, string? correlationId)
    {
        var rows = new List<(string, string)>
        {
            ("User", string.IsNullOrWhiteSpace(user) ? "(unknown)" : user!),
            ("IP Address", string.IsNullOrWhiteSpace(clientIp) ? "(unknown)" : clientIp!),
            ("Request", $"{method} {path}"),
            ("Result", "403 Forbidden — user is not on the application allowlist")
        };
        if (!string.IsNullOrWhiteSpace(correlationId)) rows.Add(("Correlation Id", correlationId!));
        rows.Add(("Time", DateTime.Now.ToString("MM/dd/yyyy h:mm tt", Us)));

        var body = Body("UnauthorizedAccessAudit.html",
            ("Details", EmailTemplate.DetailsTable(rows)));

        var html = EmailTemplate.Wrap("Unauthorized Access", "Automated allowlist rejection notification.", EmailTemplate.AccentRed, body);
        return new EmailContent($"PWA Permits - Unauthorized Access - {(string.IsNullOrWhiteSpace(user) ? "unknown" : user)}", html);
    }

    /// <summary>intra ProcessApprovalServlet.sendCCChargeAuditEmail — CC charge approved/failed audit.</summary>
    public static EmailContent CcChargeAudit(string appId, string amount, bool success, string? failReason, string? paymentId, string? authCode)
    {
        var rows = new List<(string, string)> { ("Application ID", appId), ("Charge Amount", "$" + amount) };
        string statusCallout;
        string statusNote;
        if (success)
        {
            statusCallout = EmailTemplate.Callout("<strong>Credit card charge APPROVED.</strong>", EmailTemplate.AccentGreen);
            if (!string.IsNullOrWhiteSpace(paymentId)) rows.Add(("IntelliPay Payment ID", paymentId!));
            if (!string.IsNullOrWhiteSpace(authCode)) rows.Add(("Auth Code", authCode!));
            statusNote = "Receipt number and auth code have been stored. Application status set to PAID.";
        }
        else
        {
            statusCallout = EmailTemplate.Callout("<strong>Credit card charge FAILED.</strong>", EmailTemplate.AccentRed);
            if (!string.IsNullOrWhiteSpace(failReason)) rows.Add(("Reason", failReason!));
            statusNote = "Application payment status set to PAYFL. Staff action required.";
        }

        var body = Body("CcChargeAudit.html",
            ("StatusCallout", statusCallout),
            ("Details", EmailTemplate.DetailsTable(rows)),
            ("StatusNote", statusNote));

        var accent = success ? EmailTemplate.AccentGreen : EmailTemplate.AccentRed;
        var html = EmailTemplate.Wrap(success ? "CC Charge Approved" : "CC Charge Failed", "IntelliPay charge audit notification.", accent, body);
        var subject = success
            ? $"PWA Permits - CC Charge APPROVED - App {appId} - ${amount}"
            : $"PWA Permits - CC Charge FAILED - App {appId}";
        return new EmailContent(subject, html);
    }

    // ------------------------------------------------------------------ helpers ------

    private static (string Text, string Role) ResolveSiteVisit(string? siteVisitType)
    {
        if (string.Equals(siteVisitType, "INSP", StringComparison.OrdinalIgnoreCase)
            || string.Equals(siteVisitType, "Inspection", StringComparison.OrdinalIgnoreCase))
        {
            return ("Inspection", "inspector");
        }
        if (string.Equals(siteVisitType, "REVW", StringComparison.OrdinalIgnoreCase)
            || string.Equals(siteVisitType, "Review", StringComparison.OrdinalIgnoreCase))
        {
            return ("Site Visit", "field technician");
        }
        return ("Site Visit", "Agency representative");
    }

    private static string Cell(string value, string bg) =>
        $"<td style=\"padding:9px 12px;font-size:13px;color:#1f2933;border-bottom:1px solid #e3e8ee;background:{bg};\">{EmailTemplate.Encode(value)}</td>";

    private static string CellRight(string value, string bg) =>
        $"<td align=\"right\" style=\"padding:9px 12px;font-size:13px;color:#1f2933;border-bottom:1px solid #e3e8ee;background:{bg};\">{EmailTemplate.Encode(value)}</td>";
}

/// <summary>Assigned inspector / technician contact details for the site-visit blocks in emails.</summary>
public sealed record InspectorContact(string Name, string? Email, string? Phone);
