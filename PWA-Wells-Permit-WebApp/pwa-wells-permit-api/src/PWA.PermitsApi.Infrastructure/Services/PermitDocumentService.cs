using System.Globalization;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Shapes;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PWA.PermitsApi.Application.Common;
using PWA.PermitsApi.Application.Interfaces.Integration;
using PWA.PermitsApi.Application.Notifications;
using DomainWork = PWA.PermitsApi.Domain.Models.ApplicationWork;
using DomainSpec = PWA.PermitsApi.Domain.Models.ApplicationWorkSpec;

namespace PWA.PermitsApi.Infrastructure.Services;

/// <summary>
/// Renders the Water Resources well permit to a PDF using MigraDoc/PDFsharp (MIT-licensed). The
/// layout mirrors the intra React <c>PermitPage</c> / legacy <c>DisplayPdf</c> permit: agency
/// masthead, approval banner, application/site grid, parties, payment summary, per-work
/// specifications and the specific work-permit conditions.
/// </summary>
public sealed class PermitDocumentService : IPermitDocumentService
{
    private static readonly Color Navy = new(20, 48, 79);              // #14304f — matches intra PermitPage
    private static readonly Color Subtitle = new(47, 75, 107);         // #2f4b6b
    private static readonly Color Muted = new(90, 96, 104);            // #5a6068
    private static readonly Color OfficeClr = new(51, 51, 51);         // #333
    private static readonly Color BorderClr = new(226, 229, 234);      // #e2e5ea
    private static readonly Color SpecBorder = new(204, 208, 214);     // #ccd0d6
    private static readonly Color SpecHeaderFill = new(238, 242, 246); // #eef2f6
    private static readonly Color RowAlt = new(247, 249, 252);
    private static readonly Color OkGreen = new(46, 125, 50);          // #2e7d32
    private static readonly Color PreviewBg = new(253, 244, 230);      // #fdf4e6
    private static readonly Color PreviewBorder = new(200, 137, 42);   // #c8892a
    private static readonly Color PreviewText = new(122, 83, 20);      // #7a5314
    private static readonly Color FooterClr = new(122, 128, 136);      // #7a8088

    private static readonly string[] BoreholeCategories = { "inv", "invprb" };

    private static readonly object FontLock = new();
    private static bool _fontsReady;

    private static readonly byte[]? LogoBytes = LoadLogo();

    public byte[] GeneratePermitPdf(PermitDocumentModel model)
    {
        EnsureFonts();

        var doc = new Document();
        doc.Info.Title = $"Well Permit {model.Application.AppId}";
        doc.Info.Author = "Alameda County Public Works Agency";

        var normal = doc.Styles["Normal"]!;
        normal.Font.Name = "Arial";
        normal.Font.Size = 9.5;
        normal.Font.Color = new Color(31, 41, 51);

        var section = doc.AddSection();
        section.PageSetup.PageFormat = PageFormat.Letter;
        section.PageSetup.TopMargin = "1.4cm";
        section.PageSetup.BottomMargin = "1.6cm";
        section.PageSetup.LeftMargin = "1.6cm";
        section.PageSetup.RightMargin = "1.6cm";

        var logoPath = WriteLogoTempFile();
        try
        {
            BuildHeader(section, logoPath);
            BuildApprovalBand(section, model);
            BuildInfoGrid(section, model);
            BuildParties(section, model);
            BuildPayment(section, model);
            BuildWorks(section, model);
            BuildConditions(section, model);
            BuildFooter(section, model);

            var renderer = new PdfDocumentRenderer { Document = doc };
            renderer.RenderDocument();
            if (!model.Approved)
            {
                DrawPreviewWatermark(renderer.PdfDocument);
            }
            using var ms = new MemoryStream();
            renderer.PdfDocument.Save(ms, false);
            return ms.ToArray();
        }
        finally
        {
            if (logoPath is not null)
            {
                try { File.Delete(logoPath); } catch { /* best-effort cleanup */ }
            }
        }
    }

    // ---------------------------------------------------------------- sections ------

    private static void BuildHeader(Section section, string? logoPath)
    {
        // Logo floats at the top-left and overlaps the title block (mirrors the intra
        // React PermitPage where .permit-logo is absolutely positioned, 96px tall). Keeping
        // it out of the text flow lets the mark be large without pushing the masthead down.
        if (logoPath is not null)
        {
            var logo = section.AddImage(logoPath);
            logo.Height = "2.75cm";
            logo.LockAspectRatio = true;
            logo.RelativeHorizontal = RelativeHorizontal.Margin;
            logo.RelativeVertical = RelativeVertical.Page;
            logo.Left = ShapePosition.Left;
            logo.Top = "1.05cm";
            logo.WrapFormat.Style = WrapStyle.Through;
        }

        var title = section.AddParagraph("Alameda County Public Works Agency");
        title.Format.Alignment = ParagraphAlignment.Center;
        title.Format.Font.Size = 16;
        title.Format.Font.Bold = true;
        title.Format.Font.Color = Navy;
        title.Format.SpaceAfter = 1;

        var subtitle = section.AddParagraph("Water Resources — Well Permit");
        subtitle.Format.Alignment = ParagraphAlignment.Center;
        subtitle.Format.Font.Size = 11;
        subtitle.Format.Font.Bold = true;
        subtitle.Format.Font.Color = Subtitle;
        subtitle.Format.SpaceAfter = 2;

        var office = section.AddParagraph();
        office.Format.Alignment = ParagraphAlignment.Center;
        office.Format.Font.Size = 9.5;
        office.Format.Font.Color = OfficeClr;
        office.AddText("399 Elmhurst Street, Hayward, CA 94544-1395");
        office.AddLineBreak();
        office.AddText("Telephone: (510) 670-6633   ·   Fax: (510) 782-1939");

        var rule = section.AddParagraph();
        rule.Format.Borders.Bottom = new Border { Width = 2, Color = Navy };
        rule.Format.SpaceBefore = 3;
        rule.Format.SpaceAfter = 8;
    }

    private static void BuildApprovalBand(Section section, PermitDocumentModel model)
    {
        if (!model.Approved)
        {
            var note = section.AddParagraph();
            note.Format.Shading.Color = PreviewBg;
            note.Format.Borders.Left = new Border { Width = 3, Color = PreviewBorder };
            note.Format.LeftIndent = "0.3cm";
            note.Format.SpaceAfter = 8;
            note.Format.Font.Size = 10;
            note.Format.Font.Color = PreviewText;
            note.AddText("This is a ");
            note.AddFormattedText("preview", new Font { Bold = true });
            note.AddText(" of the well permit. It is ");
            note.AddFormattedText("not yet approved", new Font { Bold = true });
            note.AddText(" and is not valid for drilling.");
            return;
        }

        var permitSummary = BuildPermitSummary(model);

        // Plain two-column approval text — no banner, shading, borders or strap.
        var table = section.AddTable();
        table.Borders.Width = 0;
        table.LeftPadding = 0;
        table.RightPadding = 0;
        table.AddColumn("8.4cm");
        table.AddColumn("8.4cm");
        var row = table.AddRow();
        row.VerticalAlignment = VerticalAlignment.Top;
        row.TopPadding = 0;
        row.BottomPadding = 0;

        var left = row.Cells[0].AddParagraph();
        left.Format.Font.Size = 9.5;
        left.Format.LineSpacingRule = LineSpacingRule.Multiple;
        left.Format.LineSpacing = 1.2;
        left.AddFormattedText("Application Approved on: ", new Font { Bold = true, Color = Navy });
        left.AddText($"{Date(model.PermitInfo?.ApprovedDate)} By {model.PermitInfo?.ApprovedBy ?? "—"}");

        var right = row.Cells[1].AddParagraph();
        right.Format.Alignment = ParagraphAlignment.Right;
        right.Format.Font.Size = 9.5;
        right.Format.LineSpacingRule = LineSpacingRule.Multiple;
        right.Format.LineSpacing = 1.2;
        if (!string.IsNullOrEmpty(permitSummary))
        {
            right.AddFormattedText("Permit Numbers: ", new Font { Bold = true, Color = Navy });
            right.AddText(permitSummary);
            right.AddLineBreak();
        }
        right.AddFormattedText("Permits Valid ", new Font { Bold = true, Color = Navy });
        right.AddText($"from {Date(model.Application.ProjStartDate)} to {Date(model.Application.ProjEndDate)}");

        var spacer = section.AddParagraph();
        spacer.Format.SpaceAfter = 6;
    }

    private static void BuildInfoGrid(Section section, PermitDocumentModel model)
    {
        var app = model.Application;
        var table = section.AddTable();
        table.Borders.Width = 0;
        table.TopPadding = 1;
        table.BottomPadding = 1;
        table.AddColumn("8.4cm");
        table.AddColumn("8.4cm");

        var r1 = table.AddRow();
        LabelValueCell(r1.Cells[0], "Application Id:", app.AppId);
        LabelValueCell(r1.Cells[1], "City of Project Site:", app.SiteCityName ?? app.SiteCityCode ?? "");

        var r2 = table.AddRow();
        r2.Cells[0].MergeRight = 1;
        LabelValueCell(r2.Cells[0], "Site Location:", app.SiteLocation ?? "");

        var r3 = table.AddRow();
        LabelValueCell(r3.Cells[0], "Project Start Date:", Date(app.ProjStartDate));
        LabelValueCell(r3.Cells[1], "Completion Date:", Date(app.ProjEndDate));

        var r4 = table.AddRow();
        r4.Cells[0].MergeRight = 1;
        LabelValueCell(r4.Cells[0], "Agency Representative:", "Alameda County Water Resources Section   ·   (510) 670-6633");

        section.AddParagraph().Format.SpaceAfter = 6;
    }

    private static void BuildParties(Section section, PermitDocumentModel model)
    {
        var app = model.Application;

        var applicantName = JoinName(
            string.IsNullOrWhiteSpace(app.Applicant.AppBusinessName) ? null : $"{app.Applicant.AppBusinessName} -",
            app.Applicant.AppFirstName, app.Applicant.AppLastName);
        var applicantAddr = FullAddress(app.Applicant.AppAddrStreet, app.Applicant.AppAddrStreet2, app.Applicant.AppAddrCity, app.Applicant.AppAddrState, app.Applicant.AppAddrZip);

        var ownerName = JoinName(app.Owner.FirstName, app.Owner.LastName);
        var ownerAddr = FullAddress(app.Owner.AddrStreet, null, app.Owner.AddrCity, app.Owner.AddrState, app.Owner.AddrZip);

        var clientName = JoinName(app.Client.FirstName, app.Client.LastName);
        var clientAddr = FullAddress(app.Client.AddrStreet, null, app.Client.AddrCity, app.Client.AddrState, app.Client.AddrZip);

        var contactName = JoinName(app.Contact.ContactFirstName, app.Contact.ContactLastName);

        var table = section.AddTable();
        table.Borders.Width = 0;
        table.Borders.Top = new Border { Width = 0.5, Color = BorderClr };
        table.AddColumn("11.6cm");
        table.AddColumn("5.2cm");

        PartyRow(table, "Applicant:", applicantName, applicantAddr, PhoneLines(("Phone:", app.Applicant.AppPhone)), first: true);
        PartyRow(table, "Property Owner:", ownerName, ownerAddr, PhoneLines(("Phone:", app.Owner.Phone)), first: false);

        var clientDisplay = string.IsNullOrEmpty(clientName) ? "** same as Property Owner **" : clientName;
        PartyRow(table, "Client:", clientDisplay, string.IsNullOrEmpty(clientName) ? null : clientAddr,
            string.IsNullOrEmpty(clientName) ? Array.Empty<(string, string?)>() : PhoneLines(("Phone:", app.Client.Phone)), first: false);

        if (!string.IsNullOrEmpty(contactName))
        {
            PartyRow(table, "Contact:", contactName, app.Contact.ContactEmail,
                PhoneLines(("Phone:", app.Contact.ContactPhone), ("Cell:", app.Contact.ContactCell)), first: false);
        }

        section.AddParagraph().Format.SpaceAfter = 6;
    }

    // Human description for a payment-type code, mirroring the intra app's formatPaymentType
    // (EEAOWN.PAYMENT_TYPES) so the emailed permit's "Paid By" matches the printed permit.
    private static string PaymentTypeLabel(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant() switch
    {
        "CC" => "Credit Card",
        "CHECK" => "Check",
        "CK" => "Check",
        "CASH" => "Cash",
        "EXMPT" => "Exempt",
        "MC" => "Master Card",
        "VISA" => "VISA",
        "" => "—",
        var other => other,
    };

    private static void BuildPayment(Section section, PermitDocumentModel model)
    {
        var p = model.Payment;
        var works = model.Application.Works;
        var serviceCharge = p?.ServiceCharge ?? 0m;
        var fine = p?.FineAmount ?? 0m;
        var authAmount = p?.AuthAmount ?? 0m;
        // Total Due is the full amount owed for the permit — base/authorized fee (+ service charge)
        // PLUS any fine assessed during review. The fine is persisted on the payment row, so include
        // it here; otherwise the emailed permit understates the total by the fine amount.
        var totalDue = (authAmount > 0 ? authAmount : FeeCalculator.BaseFee(works) + serviceCharge) + fine;
        var paid = p?.PaidAmount ?? 0m;
        var payStatus = (p?.StatusCode ?? "").ToUpperInvariant();

        string statusLabel;
        if (payStatus == "PAYFL") statusLabel = "** PAYMENT FAILED **";
        else if (payStatus == "EXMPT") statusLabel = "PAYMENT EXEMPT";
        else if (paid > 0 && paid >= totalDue) statusLabel = "PAID IN FULL";
        else statusLabel = "PAYMENT DUE";
        var paidInFull = statusLabel == "PAID IN FULL";
        var paidByLabel = model.Approved ? "Paid By" : "Payment Type";

        var table = section.AddTable();
        table.Borders.Width = 0;
        table.Rows.LeftIndent = "1.8cm";
        table.AddColumn("8.2cm");
        table.AddColumn("3.8cm");
        table.AddColumn("2.8cm");

        var r1 = table.AddRow();
        var totalDueLbl = r1.Cells[1].AddParagraph();
        totalDueLbl.Format.Alignment = ParagraphAlignment.Right;
        totalDueLbl.AddFormattedText("Total Due:", new Font { Bold = true, Color = Navy });
        MoneyCell(r1.Cells[2], Money(totalDue), bold: true);

        var r2 = table.AddRow();
        r2.Cells[0].AddParagraph($"Receipt Number: {p?.ReceiptNum ?? "—"}").Format.Font.Size = 9;
        var totalPaidLbl = r2.Cells[1].AddParagraph();
        totalPaidLbl.Format.Alignment = ParagraphAlignment.Right;
        totalPaidLbl.AddFormattedText("Total Amount Paid:", new Font { Bold = true, Color = Navy });
        var paidCell = MoneyCell(r2.Cells[2], Money(paid), bold: true);
        paidCell.Format.Borders.Top = new Border { Width = 0.75, Color = new Color(26, 26, 26) };

        var r3 = table.AddRow();
        r3.Cells[0].AddParagraph($"Payer Name: {p?.AcctName ?? ""}").Format.Font.Size = 9;
        var paidByPara = r3.Cells[1].AddParagraph();
        paidByPara.Format.Alignment = ParagraphAlignment.Right;
        paidByPara.AddFormattedText($"{paidByLabel}: {PaymentTypeLabel(p?.PaymentType)}", new Font { Color = Navy });
        var status = r3.Cells[2].AddParagraph(statusLabel);
        status.Format.Alignment = ParagraphAlignment.Right;
        status.Format.Font.Bold = true;
        status.Format.Font.Size = 9.5;
        status.Format.Font.Color = paidInFull ? OkGreen : Navy;

        section.AddParagraph().Format.SpaceAfter = 6;
    }

    private static void BuildWorks(Section section, PermitDocumentModel model)
    {
        var heading = section.AddParagraph("Works Requesting Permits");
        heading.Format.Font.Size = 11.5;
        heading.Format.Font.Bold = true;
        heading.Format.Font.Color = Navy;
        heading.Format.Borders.Bottom = new Border { Width = 1, Color = Navy };
        heading.Format.SpaceAfter = 6;

        var permitsByWork = (model.PermitInfo?.Permits ?? Array.Empty<PWA.PermitsApi.Application.DTOs.PermitLineDto>())
            .GroupBy(x => x.WorkId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var work in model.Application.Works)
        {
            var cancelled = string.Equals(work.StatusCode, "CAN", StringComparison.OrdinalIgnoreCase);
            var wells = FeeCalculator.CountWells(work);
            var isBorehole = BoreholeCategories.Contains((work.WorkCategory ?? "").ToLowerInvariant());
            var workName = string.Join(" - ", new[]
            {
                Desc(work.WorkCategoryDesc, work.WorkCategory),
                Desc(work.WorkTypeDesc, work.WorkType),
                Desc(work.WellUseDesc, work.WellUseType)
            }.Where(s => !string.IsNullOrWhiteSpace(s)));
            var drillMethod = Desc(work.DrillMethodName, work.DrillMethodType);
            var driller = JoinName(work.DrillerName)
                + (string.IsNullOrWhiteSpace(work.DrillerLicenseNum) ? "" : $" — Lic #: {work.DrillerLicenseNum}")
                + (string.IsNullOrWhiteSpace(drillMethod) ? "" : $" — Method: {drillMethod}");
            var lines = permitsByWork.TryGetValue(work.WorkId, out var l) ? l : new List<PWA.PermitsApi.Application.DTOs.PermitLineDto>();

            var head = section.AddTable();
            head.Borders.Width = 0;
            head.KeepTogether = true;
            head.AddColumn("11.6cm");
            head.AddColumn("5.2cm");
            var hr = head.AddRow();
            var hp = hr.Cells[0].AddParagraph();
            hp.Format.Font.Bold = true;
            hp.AddText($"{workName} — {wells} {(isBorehole ? "Boreholes" : "Wells")}");
            var tp = hr.Cells[1].AddParagraph($"Work Total: {Money(cancelled ? 0m : FeeCalculator.WorkCalcAmount(work))}");
            tp.Format.Alignment = ParagraphAlignment.Right;
            tp.Format.Font.Bold = true;

            var drillerP = section.AddParagraph($"Driller: {(string.IsNullOrWhiteSpace(driller) ? "—" : driller)}");
            drillerP.Format.Font.Size = 8.5;
            drillerP.Format.Font.Color = Muted;
            drillerP.Format.SpaceAfter = 2;

            if (cancelled)
            {
                var cx = section.AddParagraph("** Cancelled Work. Total amount adjusted. **");
                cx.Format.Font.Size = 8.5;
                cx.Format.Font.Italic = true;
                cx.Format.Font.Color = new Color(176, 42, 55);
            }

            var specsLabel = section.AddParagraph("Specifications");
            specsLabel.Format.Font.Size = 7.8;
            specsLabel.Format.Font.Bold = true;
            specsLabel.Format.Font.Color = Muted;
            specsLabel.Format.SpaceBefore = 2;
            specsLabel.Format.SpaceAfter = 2;

            BuildSpecsTable(section, work, isBorehole, cancelled, lines);
            section.AddParagraph().Format.SpaceAfter = 6;
        }
    }

    private static void BuildSpecsTable(Section section, DomainWork work, bool isBorehole, bool cancelled, List<PWA.PermitsApi.Application.DTOs.PermitLineDto> lines)
    {
        var specs = work.Specs;
        var hasStateWell = !isBorehole && specs.Any(s => !string.IsNullOrWhiteSpace(s.StateWellId));
        var hasOrigPermit = !isBorehole && specs.Any(s => !string.IsNullOrWhiteSpace(s.PermitNum));
        var hasDwr = !isBorehole && specs.Any(s => !string.IsNullOrWhiteSpace(s.DwrNum));

        var headers = new List<string> { "Permit #", "Issued", "Expires", isBorehole ? "# Boreholes" : "Owner Well Id", "Hole Diam." };
        if (!isBorehole) { headers.Add("Casing Diam."); headers.Add("Seal Depth"); }
        headers.Add("Max. Depth");
        if (hasStateWell) headers.Add("State Well #");
        if (hasOrigPermit) headers.Add("Orig. Permit #");
        if (hasDwr) headers.Add("DWR #");

        var table = NewGridTable(section);
        table.KeepTogether = true;
        var colCount = headers.Count;
        var contentWidth = 16.6;
        var colWidth = contentWidth / colCount;
        for (var i = 0; i < colCount; i++) table.AddColumn(Unit.FromCentimeter(colWidth));

        var hr = table.AddRow();
        hr.Shading.Color = SpecHeaderFill;
        hr.Format.Font.Bold = true;
        hr.Format.Font.Color = Navy;
        hr.Format.Font.Size = 7.8;
        for (var i = 0; i < colCount; i++) hr.Cells[i].AddParagraph(headers[i]);

        var alt = false;
        foreach (var spec in specs)
        {
            var line = lines.FirstOrDefault(x => x.WorkSpecsId == spec.WorkSpecsId) ?? (lines.Count > 0 ? lines[0] : null);
            var permitCell = !string.IsNullOrWhiteSpace(line?.PermitNumber)
                ? line!.PermitNumber
                : (cancelled ? "* Cancelled *" : "* Pending Approval *");

            var row = table.AddRow();
            row.Format.Font.Size = 8;
            if (alt) row.Shading.Color = RowAlt;
            alt = !alt;

            var cells = new List<string>
            {
                permitCell,
                line is not null ? Date(line.IssuedDate) : "",
                line is not null ? Date(line.ExpireDate) : "",
                isBorehole ? (spec.DrillCount?.ToString(CultureInfo.InvariantCulture) ?? "") : (spec.OwnerWellNum ?? ""),
                spec.HoleDiamIn is not null ? $"{Num(spec.HoleDiamIn)} in." : "",
            };
            if (!isBorehole)
            {
                cells.Add(spec.CasingDiamIn is not null ? $"{Num(spec.CasingDiamIn)} in." : "");
                cells.Add(spec.SealDepthFt is not null ? $"{Num(spec.SealDepthFt)} ft" : "");
            }
            cells.Add(spec.MaxDepthFt is not null ? $"{Num(spec.MaxDepthFt)} ft" : "");
            if (hasStateWell) cells.Add(spec.StateWellId ?? "");
            if (hasOrigPermit) cells.Add(spec.PermitNum ?? "");
            if (hasDwr) cells.Add(spec.DwrNum ?? "");

            for (var i = 0; i < colCount; i++) row.Cells[i].AddParagraph(cells[i]);
        }

        if (specs.Count == 0)
        {
            var row = table.AddRow();
            row.Cells[0].MergeRight = colCount - 1;
            var p = row.Cells[0].AddParagraph("No specifications recorded.");
            p.Format.Font.Size = 8;
            p.Format.Font.Italic = true;
        }
    }

    private static void BuildConditions(Section section, PermitDocumentModel model)
    {
        if (model.Conditions.Count == 0) return;

        var heading = section.AddParagraph("Specific Work Permit Conditions");
        heading.Format.Font.Size = 11.5;
        heading.Format.Font.Bold = true;
        heading.Format.Font.Color = Navy;
        heading.Format.Borders.Bottom = new Border { Width = 1, Color = Navy };
        heading.Format.SpaceBefore = 4;
        heading.Format.SpaceAfter = 6;

        var i = 1;
        foreach (var condition in model.Conditions)
        {
            var p = section.AddParagraph();
            p.Format.LeftIndent = "0.6cm";
            p.Format.FirstLineIndent = "-0.6cm";
            p.Format.SpaceAfter = 3;
            p.Format.Font.Size = 9;
            p.AddFormattedText($"{i}. ", new Font { Bold = true });
            p.AddText(condition);
            i++;
        }
    }

    private static void BuildFooter(Section section, PermitDocumentModel model)
    {
        var rule = section.AddParagraph();
        rule.Format.Borders.Top = new Border { Width = 2, Color = Navy };
        rule.Format.SpaceBefore = 14;
        rule.Format.SpaceAfter = 4;

        var footer = section.AddParagraph(
            $"Alameda County Public Works Agency · Water Resources Section — Application {model.Application.AppId}");
        footer.Format.Alignment = ParagraphAlignment.Center;
        footer.Format.Font.Size = 8;
        footer.Format.Font.Color = FooterClr;
    }

    /// <summary>
    /// Overlays a faint, diagonal "PREVIEW" watermark on every page for non-approved permits,
    /// mirroring the intra React <c>.permit-watermark</c> (rgba(200,137,42,0.08), rotated -30deg).
    /// </summary>
    private static void DrawPreviewWatermark(PdfDocument pdf)
    {
        var font = new XFont("Arial", 96, XFontStyleEx.Bold);
        var brush = new XSolidBrush(XColor.FromArgb(24, 200, 137, 42));
        foreach (PdfPage page in pdf.Pages)
        {
            using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
            var state = gfx.Save();
            gfx.TranslateTransform(page.Width.Point / 2, page.Height.Point / 2);
            gfx.RotateTransform(-30);
            gfx.DrawString("PREVIEW", font, brush, new XPoint(0, 0), XStringFormats.Center);
            gfx.Restore(state);
        }
    }

    // ---------------------------------------------------------------- helpers ------

    private static Table NewGridTable(Section section)
    {
        var table = section.AddTable();
        table.Borders.Width = 0.5;
        table.Borders.Color = SpecBorder;
        table.LeftPadding = 3;
        table.RightPadding = 3;
        table.TopPadding = 2;
        table.BottomPadding = 2;
        return table;
    }

    private static void LabelValueCell(Cell cell, string label, string value)
    {
        var p = cell.AddParagraph();
        p.Format.Font.Size = 9;
        p.AddFormattedText(label + " ", new Font { Bold = true, Color = Navy });
        p.AddText(value);
    }

    private static void PartyRow(Table table, string label, string name, string? addr, IReadOnlyList<(string Label, string? Value)> phones, bool first)
    {
        var row = table.AddRow();
        row.TopPadding = 3;
        row.BottomPadding = 3;
        row.Borders.Top = first
            ? new Border { Width = 0 }
            : new Border { Width = 0.5, Color = BorderClr, Style = BorderStyle.Dot };

        var main = row.Cells[0].AddParagraph();
        main.Format.Font.Size = 9.5;
        main.AddFormattedText(label + " ", new Font { Bold = true, Color = Navy });
        main.AddText(string.IsNullOrEmpty(name) ? "—" : name);
        if (!string.IsNullOrWhiteSpace(addr))
        {
            var addrP = row.Cells[0].AddParagraph(addr);
            addrP.Format.Font.Size = 8.5;
            addrP.Format.Font.Color = Muted;
        }

        foreach (var (plabel, value) in phones)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            var pp = row.Cells[1].AddParagraph();
            pp.Format.Alignment = ParagraphAlignment.Right;
            pp.Format.Font.Size = 9;
            pp.AddFormattedText(plabel + " ", new Font { Bold = true, Color = Navy });
            pp.AddText(value);
        }
    }

    private static Paragraph MoneyCell(Cell cell, string text, bool bold)
    {
        var p = cell.AddParagraph(text);
        p.Format.Alignment = ParagraphAlignment.Right;
        p.Format.Font.Bold = bold;
        p.Format.Font.Size = 9.5;
        return p;
    }

    private static (string, string?)[] PhoneLines(params (string Label, string? Value)[] items) => items;

    private static string BuildPermitSummary(PermitDocumentModel model)
    {
        var numbers = (model.PermitInfo?.Permits ?? Array.Empty<PWA.PermitsApi.Application.DTOs.PermitLineDto>())
            .Select(p => p.PermitNumber)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();
        if (numbers.Count > 1) return $"{numbers[0]} to {numbers[^1]}";
        return numbers.Count == 1 ? numbers[0] : "";
    }

    private static string Money(decimal n) => "$" + n.ToString("F2", CultureInfo.InvariantCulture);

    private static string Num(decimal? n) => n?.ToString("0.##", CultureInfo.InvariantCulture) ?? "";

    private static string Date(DateTime? value) =>
        value.HasValue ? value.Value.ToString("M/d/yyyy", CultureInfo.InvariantCulture) : "";

    private static string JoinName(params string?[] parts) =>
        string.Join(" ", parts.Select(p => (p ?? "").Trim()).Where(s => s.Length > 0));

    // Prefer the human-readable lookup description; fall back to the raw code when the
    // description isn't available so nothing renders blank.
    private static string Desc(string? description, string? code) =>
        !string.IsNullOrWhiteSpace(description) ? description.Trim() : (code ?? string.Empty).Trim();

    private static string FullAddress(string? street, string? street2, string? city, string? state, string? zip)
    {
        var line1 = string.Join(" ", new[] { street, street2 }.Select(p => (p ?? "").Trim()).Where(s => s.Length > 0));
        var left = string.Join(", ", new[] { city, state }.Select(p => (p ?? "").Trim()).Where(s => s.Length > 0));
        var cityStateZip = string.Join("  ", new[] { left, (zip ?? "").Trim() }.Where(s => s.Length > 0));
        return string.Join(", ", new[] { line1, cityStateZip }.Where(s => s.Length > 0));
    }

    private static string? WriteLogoTempFile()
    {
        if (LogoBytes is null) return null;
        try
        {
            var path = Path.Combine(Path.GetTempPath(), $"pwa-permit-logo-{Guid.NewGuid():N}.png");
            File.WriteAllBytes(path, LogoBytes);
            return path;
        }
        catch
        {
            return null;
        }
    }

    private static byte[]? LoadLogo()
    {
        var asm = typeof(PermitDocumentService).Assembly;
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("pwa-logo.png", StringComparison.OrdinalIgnoreCase));
        if (name is null) return null;
        using var stream = asm.GetManifestResourceStream(name);
        if (stream is null) return null;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    private static void EnsureFonts()
    {
        if (_fontsReady) return;
        lock (FontLock)
        {
            if (_fontsReady) return;
            if (GlobalFontSettings.FontResolver is null)
            {
                GlobalFontSettings.FontResolver = new WindowsArialFontResolver();
            }
            _fontsReady = true;
        }
    }

    /// <summary>
    /// Minimal font resolver that maps every requested family to Arial (regular/bold/italic) from the
    /// Windows font directory. The API runs on Windows, so these TrueType files are always present;
    /// this keeps PDF generation deterministic without embedding font binaries.
    /// </summary>
    private sealed class WindowsArialFontResolver : IFontResolver
    {
        public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic) => (bold, italic) switch
        {
            (true, true) => new FontResolverInfo("Arial#BI"),
            (true, false) => new FontResolverInfo("Arial#B"),
            (false, true) => new FontResolverInfo("Arial#I"),
            _ => new FontResolverInfo("Arial#")
        };

        public byte[]? GetFont(string faceName)
        {
            var file = faceName switch
            {
                "Arial#BI" => "arialbi.ttf",
                "Arial#B" => "arialbd.ttf",
                "Arial#I" => "ariali.ttf",
                _ => "arial.ttf"
            };
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), file);
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
    }
}
