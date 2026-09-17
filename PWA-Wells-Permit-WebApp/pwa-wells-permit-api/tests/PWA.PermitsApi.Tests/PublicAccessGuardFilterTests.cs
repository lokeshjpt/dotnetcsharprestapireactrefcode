using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PWA.PermitsApi.Application.Configuration;
using PWA.PermitsApi.Application.Interfaces.Integration;
using PWA.PermitsApi.WebApi.Authorization;

namespace PWA.PermitsApi.Tests;

public sealed class PublicAccessGuardFilterTests
{
    private sealed class FakeCaptchaVerifier : ICaptchaVerifier
    {
        private readonly bool _result;
        public string? LastToken { get; private set; }
        public bool Called { get; private set; }
        public FakeCaptchaVerifier(bool result) => _result = result;

        public Task<bool> VerifyAsync(string? token, string? remoteIp = null, CancellationToken cancellationToken = default)
        {
            Called = true;
            LastToken = token;
            return Task.FromResult(_result);
        }
    }

    private static AuthorizationFilterContext BuildContext(Action<HttpRequest>? configureRequest = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "POST";
        httpContext.Request.Path = "/api/applications";
        configureRequest?.Invoke(httpContext.Request);

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    private static PublicAccessGuardFilter Build(
        ICaptchaVerifier verifier,
        string secret = "enabled-secret",
        string trustedClientIds = "")
    {
        var captcha = Options.Create(new CaptchaOptions { SecretKey = secret });
        var auth = Options.Create(new AuthOptions
        {
            TrustedClientIds = trustedClientIds,
        });
        var proofs = new IPublicAccessProof[]
        {
            new TrustedClientProof(auth),
            new CaptchaProof(verifier),
        };
        return new PublicAccessGuardFilter(captcha, proofs, NullLogger<PublicAccessGuardFilter>.Instance);
    }

    [Fact]
    public async Task FailsOpenWhenCaptchaDisabled()
    {
        var verifier = new FakeCaptchaVerifier(false);
        var filter = Build(verifier, secret: string.Empty);
        var context = BuildContext();

        await filter.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
        Assert.False(verifier.Called);
    }

    [Fact]
    public async Task AllowsValidCaptchaToken()
    {
        var verifier = new FakeCaptchaVerifier(true);
        var filter = Build(verifier);
        var context = BuildContext(r => r.Headers[PublicAccessGuardFilter.CaptchaTokenHeader] = "good-token");

        await filter.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
        Assert.Equal("good-token", verifier.LastToken);
    }

    [Fact]
    public async Task RejectsWhenNoProofPresented()
    {
        var filter = Build(new FakeCaptchaVerifier(false));
        var context = BuildContext();

        await filter.OnAuthorizationAsync(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(PublicAccessGuardFilter.VerificationRequiredCode, problem.Extensions["code"]);
    }

    [Fact]
    public async Task RejectsWhenCaptchaTokenInvalid()
    {
        var filter = Build(new FakeCaptchaVerifier(false));
        var context = BuildContext(r => r.Headers[PublicAccessGuardFilter.CaptchaTokenHeader] = "bad-token");

        await filter.OnAuthorizationAsync(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
    }
}
