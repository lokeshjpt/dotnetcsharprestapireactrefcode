using Serilog.Context;

namespace PWA.PermitsApi.WebApi.Middleware;

/// <summary>
/// Assigns a correlation id to every request — taken from the inbound <c>X-Correlation-ID</c> header
/// when the caller supplies one, otherwise a fresh GUID — echoes it back on the response, and pushes
/// it into the Serilog <see cref="LogContext"/> so it renders as <c>{CorrelationId}</c> on every log
/// line for the request. The sibling ESPOS / MTA-Tracker APIs reference <c>{CorrelationId}</c> in the
/// file output template but never populate it; this middleware makes it functional.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var supplied)
            && !string.IsNullOrWhiteSpace(supplied)
                ? supplied.ToString()
                : Guid.NewGuid().ToString();

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
