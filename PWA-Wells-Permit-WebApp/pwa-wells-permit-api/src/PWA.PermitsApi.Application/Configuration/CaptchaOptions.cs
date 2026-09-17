namespace PWA.PermitsApi.Application.Configuration;

/// <summary>
/// Server-side Google reCAPTCHA verification settings for the public (ecomm) endpoints. The public
/// SPA obtains a token from the invisible reCAPTCHA widget and forwards it in the <c>X-Captcha-Token</c>
/// header; the API verifies that token against reCAPTCHA before allowing the protected action.
/// </summary>
/// <remarks>
/// When <see cref="SecretKey"/> is blank the whole public-endpoint guard is disabled (fail-open), so a
/// freshly-provisioned environment is never bricked before its captcha secret is set. Local/dev may use
/// Google's universal test secret <c>6LeIxAcTAAAAAGG-vFI1TnRWxMZNFuojJ4WifJWe</c>, which pairs with the
/// universal test sitekey and always verifies true.
/// </remarks>
public sealed class CaptchaOptions
{
    /// <summary>reCAPTCHA secret key. Blank disables the guard (fail-open).</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>reCAPTCHA siteverify endpoint. Overridable for testing/self-hosting.</summary>
    public string VerifyUrl { get; set; } = "https://www.google.com/recaptcha/api/siteverify";

    /// <summary>True only when a secret is configured (i.e. the guard is active).</summary>
    public bool IsEnabled => !string.IsNullOrWhiteSpace(SecretKey);
}
