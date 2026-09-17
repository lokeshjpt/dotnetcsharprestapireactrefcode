using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using PWA.PermitsApi.Application.Configuration;

namespace PWA.PermitsApi.WebApi.Authorization;

/// <summary>
/// Authorization filter that guards the anonymous <b>public ecomm</b> endpoints (application submit,
/// site-map/document upload, and the Track search) against scripted/bot abuse. A request is allowed
/// when it satisfies <b>any one</b> of the registered <see cref="IPublicAccessProof"/> strategies;
/// otherwise it is rejected with <c>401</c>.
/// </summary>
/// <remarks>
/// <para>
/// This class is deliberately a thin <b>orchestrator</b>: it owns only the OR-composition and the
/// fail-open switch. Each accepted credential lives in its own proof so the schemes are not mixed
/// together here and can be tested in isolation:
/// </para>
/// <list type="bullet">
/// <item><see cref="TrustedClientProof"/> — validated, whitelisted Entra (Azure AD) bearer client
/// (e.g. the authenticated intra SPA on an endpoint it shares with ecomm).</item>
/// <item><see cref="CaptchaProof"/> — reCAPTCHA token from the browser SPA.</item>
/// </list>
/// <para>
/// Proofs are evaluated in DI-registration order (cheapest-first) and short-circuit on the first
/// success. When no reCAPTCHA secret is configured (<see cref="CaptchaOptions.IsEnabled"/> is false)
/// the whole guard is fail-open, so a freshly-provisioned environment is never bricked before its
/// secret is set.
/// </para>
/// </remarks>
public sealed class PublicAccessGuardFilter : IAsyncAuthorizationFilter
{
    /// <summary>Header carrying the reCAPTCHA response token issued to the browser SPA.</summary>
    public const string CaptchaTokenHeader = CaptchaProof.HeaderName;

    /// <summary>Extension key on the 401 ProblemDetails identifying a guard rejection.</summary>
    public const string VerificationRequiredCode = "verification_required";

    private readonly CaptchaOptions _captcha;
    private readonly IReadOnlyList<IPublicAccessProof> _proofs;
    private readonly ILogger<PublicAccessGuardFilter> _logger;

    public PublicAccessGuardFilter(
        IOptions<CaptchaOptions> captcha,
        IEnumerable<IPublicAccessProof> proofs,
        ILogger<PublicAccessGuardFilter> logger)
    {
        _captcha = captcha.Value;
        _proofs = proofs as IReadOnlyList<IPublicAccessProof> ?? proofs.ToList();
        _logger = logger;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // Fail-open when the captcha guard is not configured — never brick an environment.
        if (!_captcha.IsEnabled)
        {
            return;
        }

        var httpContext = context.HttpContext;

        // Allow as soon as any single proof is satisfied (OR composition, cheapest-first).
        foreach (var proof in _proofs)
        {
            if (await proof.IsSatisfiedAsync(httpContext))
            {
                return;
            }
        }

        // No accepted proof — reject.
        _logger.LogWarning(
            "Public-endpoint verification failed for {Method} {Path} from {Ip}.",
            httpContext.Request.Method,
            httpContext.Request.Path,
            httpContext.Connection.RemoteIpAddress);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Verification required",
            Detail = "This request could not be verified. Complete the on-page verification challenge, or sign in with an authorized account.",
        };
        problem.Extensions["code"] = VerificationRequiredCode;

        context.Result = new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status401Unauthorized,
            ContentTypes = { "application/problem+json" },
        };
    }
}
