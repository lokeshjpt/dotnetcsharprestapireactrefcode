namespace PWA.PermitsApi.WebApi.Http;

/// <summary>
/// Resolves the best-known client IP address for a request so audit emails can identify the caller.
/// Prefers the origin client from <c>X-Forwarded-For</c> (when a trusted proxy set it) and falls back
/// to the transport-level <see cref="Microsoft.AspNetCore.Http.ConnectionInfo.RemoteIpAddress"/>.
/// </summary>
public static class ClientIpResolver
{
    /// <summary>
    /// Returns a human-readable client IP for the request, or <c>null</c> when none can be determined.
    /// When an <c>X-Forwarded-For</c> origin differs from the immediate peer, both are shown as
    /// "<c>client (via proxy)</c>" so auditors can see the real caller and the proxy it came through.
    /// </summary>
    public static string? Resolve(HttpContext context)
    {
        var remote = Normalize(context.Connection.RemoteIpAddress?.ToString());

        var forwarded = context.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            // X-Forwarded-For may be a comma-separated list "client, proxy1, proxy2"; the first
            // entry is the original client.
            var client = Normalize(forwarded.Split(',')[0].Trim());
            if (!string.IsNullOrWhiteSpace(client)
                && !string.Equals(client, remote, StringComparison.OrdinalIgnoreCase))
            {
                return string.IsNullOrWhiteSpace(remote) ? client : $"{client} (via {remote})";
            }
        }

        return remote;
    }

    /// <summary>
    /// Normalizes loopback and IPv4-mapped IPv6 forms to their familiar IPv4 text so audit emails
    /// read <c>127.0.0.1</c> / <c>203.0.113.7</c> rather than <c>::1</c> / <c>::ffff:203.0.113.7</c>.
    /// Unrecognized/genuine IPv6 addresses are returned unchanged.
    /// </summary>
    private static string? Normalize(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
        {
            return ip;
        }

        if (!System.Net.IPAddress.TryParse(ip, out var parsed))
        {
            return ip;
        }

        if (System.Net.IPAddress.IsLoopback(parsed))
        {
            return "127.0.0.1";
        }

        return parsed.IsIPv4MappedToIPv6 ? parsed.MapToIPv4().ToString() : parsed.ToString();
    }
}
