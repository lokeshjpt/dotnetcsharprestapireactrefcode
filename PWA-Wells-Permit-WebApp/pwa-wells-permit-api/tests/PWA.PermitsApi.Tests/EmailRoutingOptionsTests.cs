using Microsoft.Extensions.Configuration;
using PWA.PermitsApi.Application.Configuration;

namespace PWA.PermitsApi.Tests;

public sealed class EmailRoutingOptionsTests
{
    [Fact]
    public void BindsIndexedProjectRoutingFromConfiguration()
    {
        var settings = new Dictionary<string, string?>
        {
            ["EmailRouting:NumberOfProjects"] = "1",
            ["EmailRouting:LogLocation"] = "pwaPermitsEcomm/logs/emailLog",
            ["EmailRouting:LogRotation"] = "Monthly",
            ["EmailRouting:GlobalTraceEnabled"] = "true",
            ["EmailRouting:Projects:0:ProjectId"] = "PWA",
            ["EmailRouting:Projects:0:RecipientName"] = "PWAApp",
            ["EmailRouting:Projects:0:EmailServer"] = "allsmtp.acgov.org",
            ["EmailRouting:Projects:0:DefaultFrom"] = "Janu.Sundaram@alamedacountyca.gov",
            ["EmailRouting:Projects:0:DefaultTo"] = "Janu.Sundaram@alamedacountyca.gov",
            ["EmailRouting:Projects:0:DefaultSubject"] = "Message from PWA Permits Application",
            ["EmailRouting:Projects:0:BackupCopyLocation"] = "pwaPermitsInter/email_archive",
            ["EmailRouting:Projects:0:DebugMail"] = "true"
        };

        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var options = config.GetSection("EmailRouting").Get<EmailRoutingOptions>();

        Assert.NotNull(options);
        Assert.Equal(1, options!.NumberOfProjects);
        Assert.True(options.GlobalTraceEnabled);
        Assert.Single(options.Projects);

        var project = options.FindProject("pwa");
        Assert.NotNull(project);
        Assert.Equal("allsmtp.acgov.org", project!.EmailServer);
        Assert.Equal("Janu.Sundaram@alamedacountyca.gov", project.DefaultFrom);
        Assert.Equal("pwaPermitsInter/email_archive", project.BackupCopyLocation);
        Assert.True(project.DebugMail);
    }

    [Fact]
    public void BindsIntraExtrasFromConfiguration()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Intra:IntelliPayWebapiUrl"] = "https://secure.cpteller.com/api/26/webapi.cfc",
            ["Intra:SsrsServer"] = "ssrspbid.acgov.org/ReportServer",
            ["Intra:MaxFileSizeUpload"] = "104857600",
            ["Intra:FtpServer"] = "xtweb01",
            ["Intra:GenCondDoc"] = "general_cond.pdf"
        };

        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var options = config.GetSection("Intra").Get<IntraOptions>();

        Assert.NotNull(options);
        Assert.Equal("https://secure.cpteller.com/api/26/webapi.cfc", options!.IntelliPayWebapiUrl);
        Assert.Equal(104857600L, options.MaxFileSizeUpload);
        Assert.Equal("xtweb01", options.FtpServer);
        Assert.Equal("general_cond.pdf", options.GenCondDoc);
    }
}
