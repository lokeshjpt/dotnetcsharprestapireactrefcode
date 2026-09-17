using Microsoft.AspNetCore.Http;
using PWA.PermitsApi.Application.Interfaces.Integration;

namespace PWA.PermitsApi.WebApi.Authorization;

/// <summary>
/// Public-access proof for a <b>human browser</b>: a Google reCAPTCHA response token presented in the
/// <see cref="HeaderName"/> header and verified server-side via <see cref="ICaptchaVerifier"/>. This is
/// how the public ecomm SPA passes the guard. The proof is unsatisfied when the header is absent/blank
/// or the verifier rejects (or times out on) the token.
/// </summary>
public sealed class CaptchaProof : IPublicAccessProof
{
    /// <summary>Header carrying the reCAPTCHA response token issued to the browser SPA.</summary>
    public const string HeaderName = "X-Captcha-Token";

    private readonly ICaptchaVerifier _verifier;

    public CaptchaProof(ICaptchaVerifier verifier) => _verifier = verifier;

    public async Task<bool> IsSatisfiedAsync(HttpContext context)
    {
        var token = context.Request.Headers.TryGetValue(HeaderName, out var value)
            ? value.ToString()
            : null;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var remoteIp = context.Connection.RemoteIpAddress?.ToString();
        return await _verifier.VerifyAsync(token, remoteIp, context.RequestAborted);
    }
}
