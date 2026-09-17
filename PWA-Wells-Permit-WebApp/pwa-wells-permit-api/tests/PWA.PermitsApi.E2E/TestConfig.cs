namespace PWA.PermitsApi.E2E;

/// <summary>
/// Central configuration for the E2E suite. Values can be overridden with
/// environment variables so the same tests run against local / CI hosts.
/// </summary>
public static class TestConfig
{
    public static string UiBaseUrl =>
        Environment.GetEnvironmentVariable("E2E_UI_BASE_URL")?.TrimEnd('/')
        ?? "http://localhost:3000";

    public static string ApiBaseUrl =>
        Environment.GetEnvironmentVariable("E2E_API_BASE_URL")?.TrimEnd('/')
        ?? "http://localhost:5242";

    public static string ApiRoot => $"{ApiBaseUrl}/api";
}
