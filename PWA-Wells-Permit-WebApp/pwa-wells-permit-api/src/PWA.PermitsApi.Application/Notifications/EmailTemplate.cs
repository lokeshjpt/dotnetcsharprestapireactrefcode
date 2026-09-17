using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PWA.PermitsApi.Application.Notifications;

/// <summary>
/// A reusable, professionally branded HTML email shell for all Alameda County PWA Wells Permits
/// notifications. Every message is wrapped in the same responsive, table-based, inline-styled layout
/// (email-client safe) with an agency header band, a coloured accent bar that signals the message
/// intent (blue = informational, green = approved, amber = action required, red = problem), and a
/// consistent footer. This replaces the legacy inline <c>.contentBDM</c> tables with a modern look
/// while preserving all of the original content.
/// </summary>
public static class EmailTemplate
{
    public const string AccentBlue = "#1f4e79";   // informational / confirmation
    public const string AccentGreen = "#1e7e34";  // approval / success
    public const string AccentAmber = "#b26a00";  // action required
    public const string AccentRed = "#b02a37";    // decline / error

    private const string HeaderNavy = "#003366";
    private const string PageBg = "#eef1f5";
    private const string CardBg = "#ffffff";
    private const string TextColor = "#1f2933";
    private const string MutedColor = "#6b7280";
    private const string BorderColor = "#e3e8ee";
    private const string FontStack = "-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif";

    /// <summary>Content-ID used to reference the inline agency logo embedded by the email sender.</summary>
    public const string LogoContentId = "pwaLogo";

    /// <summary>
    /// Placeholder emitted at the very top of every wrapped message body. The notification layer
    /// replaces it (via <see cref="ApplyEnvironmentBanner"/>) with a prominent non-production warning
    /// banner in every environment except production, and with an empty string in production.
    /// </summary>
    public const string EnvironmentBannerMarker = "<!--PWA_ENV_BANNER-->";

    /// <summary>
    /// Wraps the supplied inner body HTML in the branded shell defined by the embedded
    /// <c>Templates/Layout.html</c> file. Only the dynamic values (title, preheader, accent colour,
    /// heading block, body, footer year, logo cid, and the environment-banner marker) are supplied as
    /// placeholders — all of the static markup lives in the HTML template.
    /// </summary>
    /// <param name="heading">Large heading shown at the top of the message body.</param>
    /// <param name="preheader">Hidden inbox-preview snippet.</param>
    /// <param name="accentColor">Accent bar / heading colour (use the Accent* constants).</param>
    /// <param name="bodyHtml">Inner HTML (paragraphs, tables, buttons) built with the helpers below.</param>
    public static string Wrap(string heading, string preheader, string accentColor, string bodyHtml)
    {
        var headingBlock = string.IsNullOrEmpty(heading)
            ? string.Empty
            : "<h1 style=\"margin:0 0 16px 0;font-size:21px;line-height:1.3;color:" + accentColor + ";\">" + Encode(heading) + "</h1>";

        return EmailTemplateStore.Render("Layout.html", new Dictionary<string, string?>
        {
            ["Title"] = Encode(heading),
            ["Preheader"] = Encode(preheader),
            ["Accent"] = accentColor,
            ["LogoCid"] = LogoContentId,
            ["HeadingBlock"] = headingBlock,
            ["Body"] = bodyHtml,
            ["Year"] = DateTime.Now.Year.ToString(),
        });
    }

    /// <summary>
    /// Replaces the <see cref="EnvironmentBannerMarker"/> in a wrapped message body with a prominent
    /// non-production warning banner when <paramref name="nonProduction"/> is true, or with an empty
    /// string (production) otherwise. Safe to call on any HTML: when the marker is absent it is a
    /// no-op.
    /// </summary>
    public static string ApplyEnvironmentBanner(string? html, bool nonProduction, string? environmentName)
    {
        if (string.IsNullOrEmpty(html))
        {
            return html ?? string.Empty;
        }

        var banner = nonProduction ? EnvironmentBanner(environmentName) : string.Empty;
        return html!.Replace(EnvironmentBannerMarker, banner);
    }

    /// <summary>
    /// The non-production warning banner shown at the top of every email body outside production,
    /// carrying the uppercase environment label. Rendered from the embedded
    /// <c>Templates/EnvironmentBanner.html</c> file.
    /// </summary>
    public static string EnvironmentBanner(string? environmentName)
    {
        var env = string.IsNullOrWhiteSpace(environmentName) ? "NON-PROD" : environmentName.Trim().ToUpperInvariant();
        return EmailTemplateStore.Render("EnvironmentBanner.html", new Dictionary<string, string?>
        {
            ["EnvUpper"] = Encode(env),
        });
    }

    /// <summary>A standard body paragraph.</summary>
    public static string Paragraph(string html) =>
        "<p style=\"margin:0 0 14px 0;font-size:14px;line-height:1.6;color:" + TextColor + ";\">" + html + "</p>";

    /// <summary>A muted/secondary paragraph (smaller, grey).</summary>
    public static string MutedParagraph(string html) =>
        "<p style=\"margin:0 0 14px 0;font-size:12.5px;line-height:1.6;color:" + MutedColor + ";\">" + html + "</p>";

    /// <summary>A coloured callout box for emphasis (reminders, action-required, decline reasons).</summary>
    public static string Callout(string html, string accentColor)
    {
        var bg = TintFor(accentColor);
        return "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"margin:0 0 16px 0;\"><tr>"
            + "<td bgcolor=\"" + bg + "\" style=\"background:" + bg + ";border-left:4px solid " + accentColor
            + ";border-radius:6px;padding:12px 16px;font-size:13.5px;line-height:1.6;color:" + TextColor + ";\">"
            + html + "</td></tr></table>";
    }

    /// <summary>A clean two-column label/value details table.</summary>
    public static string DetailsTable(IEnumerable<(string Label, string Value)> rows)
    {
        var sb = new StringBuilder();
        sb.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" ")
          .Append("style=\"margin:0 0 18px 0;border:1px solid ").Append(BorderColor).Append(";border-radius:8px;border-collapse:separate;overflow:hidden;\">");
        var i = 0;
        foreach (var (label, value) in rows)
        {
            var rowBg = i % 2 == 0 ? "#f7f9fc" : "#ffffff";
            sb.Append("<tr>")
              .Append("<td bgcolor=\"").Append(rowBg).Append("\" style=\"background:").Append(rowBg).Append(";padding:9px 14px;font-size:13px;font-weight:600;color:")
              .Append(HeaderNavy).Append(";width:42%;vertical-align:top;border-bottom:1px solid ").Append(BorderColor).Append(";\">")
              .Append(Encode(label)).Append("</td>")
              .Append("<td bgcolor=\"").Append(rowBg).Append("\" style=\"background:").Append(rowBg).Append(";padding:9px 14px;font-size:13px;color:")
              .Append(TextColor).Append(";vertical-align:top;border-bottom:1px solid ").Append(BorderColor).Append(";\">")
              .Append(string.IsNullOrEmpty(value) ? "&nbsp;" : Encode(value)).Append("</td>")
              .Append("</tr>");
            i++;
        }
        sb.Append("</table>");
        return sb.ToString();
    }

    /// <summary>A prominent call-to-action button (falls back to a styled link in all clients).</summary>
    public static string Button(string text, string url, string accentColor)
    {
        return "<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" style=\"margin:6px 0 18px 0;\"><tr>"
            + "<td style=\"border-radius:6px;background:" + accentColor + ";\">"
            + "<a href=\"" + EncodeAttr(url) + "\" style=\"display:inline-block;padding:11px 22px;font-size:14px;font-weight:600;"
            + "color:#ffffff;text-decoration:none;border-radius:6px;\">" + Encode(text) + "</a>"
            + "</td></tr></table>";
    }

    /// <summary>Section sub-heading used inside the body.</summary>
    public static string SectionHeading(string text) =>
        "<h2 style=\"margin:22px 0 10px 0;font-size:15px;color:" + HeaderNavy + ";border-bottom:2px solid " + BorderColor + ";padding-bottom:6px;\">"
        + Encode(text) + "</h2>";

    /// <summary>Signature block used to close applicant messages.</summary>
    public static string Signature() =>
        Paragraph("Thank you,<br><strong>Public Works Agency &mdash; Water Resources</strong>");

    /// <summary>Renders an arbitrary HTML table (already-built markup) — used for the works line-items.</summary>
    public static string RawTable(string innerRowsHtml, params string[] headers)
    {
        var sb = new StringBuilder();
        sb.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" ")
          .Append("style=\"margin:0 0 18px 0;border:1px solid ").Append(BorderColor).Append(";border-radius:8px;border-collapse:collapse;\">");
        sb.Append("<tr>");
        foreach (var h in headers)
        {
            sb.Append("<th align=\"left\" style=\"background:").Append(HeaderNavy)
              .Append(";color:#ffffff;font-size:12px;font-weight:600;padding:9px 12px;\">").Append(Encode(h)).Append("</th>");
        }
        sb.Append("</tr>");
        sb.Append(innerRowsHtml);
        sb.Append("</table>");
        return sb.ToString();
    }

    private static string TintFor(string accent)
    {
        if (accent == AccentGreen) return "#e9f5ec";
        if (accent == AccentAmber) return "#fbf1e0";
        if (accent == AccentRed) return "#fbe9eb";
        return "#e9f0f8";
    }

    public static string Encode(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty :
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    /// <summary>Encodes a value for safe use inside an HTML attribute (e.g. an <c>href</c>).</summary>
    public static string EncodeAttr(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty :
        value.Replace("&", "&amp;").Replace("\"", "&quot;");
}
