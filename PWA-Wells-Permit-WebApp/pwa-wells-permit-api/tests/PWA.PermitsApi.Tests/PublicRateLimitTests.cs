using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PWA.PermitsApi.WebApi.Authorization;
using PWA.PermitsApi.WebApi.RateLimiting;

namespace PWA.PermitsApi.Tests;

public sealed class PublicRateLimitTests
{
    private static Endpoint EndpointWith(params object[] metadata) =>
        new(_ => Task.CompletedTask, new EndpointMetadataCollection(metadata), "test");

    [Fact]
    public void DoesNotApplyWhenNoEndpointMatched()
    {
        Assert.False(PublicRateLimit.AppliesTo(null));
    }

    [Fact]
    public void AppliesToAnonymousUnguardedEndpoint()
    {
        var endpoint = EndpointWith();

        Assert.True(PublicRateLimit.AppliesTo(endpoint));
    }

    [Fact]
    public void DoesNotApplyToAuthorizedEndpoint()
    {
        var endpoint = EndpointWith(new AuthorizeAttribute());

        Assert.False(PublicRateLimit.AppliesTo(endpoint));
    }

    [Fact]
    public void AppliesToAuthorizedEndpointThatAllowsAnonymous()
    {
        // Mirrors InspectionController: class-level [Authorize] with an [AllowAnonymous] action.
        var endpoint = EndpointWith(new AuthorizeAttribute(), new AllowAnonymousAttribute());

        Assert.True(PublicRateLimit.AppliesTo(endpoint));
    }

    [Fact]
    public void DoesNotApplyToCaptchaGuardedEndpoint()
    {
        var endpoint = EndpointWith(new ServiceFilterAttribute(typeof(PublicAccessGuardFilter)));

        Assert.False(PublicRateLimit.AppliesTo(endpoint));
    }

    [Fact]
    public void AppliesToEndpointWithUnrelatedServiceFilter()
    {
        var endpoint = EndpointWith(new ServiceFilterAttribute(typeof(PublicRateLimitTests)));

        Assert.True(PublicRateLimit.AppliesTo(endpoint));
    }
}
