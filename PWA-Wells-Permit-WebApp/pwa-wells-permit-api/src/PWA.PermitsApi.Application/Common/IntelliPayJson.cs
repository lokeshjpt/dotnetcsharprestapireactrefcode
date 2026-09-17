using System.Text.RegularExpressions;

namespace PWA.PermitsApi.Application.Common;

/// <summary>
/// PCI-safe helpers for parsing IntelliPay JSON responses and building the payment_history
/// entries persisted to APP_PAYMENT_INFO. Mirrors the legacy Java
/// <c>IntelliPayService.extractJsonField</c> / <c>stripPciFields</c> exactly so the two apps
/// produce identical history payloads. PCI-sensitive fields are stripped before anything is
/// stored or logged.
/// </summary>
public static class IntelliPayJson
{
    // Fields that must never be persisted or logged (PCI). Matches the legacy PCI_FIELDS set.
    private static readonly string[] PciFields =
    {
        "number", "card_number", "cvv", "cvv2", "csc",
        "cardnumber", "cardnumdisplay", "nonce", "hmac",
        "track1", "track2", "pin", "token"
    };

    /// <summary>
    /// Extracts a top-level JSON field value (quoted first, then numeric). Returns empty when
    /// the field is absent — mirrors legacy IntelliPayService.extractJsonField.
    /// </summary>
    public static string ExtractField(string? json, string field)
    {
        if (string.IsNullOrEmpty(json))
        {
            return string.Empty;
        }

        var quoted = Regex.Match(json, "\"" + Regex.Escape(field) + "\"\\s*:\\s*\"([^\"\\\\]*)\"", RegexOptions.IgnoreCase);
        if (quoted.Success)
        {
            return quoted.Groups[1].Value;
        }

        var numeric = Regex.Match(json, "\"" + Regex.Escape(field) + "\"\\s*:\\s*([0-9]+)", RegexOptions.IgnoreCase);
        return numeric.Success ? numeric.Groups[1].Value : string.Empty;
    }

    /// <summary>
    /// Removes PCI-sensitive fields from a raw IntelliPay JSON response before it is stored in
    /// payment_history. Mirrors legacy IntelliPayService.stripPciFields.
    /// </summary>
    public static string StripPciFields(string? json)
    {
        if (json is null)
        {
            return "{}";
        }

        var result = json;
        foreach (var field in PciFields)
        {
            result = Regex.Replace(result, "\"" + Regex.Escape(field) + "\"\\s*:\\s*\"[^\"]*\"\\s*,?", string.Empty, RegexOptions.IgnoreCase);
            result = Regex.Replace(result, "\"" + Regex.Escape(field) + "\"\\s*:\\s*[0-9]+\\s*,?", string.Empty, RegexOptions.IgnoreCase);
        }

        result = Regex.Replace(result, ",\\s*}", "}");
        result = Regex.Replace(result, ",\\s*,", ",");
        return result;
    }

    /// <summary>
    /// Builds a PCI-stripped, source-tagged payment_history entry from a raw IntelliPay response.
    /// When <paramref name="paymentId"/> is supplied it is injected after the source tag (used for
    /// the payment_details read entry). Mirrors the entry construction in
    /// ProcessApprovalServlet.chargeStoredCC.
    /// </summary>
    public static string BuildHistoryEntry(string? rawJson, string source, string? paymentId = null)
    {
        var safe = StripPciFields(rawJson);
        if (!safe.StartsWith("{", StringComparison.Ordinal))
        {
            return safe;
        }

        var prefix = string.IsNullOrEmpty(paymentId)
            ? "{\"source\":\"" + source + "\","
            : "{\"source\":\"" + source + "\",\"paymentid\":" + paymentId + ",";
        return prefix + safe.Substring(1);
    }

    /// <summary>
    /// Builds the PCI-safe vault snapshot JSON object stored as the first payment_history entry at
    /// submit time. Field set/order mirrors the legacy Java ecomm ProcessAppServlet snapshot
    /// (timestamp, call, custid, status, authcode, amount, fee, paymenttype, cardbrand, account,
    /// declinereason). Values are JSON-escaped; PCI fields are never included.
    /// </summary>
    public static string BuildVaultSnapshot(
        string? call, string custid, string? status, string? authCode,
        string? amount, string? fee, string paymentType, string? cardBrand,
        string account, string? declineReason)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        var entry = new
        {
            timestamp,
            call = call ?? string.Empty,
            custid = custid ?? string.Empty,
            status = status ?? string.Empty,
            authcode = authCode ?? string.Empty,
            amount = amount ?? string.Empty,
            fee = fee ?? string.Empty,
            paymenttype = paymentType ?? string.Empty,
            cardbrand = cardBrand ?? string.Empty,
            account = account ?? string.Empty,
            declinereason = declineReason ?? string.Empty
        };
        return System.Text.Json.JsonSerializer.Serialize(entry);
    }
}
