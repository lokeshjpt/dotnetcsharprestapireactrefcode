using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace PWA.PermitsApi.Application.Notifications;

/// <summary>
/// Loads the branded HTML email templates that ship as embedded resources in this assembly (the
/// <c>*.html</c> files under <c>Notifications/Templates</c>) and renders them by substituting
/// <c>{{Placeholder}}</c> tokens with supplied values. Raw template text is cached after first load.
/// <para>
/// This is what "extracts" every email body out of C# string concatenation and into editable HTML
/// files: the <see cref="EmailMessages"/> layer only computes the dynamic values (ids, dates, links,
/// pre-rendered tables) and hands them to a template, which owns all of the static markup/copy.
/// </para>
/// </summary>
internal static class EmailTemplateStore
{
    private static readonly Assembly Asm = typeof(EmailTemplateStore).Assembly;
    private static readonly ConcurrentDictionary<string, string> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Reads (and caches) the raw text of the named embedded template, e.g. "Layout.html".</summary>
    public static string Load(string templateName)
    {
        return Cache.GetOrAdd(templateName, name =>
        {
            var suffix = ".Templates." + name;
            var resource = Asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Embedded email template '{name}' was not found in {Asm.GetName().Name}.");

            using var stream = Asm.GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException($"Embedded email template stream '{resource}' could not be opened.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        });
    }

    /// <summary>
    /// Renders the named template, replacing each <c>{{Key}}</c> occurrence with the matching value
    /// (a null value renders as an empty string, so an optional block simply disappears).
    /// </summary>
    public static string Render(string templateName, IReadOnlyDictionary<string, string?> tokens)
    {
        var sb = new StringBuilder(Load(templateName));
        foreach (var token in tokens)
        {
            sb.Replace("{{" + token.Key + "}}", token.Value ?? string.Empty);
        }
        return sb.ToString();
    }
}
