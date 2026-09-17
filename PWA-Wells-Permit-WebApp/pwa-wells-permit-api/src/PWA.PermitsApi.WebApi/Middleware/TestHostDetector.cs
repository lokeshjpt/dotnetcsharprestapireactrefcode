namespace PWA.PermitsApi.WebApi.Middleware;

/// <summary>
/// Detects whether the current process is a unit/integration test host (xUnit, NUnit, MSTest or the
/// VS test platform). Audit and exception emails are suppressed when this is true so an automated test
/// run never pages the audit distribution list. A production deployment never loads these assemblies,
/// so <see cref="IsTestHost"/> is always <c>false</c> in production and genuine 401/exception audit
/// emails continue to be sent there.
/// </summary>
public static class TestHostDetector
{
    public static bool IsTestHost { get; } = Detect();

    private static bool Detect()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var name = assembly.GetName().Name;
            if (name is null)
            {
                continue;
            }

            if (name.StartsWith("xunit", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("nunit", StringComparison.OrdinalIgnoreCase)
                || name.Contains("testhost", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Microsoft.VisualStudio.TestPlatform", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Microsoft.TestPlatform", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
