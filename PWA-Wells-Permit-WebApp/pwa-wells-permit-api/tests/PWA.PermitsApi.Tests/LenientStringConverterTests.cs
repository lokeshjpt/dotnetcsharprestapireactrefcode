using System.Text.Json;
using System.Text.Json.Serialization;
using PWA.PermitsApi.Application.DTOs;
using PWA.PermitsApi.WebApi.Json;

namespace PWA.PermitsApi.Tests;

public class LenientStringConverterTests
{
    private static JsonSerializerOptions Options()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
        };
        options.Converters.Add(new LenientStringConverter());
        return options;
    }

    [Fact]
    public void Deserializes_numeric_appId_as_string()
    {
        // Mirrors the exact failure class: a client sending appId as a JSON number used to yield
        // "The JSON value could not be converted to System.String" (HTTP 400).
        const string json = "{\"appId\":1785991088186,\"customerId\":\"12345678\",\"paymentType\":\"CC\",\"authorizedAmount\":660.00}";

        var request = JsonSerializer.Deserialize<PreAuthRequest>(json, Options());

        Assert.NotNull(request);
        Assert.Equal("1785991088186", request!.AppId);
        Assert.Equal("12345678", request.CustomerId);
        Assert.Equal(660.00m, request.AuthorizedAmount);
    }

    [Fact]
    public void Deserializes_string_appId_unchanged()
    {
        const string json = "{\"appId\":\"1785991088186\",\"customerId\":\"12345678\",\"paymentType\":\"CC\",\"authorizedAmount\":445}";

        var request = JsonSerializer.Deserialize<PreAuthRequest>(json, Options());

        Assert.NotNull(request);
        Assert.Equal("1785991088186", request!.AppId);
        Assert.Equal(445m, request.AuthorizedAmount);
    }

    [Fact]
    public void Reads_amount_from_string()
    {
        const string json = "{\"appId\":\"1785991088186\",\"customerId\":\"12345678\",\"paymentType\":\"CC\",\"authorizedAmount\":\"445.50\"}";

        var request = JsonSerializer.Deserialize<PreAuthRequest>(json, Options());

        Assert.NotNull(request);
        Assert.Equal(445.50m, request!.AuthorizedAmount);
    }

    [Fact]
    public void Round_trips_string_values_on_write()
    {
        var request = new PreAuthRequest { AppId = "1785991088186", CustomerId = "12345678", AuthorizedAmount = 660m };

        var json = JsonSerializer.Serialize(request, Options());

        Assert.Contains("\"appId\":\"1785991088186\"", json);
        Assert.Contains("\"customerId\":\"12345678\"", json);
    }
}
