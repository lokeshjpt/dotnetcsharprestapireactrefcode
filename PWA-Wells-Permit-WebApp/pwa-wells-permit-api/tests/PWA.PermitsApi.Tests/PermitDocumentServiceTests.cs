using PWA.PermitsApi.Application.DTOs;
using PWA.PermitsApi.Application.Notifications;
using PWA.PermitsApi.Infrastructure.Services;
using PWA.PermitsApi.Domain.Models;
using DomainApplication = PWA.PermitsApi.Domain.Models.Application;

namespace PWA.PermitsApi.Tests;

public sealed class PermitDocumentServiceTests
{
    private static DomainApplication SampleApp() => new()
    {
        AppId = "1234567890123",
        StatusCode = "APPRV",
        AddDate = new DateTime(2026, 1, 5),
        SiteCityName = "Oakland",
        SiteLocation = "123 Main St",
        ProjStartDate = new DateTime(2026, 2, 1),
        ProjEndDate = new DateTime(2026, 3, 1),
        SitemapFilename = "map.pdf",
        Applicant = new Applicant
        {
            AppFirstName = "Tina", AppLastName = "Lopez", AppEmailAddr = "tina@example.com",
            AppAddrStreet = "1 A St", AppAddrCity = "Oakland", AppAddrState = "CA", AppAddrZip = "94601", AppPhone = "510-555-1000"
        },
        Owner = new PartyInfo { FirstName = "Owen", LastName = "Ownerly", AddrStreet = "2 B St", AddrCity = "Hayward", AddrState = "CA", AddrZip = "94544", Phone = "510-555-2000" },
        Client = new PartyInfo(),
        Contact = new ContactInfo { ContactFirstName = "Cara", ContactLastName = "Contact", ContactEmail = "cara@example.com", ContactPhone = "510-555-3000", ContactCell = "510-555-4000" },
        Works = new List<ApplicationWork>
        {
            new()
            {
                WorkId = 1, WorkCategory = "con", WorkType = "conwell", WellUseType = "domestic",
                DrillerName = "Ace Drilling", DrillerLicenseNum = "L123", DrillMethodType = "rotary",
                WorkFeeRate = 250m, WorkFeeUnit = "EA", StatusCode = "APPRV",
                Specs = new List<ApplicationWorkSpec>
                {
                    new() { WorkSpecsId = 10, WorkId = 1, OwnerWellNum = "W-1", DrillCount = 1, HoleDiamIn = 8m, CasingDiamIn = 6m, SealDepthFt = 50m, MaxDepthFt = 300m, StateWellId = "SW-1", StatusCode = "APPRV" }
                }
            }
        }
    };

    [Fact]
    public void GeneratePermitPdf_ProducesValidPdfBytes()
    {
        var service = new PermitDocumentService();
        var permitInfo = new PermitInfoDto("1234567890123", "staff", new DateTime(2026, 1, 10),
            new[] { new PermitLineDto("W2026-0001", 1, 10, new DateTime(2026, 1, 10), new DateTime(2027, 1, 10), "APPRV") });
        var payment = new AppPayment
        {
            AppId = "1234567890123", PaymentType = "CC", ReceiptNum = "WR2026-0042",
            AuthAmount = 660m, PaidAmount = 660m, ServiceCharge = 10m, StatusCode = "PAID", AcctName = "Tina Lopez"
        };
        var model = new PermitDocumentModel(SampleApp(), permitInfo, payment, new[] { "Comply with county drilling standards." }, Approved: true);

        var pdf = service.GeneratePermitPdf(model);

        Assert.NotNull(pdf);
        Assert.True(pdf.Length > 1000, "PDF should have meaningful content.");
        // PDF magic number "%PDF".
        Assert.Equal((byte)'%', pdf[0]);
        Assert.Equal((byte)'P', pdf[1]);
        Assert.Equal((byte)'D', pdf[2]);
        Assert.Equal((byte)'F', pdf[3]);
    }

    [Fact]
    public void GeneratePermitPdf_PreviewWhenNotApproved_StillRenders()
    {
        var service = new PermitDocumentService();
        var model = new PermitDocumentModel(SampleApp(), PermitInfo: null, Payment: null, Conditions: Array.Empty<string>(), Approved: false);

        var pdf = service.GeneratePermitPdf(model);

        Assert.True(pdf.Length > 1000);
        Assert.Equal((byte)'%', pdf[0]);
    }
}
