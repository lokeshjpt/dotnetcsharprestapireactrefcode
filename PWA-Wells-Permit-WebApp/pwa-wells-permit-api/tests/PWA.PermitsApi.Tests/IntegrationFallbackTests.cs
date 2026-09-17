using System.Net.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PWA.PermitsApi.Application.Configuration;
using PWA.PermitsApi.Infrastructure.Services;

namespace PWA.PermitsApi.Tests;

public sealed class IntegrationFallbackTests
{
    private sealed class ThrowingHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            throw new InvalidOperationException("No live HTTP call should be made in mock mode.");
    }

    [Fact]
    public async Task IntelliPayGateway_MockMode_ReturnsVaultCustidWithoutHttp()
    {
        var options = Options.Create(new IntelliPayOptions { UseMock = true });
        var gateway = new IntelliPayGateway(options, new ThrowingHttpClientFactory(), NullLogger<IntelliPayGateway>.Instance);

        var result = await gateway.VaultZeroDollarAsync("1700000000001");

        Assert.True(result.Approved);
        Assert.False(string.IsNullOrWhiteSpace(result.CustomerId));
    }

    [Fact]
    public async Task IntelliPayGateway_MockMode_ChargeReturnsApprovedJson()
    {
        var options = Options.Create(new IntelliPayOptions { UseMock = true });
        var gateway = new IntelliPayGateway(options, new ThrowingHttpClientFactory(), NullLogger<IntelliPayGateway>.Instance);

        var response = await gateway.ChargeStoredCustomerAsync("1700000000001", "CUST123", 125m);

        Assert.Contains("\"response\":\"A\"", response);
        Assert.Contains("\"paymentid\":\"MOCK-", response);
        Assert.Contains("\"authcode\":", response);
    }

    [Fact]
    public async Task IcapVirusScanner_MockMode_ReturnsCleanWithoutHttp()
    {
        var options = Options.Create(new IcapOptions { UseMock = true });
        var scanner = new IcapVirusScanner(options, new ThrowingHttpClientFactory(), NullLogger<IcapVirusScanner>.Instance);

        var clean = await scanner.ScanAsync("sitemap.pdf", new byte[] { 1, 2, 3 });

        Assert.True(clean);
    }

    [Fact]
    public async Task EmailService_MockMode_DoesNotThrowAndSendsNoMail()
    {
        var options = Options.Create(new EmailOptions { UseMock = true });
        var routing = Options.Create(new EmailRoutingOptions
        {
            Projects = new List<EmailProjectOptions> { new() { ProjectId = "PWA", DefaultTo = "to@example.com", EmailServer = "smtp.example.com" } }
        });
        var service = new EmailService(options, routing, NullLogger<EmailService>.Instance);

        await service.SendAsync("someone@example.com", "Subject", "Body");
        await service.SendProjectNotificationAsync("PWA", null, "Body");
    }

    [Fact]
    public async Task FtpFileTransferService_MockMode_ReturnsMockPathWithoutTransfer()
    {
        var options = Options.Create(new FtpOptions { UseMock = true, RemoteDirectory = "permits" });
        var service = new FtpFileTransferService(options, new FakePermitNotificationService(), NullLogger<FtpFileTransferService>.Instance);

        var path = await service.UploadAsync("file.pdf", new byte[] { 1, 2, 3 });

        Assert.Contains("local-mock", path);
        Assert.Contains("file.pdf", path);
    }
}
