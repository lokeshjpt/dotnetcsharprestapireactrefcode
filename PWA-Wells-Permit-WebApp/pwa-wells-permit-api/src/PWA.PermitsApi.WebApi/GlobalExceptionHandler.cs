using Microsoft.AspNetCore.Diagnostics;
using PWA.PermitsApi.Application.Interfaces.Integration;
using PWA.PermitsApi.Application.Notifications;
using PWA.PermitsApi.WebApi.Middleware;

namespace PWA.PermitsApi.WebApi;

/// <summary>
/// Global fallback for any exception not handled by a controller or service catch block. Emails the
/// audit distribution list (best-effort) and returns an RFC 7807 ProblemDetails 500 so callers get a
/// clean error instead of a raw stack trace.
/// </summary>
/// <remarks>
/// Registered as a singleton by <c>AddExceptionHandler&lt;T&gt;</c>, so the scoped
/// <see cref="IPermitNotificationService"/> is resolved from the request scope
/// (<see cref="HttpContext.RequestServices"/>) rather than constructor-injected, to avoid a captive
/// dependency.
/// </remarks>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetails = problemDetails;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var context = $"{httpContext.Request.Method} {httpContext.Request.Path}";
        _logger.LogError(exception, "Unhandled exception for {Context}", context);

        // Emit the audit email here for any exception that propagated up UNLESS a catch block already
        // sent one for it (marked on Exception.Data) — this keeps the rule "rethrown -> global handler
        // audits; silently-handled -> the catch audits" with no duplicates. Suppressed under a test
        // host so an automated test run never pages the audit list; production is never a test host, so
        // unhandled-exception audit emails are always sent there.
        if (!exception.Data.Contains(AuditMarker.AuditEmailedKey) && !TestHostDetector.IsTestHost)
        {
            var notifications = httpContext.RequestServices.GetRequiredService<IPermitNotificationService>();
            await notifications.SendSystemExceptionAuditAsync("Unhandled exception", context, exception, cancellationToken);
        }
        // Mark the request so the status-code audit middleware doesn't send a second (duplicate) email.
        httpContext.Items[AuditMarker.AuditEmailedKey] = true;

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Title = "An unexpected error occurred.",
                Status = StatusCodes.Status500InternalServerError
            }
        });
    }
}
