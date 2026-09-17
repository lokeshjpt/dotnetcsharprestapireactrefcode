using System.Text;
using Microsoft.Extensions.Options;
using PWA.PermitsApi.Application.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PWA.PermitsApi.Application.Services;

namespace PWA.PermitsApi.Tests;

public sealed class FileUploadServiceTests
{
    [Fact]
    public async Task UploadDocumentAsync_ScansDeliversAndRecordsDocumentLink()
    {
        var scanner = new FakeVirusScanner { Clean = true };
        var ftp = new FakeFileTransferService();
        var repository = new FakeApplicationRepository();
        var service = new FileUploadService(scanner, ftp, repository, new FakePermitNotificationService(), Options.Create(new FtpOptions()), NullLogger<FileUploadService>.Instance);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.4 test"));
        var result = await service.UploadDocumentAsync("1700000000001", "sitemap.pdf", "application/pdf", "SITEMAP", "Site plan", stream, "public-portal");

        Assert.Equal(1, result.SeqNum);
        Assert.Equal("SITEMAP", result.DocumentType);
        Assert.Contains("sitemap.pdf", result.FileName);
        Assert.Equal(1, ftp.UploadCalls);
        Assert.Single(repository.DocumentLinks);
        Assert.Equal("public-portal", repository.DocumentLinks[0].AddedBy);
        Assert.Equal("Site plan", repository.DocumentLinks[0].Link.OtherTypeDesc);
    }

    [Fact]
    public async Task UploadDocumentAsync_RejectsInfectedFile()
    {
        var scanner = new FakeVirusScanner { Clean = false };
        var ftp = new FakeFileTransferService();
        var repository = new FakeApplicationRepository();
        var service = new FileUploadService(scanner, ftp, repository, new FakePermitNotificationService(), Options.Create(new FtpOptions()), NullLogger<FileUploadService>.Instance);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("bad"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UploadDocumentAsync("1700000000001", "sitemap.pdf", "application/pdf", "SITEMAP", null, stream, "public-portal"));

        Assert.Empty(repository.DocumentLinks);
    }

    [Fact]
    public async Task UploadDocumentAsync_StillRecordsLinkWhenFtpFails()
    {
        var scanner = new FakeVirusScanner { Clean = true };
        var ftp = new FakeFileTransferService { ThrowOnUpload = true };
        var repository = new FakeApplicationRepository();
        var service = new FileUploadService(scanner, ftp, repository, new FakePermitNotificationService(), Options.Create(new FtpOptions()), NullLogger<FileUploadService>.Instance);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.4 test"));
        var result = await service.UploadDocumentAsync("1700000000001", "sitemap.pdf", "application/pdf", "SITEMAP", null, stream, "public-portal");

        Assert.Equal(1, result.SeqNum);
        Assert.Single(repository.DocumentLinks); // FTP failure is best-effort, link still recorded
    }

    [Fact]
    public async Task UploadSitemapAsync_ScansDeliversAndRecordsSitemapOnApplication()
    {
        var scanner = new FakeVirusScanner { Clean = true };
        var ftp = new FakeFileTransferService();
        var repository = new FakeApplicationRepository();
        var service = new FileUploadService(scanner, ftp, repository, new FakePermitNotificationService(), Options.Create(new FtpOptions()), NullLogger<FileUploadService>.Instance);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.4 test"));
        var result = await service.UploadSitemapAsync("1700000000001", "sitemap.pdf", "application/pdf", stream, "public-portal");

        Assert.True(result.VirusScanPassed);
        Assert.Contains("sitemap.pdf", result.FileName);
        Assert.Equal(1, ftp.UploadCalls);
        Assert.Empty(repository.DocumentLinks); // sitemaps do NOT create APP_DOCUMENT_LINKS rows
        Assert.Single(repository.Sitemaps);
        Assert.Equal("1700000000001", repository.Sitemaps[0].AppId);
        Assert.Equal("public-portal", repository.Sitemaps[0].UpdatedBy);
        Assert.Contains("sitemap.pdf", repository.Sitemaps[0].FileName);
    }

    [Fact]
    public async Task UploadSitemapAsync_RejectsInfectedFile()
    {
        var scanner = new FakeVirusScanner { Clean = false };
        var ftp = new FakeFileTransferService();
        var repository = new FakeApplicationRepository();
        var service = new FileUploadService(scanner, ftp, repository, new FakePermitNotificationService(), Options.Create(new FtpOptions()), NullLogger<FileUploadService>.Instance);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("bad"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UploadSitemapAsync("1700000000001", "sitemap.pdf", "application/pdf", stream, "public-portal"));

        Assert.Empty(repository.Sitemaps);
    }

    [Fact]
    public async Task UploadSitemapAsync_StillRecordsSitemapWhenFtpFails()
    {
        var scanner = new FakeVirusScanner { Clean = true };
        var ftp = new FakeFileTransferService { ThrowOnUpload = true };
        var repository = new FakeApplicationRepository();
        var service = new FileUploadService(scanner, ftp, repository, new FakePermitNotificationService(), Options.Create(new FtpOptions()), NullLogger<FileUploadService>.Instance);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.4 test"));
        var result = await service.UploadSitemapAsync("1700000000001", "sitemap.pdf", "application/pdf", stream, "public-portal");

        Assert.True(result.VirusScanPassed);
        Assert.Single(repository.Sitemaps); // FTP failure is best-effort, sitemap still recorded
    }

    [Fact]
    public async Task UploadSitemapAsync_FailsClosedWhenScannerUnreachable()
    {
        // A virus-scan transport failure must NOT store or deliver the file (fail closed). The
        // exception propagates so the controller can translate it into a clean 502.
        var scanner = new FakeVirusScanner { ThrowOnScan = true };
        var ftp = new FakeFileTransferService();
        var repository = new FakeApplicationRepository();
        var service = new FileUploadService(scanner, ftp, repository, new FakePermitNotificationService(), Options.Create(new FtpOptions()), NullLogger<FileUploadService>.Instance);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.4 test"));

        await Assert.ThrowsAsync<System.Net.Http.HttpRequestException>(() =>
            service.UploadSitemapAsync("1700000000001", "sitemap.pdf", "application/pdf", stream, "public-portal"));

        Assert.Equal(0, ftp.UploadCalls);
        Assert.Empty(repository.Sitemaps);
    }
}
