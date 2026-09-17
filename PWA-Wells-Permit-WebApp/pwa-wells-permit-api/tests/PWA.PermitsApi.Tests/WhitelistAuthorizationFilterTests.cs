using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using PWA.PermitsApi.WebApi.Authorization;
using System.Security.Claims;

namespace PWA.PermitsApi.Tests;

public sealed class WhitelistAuthorizationFilterTests
{
    private sealed class FakeWhitelistProvider : IWhitelistProvider
    {
        private readonly IReadOnlyCollection<string> _allowed;

        public FakeWhitelistProvider(params string[] allowed) =>
            _allowed = allowed.ToHashSet(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyCollection<string>> GetAllowedEmailsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_allowed);
    }

    private static AuthorizationFilterContext BuildContext(
        ClaimsPrincipal user,
        params object[] endpointMetadata)
    {
        var httpContext = new DefaultHttpContext { User = user };
        httpContext.Request.Method = "GET";
        httpContext.Request.Path = "/api/applications/123";
        var endpoint = new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(endpointMetadata),
            "test-endpoint");
        httpContext.SetEndpoint(endpoint);

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    private static ClaimsPrincipal AuthenticatedUser(string email) =>
        new(new ClaimsIdentity(new[] { new Claim("preferred_username", email) }, "test"));

    private static (WhitelistAuthorizationFilter Filter, FakePermitNotificationService Notifier) BuildFilter(params string[] whitelist)
    {
        var notifier = new FakePermitNotificationService();
        var filter = new WhitelistAuthorizationFilter(
            new FakeWhitelistProvider(whitelist),
            notifier,
            NullLogger<WhitelistAuthorizationFilter>.Instance);
        return (filter, notifier);
    }

    [Fact]
    public async Task AllowsWhitelistedUserOnProtectedEndpoint()
    {
        var (filter, notifier) = BuildFilter("ok@x.gov");
        var context = BuildContext(AuthenticatedUser("ok@x.gov"), new AuthorizeAttribute());

        await filter.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
        Assert.Empty(notifier.Sent);
    }

    [Fact]
    public async Task AllowsWhitelistedUserCaseInsensitively()
    {
        var (filter, notifier) = BuildFilter("ok@x.gov");
        var context = BuildContext(AuthenticatedUser("OK@X.GOV"), new AuthorizeAttribute());

        await filter.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
        Assert.Empty(notifier.Sent);
    }

    [Fact]
    public async Task BlocksNonWhitelistedUserWith403AndAudits()
    {
        var (filter, notifier) = BuildFilter("ok@x.gov");
        var context = BuildContext(AuthenticatedUser("intruder@x.gov"), new AuthorizeAttribute());

        await filter.OnAuthorizationAsync(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(WhitelistAuthorizationFilter.NotAuthorizedCode, problem.Extensions["code"]);
        Assert.Single(notifier.Sent, s => s.Scenario == "unauthorizedAccessAudit");
        Assert.True(context.HttpContext.Items.ContainsKey(
            PWA.PermitsApi.Application.Notifications.AuditMarker.AuditEmailedKey));
    }

    [Fact]
    public async Task SkipsAnonymousEndpoint()
    {
        var (filter, notifier) = BuildFilter("ok@x.gov");
        var context = BuildContext(
            AuthenticatedUser("intruder@x.gov"),
            new AuthorizeAttribute(),
            new AllowAnonymousAttribute());

        await filter.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
        Assert.Empty(notifier.Sent);
    }

    [Fact]
    public async Task SkipsEndpointWithoutAuthorizeMetadata()
    {
        var (filter, notifier) = BuildFilter("ok@x.gov");
        var context = BuildContext(AuthenticatedUser("intruder@x.gov"));

        await filter.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
        Assert.Empty(notifier.Sent);
    }

    [Fact]
    public async Task SkipsWhenAllowlistEmpty()
    {
        var (filter, notifier) = BuildFilter();
        var context = BuildContext(AuthenticatedUser("anyone@x.gov"), new AuthorizeAttribute());

        await filter.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
        Assert.Empty(notifier.Sent);
    }

    [Fact]
    public async Task SkipsUnauthenticatedUser()
    {
        var (filter, notifier) = BuildFilter("ok@x.gov");
        var context = BuildContext(new ClaimsPrincipal(new ClaimsIdentity()), new AuthorizeAttribute());

        await filter.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
        Assert.Empty(notifier.Sent);
    }
}
