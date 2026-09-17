using Microsoft.AspNetCore.Http;
using PWA.PermitsApi.WebApi.Http;
using System.Net;

namespace PWA.PermitsApi.Tests;

public class ClientIpResolverTests
{
    [Fact]
    public void Resolve_IPv6Loopback_NormalizesTo127001()
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = IPAddress.IPv6Loopback; // ::1

        Assert.Equal("127.0.0.1", ClientIpResolver.Resolve(ctx));
    }

    [Fact]
    public void Resolve_IPv4MappedIPv6_UnwrapsToIPv4()
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7").MapToIPv6(); // ::ffff:203.0.113.7

        Assert.Equal("203.0.113.7", ClientIpResolver.Resolve(ctx));
    }

    [Fact]
    public void Resolve_PrefersForwardedForOrigin_ShownViaPeer()
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.5");
        ctx.Request.Headers["X-Forwarded-For"] = "198.51.100.42, 10.0.0.5";

        Assert.Equal("198.51.100.42 (via 10.0.0.5)", ClientIpResolver.Resolve(ctx));
    }

    [Fact]
    public void Resolve_NoRemoteNoHeader_ReturnsNull()
    {
        var ctx = new DefaultHttpContext();

        Assert.Null(ClientIpResolver.Resolve(ctx));
    }
}
