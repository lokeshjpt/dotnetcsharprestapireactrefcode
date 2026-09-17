using System.Text;

namespace PWA.PermitsApi.Application.Common;

/// <summary>
/// Normalizes phone/fax/cell values to digits-only before persistence.
///
/// The legacy Java app stores raw digits and re-formats them for display using fixed-position
/// substrings (area = chars 0-3, prefix = 3-6, suffix = 6-10, extension = 10+). Persisting a
/// pre-formatted value such as "999-999-9999" shifts those offsets and renders as
/// "999--99-9-99 x99", so every write path must strip separators first and store digits only.
/// </summary>
public static class PhoneNormalizer
{
    /// <summary>
    /// Returns the input with all non-digit characters removed. Null input is preserved as null.
    /// An input containing no digits yields an empty string.
    /// </summary>
    public static string? DigitsOnly(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsDigit(ch))
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }
}
