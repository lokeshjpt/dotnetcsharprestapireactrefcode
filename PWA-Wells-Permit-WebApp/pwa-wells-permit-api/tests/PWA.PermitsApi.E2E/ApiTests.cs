using System.Text.Json;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace PWA.PermitsApi.E2E;

/// <summary>
/// API-level end-to-end tests that exercise the real .NET REST API over HTTP.
/// These require no browser, so they run even when the Playwright browser
/// binaries have not been installed.
/// </summary>
[TestFixture]
public class ApiTests : PlaywrightTest
{
    private IAPIRequestContext _request = null!;

    [SetUp]
    public async Task SetUpApiContext()
    {
        _request = await Playwright.APIRequest.NewContextAsync(new APIRequestNewContextOptions
        {
            BaseURL = TestConfig.ApiRoot,
            IgnoreHTTPSErrors = true,
        });
    }

    [TearDown]
    public async Task DisposeApiContext()
    {
        if (_request is not null)
        {
            await _request.DisposeAsync();
        }
    }

    [TestCase("states")]
    [TestCase("cities")]
    [TestCase("payment-types")]
    [TestCase("work-categories")]
    [TestCase("work-types")]
    [TestCase("well-use-types")]
    [TestCase("drill-methods")]
    [TestCase("inspectors")]
    public async Task ReferenceEndpoint_ReturnsOkAndNonEmptyJson(string resource)
    {
        var response = await _request.GetAsync($"/api/ref/{resource}");

        Assert.That(response.Ok, Is.True,
            $"GET /api/ref/{resource} returned {response.Status} {response.StatusText}");

        var json = await response.JsonAsync();
        Assert.That(json.HasValue, Is.True, $"/api/ref/{resource} returned no JSON body");

        var root = json!.Value;
        Assert.That(root.ValueKind, Is.EqualTo(JsonValueKind.Array),
            $"/api/ref/{resource} should return a JSON array");
        Assert.That(root.GetArrayLength(), Is.GreaterThan(0),
            $"/api/ref/{resource} returned an empty array");
    }

    [Test]
    public async Task SubmitExemptApplication_PersistsAndEchoesHazardSubForm()
    {
        var payload = SampleApplication.BuildExemptWithHazard();

        // POST /api/applications -> 201 Created with the full application graph.
        var createResponse = await _request.PostAsync("/api/applications", new APIRequestContextOptions
        {
            DataObject = payload,
        });

        Assert.That(createResponse.Status, Is.EqualTo(201),
            $"POST /api/applications returned {createResponse.Status}: {await createResponse.TextAsync()}");

        var created = (await createResponse.JsonAsync())!.Value;
        AssertApplicationEchoesHazard(created);

        var appId = created.GetProperty("appId").GetString();
        Assert.That(appId, Is.Not.Null.And.Not.Empty, "created application is missing an appId");

        // GET /api/applications/{appId} -> readback echoes the same data.
        var getResponse = await _request.GetAsync($"/api/applications/{appId}");
        Assert.That(getResponse.Ok, Is.True,
            $"GET /api/applications/{appId} returned {getResponse.Status}");

        var readback = (await getResponse.JsonAsync())!.Value;
        Assert.That(readback.GetProperty("appId").GetString(), Is.EqualTo(appId));
        AssertApplicationEchoesHazard(readback);
    }

    private static void AssertApplicationEchoesHazard(JsonElement application)
    {
        Assert.That(application.GetProperty("siteHazardRequired").GetString(), Is.EqualTo("Y"));

        Assert.That(application.TryGetProperty("hazard", out var hazard), Is.True,
            "application response is missing the hazard sub-form");
        Assert.That(hazard.ValueKind, Is.EqualTo(JsonValueKind.Object),
            "hazard sub-form should be a JSON object");

        Assert.Multiple(() =>
        {
            Assert.That(hazard.GetProperty("ppeLevelA").GetString(), Is.EqualTo("Y"));
            Assert.That(hazard.GetProperty("equipHardHatFlag").GetString(), Is.EqualTo("R"));
            Assert.That(hazard.GetProperty("equipClothingFlag").GetString(), Is.EqualTo("R"));
            Assert.That(hazard.GetProperty("equipClothingDesc").GetString(), Is.EqualTo(SampleApplication.ClothingDesc));
            Assert.That(hazard.GetProperty("infoProvidedByLastName").GetString(), Is.EqualTo(SampleApplication.InfoProvidedByLastName));
            Assert.That(hazard.GetProperty("acknowledgement").GetString(), Is.EqualTo("Y"));
        });
    }
}
