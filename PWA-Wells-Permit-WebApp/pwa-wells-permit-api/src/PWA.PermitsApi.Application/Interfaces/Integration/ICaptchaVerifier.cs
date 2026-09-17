namespace PWA.PermitsApi.Application.Interfaces.Integration;

/// <summary>
/// Verifies a captcha response token issued to the public SPA against the captcha provider (Google reCAPTCHA).
/// </summary>
public interface ICaptchaVerifier
{
    /// <summary>
    /// Returns true when <paramref name="token"/> is a valid, unused captcha response. A blank token,
    /// a provider "failure" verdict, or any transport/parse error returns false (deny).
    /// </summary>
    /// <param name="token">The captcha response token from the client (<c>X-Captcha-Token</c> header).</param>
    /// <param name="remoteIp">Optional caller IP forwarded to the provider for additional scoring.</param>
    Task<bool> VerifyAsync(string? token, string? remoteIp = null, CancellationToken cancellationToken = default);
}
