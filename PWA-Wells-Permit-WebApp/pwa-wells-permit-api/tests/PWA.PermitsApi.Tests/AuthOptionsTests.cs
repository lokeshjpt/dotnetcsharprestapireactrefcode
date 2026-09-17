using PWA.PermitsApi.Application.Configuration;

namespace PWA.PermitsApi.Tests;

public sealed class AuthOptionsTests
{
    [Fact]
    public void TrustedClientIds_ParsesCommaAndSemicolonSeparatedList()
    {
        var options = new AuthOptions { TrustedClientIds = " id-1 , id-2 ; id-3 " };

        Assert.True(options.HasTrustedClients);
        Assert.True(options.IsTrustedClient("id-1"));
        Assert.True(options.IsTrustedClient("id-2"));
        Assert.True(options.IsTrustedClient("ID-3"));
        Assert.False(options.IsTrustedClient("id-4"));
    }

    [Fact]
    public void TrustedClientIds_BlankMeansNoAllowlist()
    {
        var options = new AuthOptions();

        Assert.False(options.HasTrustedClients);
        Assert.False(options.IsTrustedClient("anything"));
        Assert.False(options.IsTrustedClient(null));
    }
}
