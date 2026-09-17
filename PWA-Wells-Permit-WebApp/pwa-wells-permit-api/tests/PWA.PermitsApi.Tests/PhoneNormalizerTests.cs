using PWA.PermitsApi.Application.Common;

namespace PWA.PermitsApi.Tests;

public sealed class PhoneNormalizerTests
{
    [Theory]
    [InlineData("999-999-9999", "9999999999")]
    [InlineData("(510) 555-1234", "5105551234")]
    [InlineData("510.555.1234 x22", "510555123422")]
    [InlineData("5105551234", "5105551234")]
    [InlineData("", "")]
    [InlineData("N/A", "")]
    public void DigitsOnly_StripsAllNonDigits(string input, string expected)
    {
        Assert.Equal(expected, PhoneNormalizer.DigitsOnly(input));
    }

    [Fact]
    public void DigitsOnly_PreservesNull()
    {
        Assert.Null(PhoneNormalizer.DigitsOnly(null));
    }
}
