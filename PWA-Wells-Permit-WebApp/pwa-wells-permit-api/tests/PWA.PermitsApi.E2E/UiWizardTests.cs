using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace PWA.PermitsApi.E2E;

/// <summary>
/// UI-level end-to-end tests that drive the real React ecomm wizard in a
/// headless Chromium browser. These require the Playwright browser binaries
/// (playwright.ps1 install chromium).
/// </summary>
[TestFixture]
public class UiWizardTests : PageTest
{
    public override BrowserNewContextOptions ContextOptions() => new()
    {
        BaseURL = TestConfig.UiBaseUrl,
        IgnoreHTTPSErrors = true,
    };

    [Test]
    public async Task ApplyPage_RendersLandmarksAndFirstStep()
    {
        await Page.GotoAsync("/apply", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        // Page heading.
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Level = 1 }))
            .ToContainTextAsync(new Regex("Well Permit Application", RegexOptions.IgnoreCase));

        // Progress nav landmark.
        await Expect(Page.GetByRole(AriaRole.Navigation, new() { Name = "Application progress" }))
            .ToBeVisibleAsync();

        // Accessibility landmarks: skip link + main content region.
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Skip to main content" }))
            .ToBeAttachedAsync();
        await Expect(Page.Locator("main#main-content")).ToBeAttachedAsync();

        // First step renders (Applicant Information section + first field).
        await Expect(Page.GetByText("Applicant Information")).ToBeVisibleAsync();
        await Expect(Page.GetByLabel("Applicant Business Name")).ToBeVisibleAsync();
    }

    [Test]
    public async Task ApplyPage_FillStep1_AdvancesToProjectStep()
    {
        await Page.GotoAsync("/apply", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        await Page.GetByLabel("Applicant Business Name").FillAsync("Bayside Drilling Co.");
        await Page.GetByLabel(new Regex("^Last Name")).FillAsync("Nguyen");
        await Page.GetByLabel(new Regex("^First Name")).FillAsync("Ana");
        await Page.GetByLabel("Mailing Address").FillAsync("1401 Lakeside Dr");
        await Page.GetByLabel(new Regex("^City")).FillAsync("Oakland");
        await Page.GetByLabel("Zip Code").FillAsync("94612");
        await Page.GetByLabel("Email Address").FillAsync("ana.nguyen@example.com");

        // State select: pick the first real option (index 0 is the placeholder).
        await Page.GetByLabel(new Regex("^State"))
            .SelectOptionAsync(new SelectOptionValue { Index = 1 });

        // Phone (segmented area / prefix / line inputs, by accessible label).
        await Page.GetByLabel("Phone area code", new() { Exact = true }).FillAsync("510");
        await Page.GetByLabel("Phone prefix", new() { Exact = true }).FillAsync("555");
        await Page.GetByLabel("Phone line number", new() { Exact = true }).FillAsync("0100");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Continue" }).ClickAsync();

        // Step 2 (Project) should now render.
        await Expect(Page.GetByText("Project / Site Location")).ToBeVisibleAsync(
            new() { Timeout = 10_000 });
        await Expect(Page.GetByText("Property Owner")).ToBeVisibleAsync();
    }
}
