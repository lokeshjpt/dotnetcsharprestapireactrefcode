using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PWA.PermitsApi.Application.Configuration;
using PWA.PermitsApi.Infrastructure.Services;

namespace PWA.PermitsApi.Tests;

public sealed class GoogleReCaptchaVerifierTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage? _response;
        private readonly Exception? _exception;

        public string? LastBody { get; private set; }

        public StubHandler(HttpResponseMessage response) => _response = response;
        public StubHandler(Exception exception) => _exception = exception;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is not null)
            {
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }
            if (_exception is not null)
            {
                throw _exception;
            }
            return _response!;
        }
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public StubHttpClientFactory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler);
    }

    private static GoogleReCaptchaVerifier Build(HttpMessageHandler handler, string secret = "test-secret") =>
        new(
            Options.Create(new CaptchaOptions { SecretKey = secret }),
            new StubHttpClientFactory(handler),
            NullLogger<GoogleReCaptchaVerifier>.Instance);

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task ReturnsTrueOnProviderSuccess()
    {
        var handler = new StubHandler(Json(HttpStatusCode.OK, "{\"success\":true}"));
        var verifier = Build(handler);

        Assert.True(await verifier.VerifyAsync("tok", "1.2.3.4"));
        Assert.Contains("secret=test-secret", handler.LastBody);
        Assert.Contains("response=tok", handler.LastBody);
        Assert.Contains("remoteip=1.2.3.4", handler.LastBody);
    }

    [Fact]
    public async Task ReturnsFalseOnProviderFailure()
    {
        var verifier = Build(new StubHandler(Json(HttpStatusCode.OK, "{\"success\":false,\"error-codes\":[\"invalid-input-response\"]}")));

        Assert.False(await verifier.VerifyAsync("tok"));
    }

    [Fact]
    public async Task ReturnsFalseOnNonSuccessStatus()
    {
        var verifier = Build(new StubHandler(Json(HttpStatusCode.InternalServerError, "{}")));

        Assert.False(await verifier.VerifyAsync("tok"));
    }

    [Fact]
    public async Task ReturnsFalseOnTransportException()
    {
        var verifier = Build(new StubHandler(new HttpRequestException("boom")));

        Assert.False(await verifier.VerifyAsync("tok"));
    }

    [Fact]
    public async Task ReturnsFalseForBlankTokenWithoutCallingProvider()
    {
        // A throwing factory proves no HTTP call is made for an empty token.
        var verifier = new GoogleReCaptchaVerifier(
            Options.Create(new CaptchaOptions { SecretKey = "test-secret" }),
            new ThrowingFactory(),
            NullLogger<GoogleReCaptchaVerifier>.Instance);

        Assert.False(await verifier.VerifyAsync("   "));
        Assert.False(await verifier.VerifyAsync(null));
    }

    [Fact]
    public async Task ReturnsFalseWhenSecretMissing()
    {
        var verifier = Build(new StubHandler(Json(HttpStatusCode.OK, "{\"success\":true}")), secret: string.Empty);

        Assert.False(await verifier.VerifyAsync("tok"));
    }

    private sealed class ThrowingFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            throw new InvalidOperationException("No HTTP call expected.");
    }
}
