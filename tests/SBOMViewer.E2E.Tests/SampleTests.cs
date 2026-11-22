using System.Text.RegularExpressions;
using NUnit.Framework;

namespace SBOMViewer.E2E.Tests;

/// <summary>
/// Covers "Try a sample": the landing-page link, the Sample chip, the /?sample=1 deep link, and
/// the rule that the demo document never lands in the recent-files store.
/// </summary>
[TestFixture]
public class SampleTests : TestBase
{
    private const string SampleFile = "demo-storefront.cdx.json";

    [Test]
    public async Task Sample_LinkOpensTheDashboardWithASampleChip()
    {
        await Page.Locator(".sample-link").ClickAsync();

        await Expect(Page.Locator(".dashboard")).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await Expect(Page.Locator(".sbom-meta-name")).ToContainTextAsync(SampleFile);
        await Expect(Page.Locator(".sample-chip")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Sample_ButtonIsVisibleWithoutScrollingOnALaptopScreen()
    {
        // It used to sit below the two cards, where a short viewport pushed it under the fold.
        await Page.SetViewportSizeAsync(1366, 650);

        await Expect(Page.Locator(".sample-link")).ToBeInViewportAsync(new() { Ratio = 1 });
        await Expect(Page.Locator(".sample-link")).ToContainTextAsync("Try a sample");
    }

    [Test]
    public async Task Sample_ButtonSitsAboveTheUploadCards()
    {
        var button = await Page.Locator(".sample-link").BoundingBoxAsync();
        var cards  = await Page.Locator(".upload-options").BoundingBoxAsync();

        Assert.That(button, Is.Not.Null);
        Assert.That(cards, Is.Not.Null);
        Assert.That(button!.Y + button.Height, Is.LessThanOrEqualTo(cards!.Y),
            "the sample button must come before the cards, not after them");
    }

    [Test]
    public async Task Sample_ButtonIsOutsideTheDropzone()
    {
        // The InputFile overlay covers the whole dropzone and would swallow the click.
        await Expect(Page.Locator(".dropzone .sample-link")).ToHaveCountAsync(0);
        await Expect(Page.Locator(".quick-row .sample-link")).ToHaveCountAsync(1);
    }

    [Test]
    public async Task Sample_IsNotAddedToRecentFiles()
    {
        var samplesDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../samples"));

        await Page.EvaluateAsync("() => window.sbomRecentClear()");
        await Page.ReloadAsync();
        await Page.WaitForSelectorAsync(".dropzone", new() { Timeout = 30_000 });

        // Seed one real recent first. The strip is rendered only after the IndexedDB read comes
        // back, so "no strip" is true the instant the page appears and proves nothing; waiting for
        // a strip that must exist makes the check below run against the settled list.
        await Page.Locator("input[type='file']").SetInputFilesAsync(Path.Combine(samplesDir, "cyclonedx-1.6-minimal.json"));
        await Expect(Page.Locator(".dashboard")).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await Page.ReloadAsync();
        await Page.WaitForSelectorAsync(".dropzone", new() { Timeout = 30_000 });

        await Page.Locator(".sample-link").ClickAsync();
        await Expect(Page.Locator(".sample-chip")).ToBeVisibleAsync(new() { Timeout = 15_000 });

        await Page.ReloadAsync();
        await Page.WaitForSelectorAsync(".dropzone", new() { Timeout = 30_000 });

        await Expect(Page.Locator(".recent-item")).ToHaveCountAsync(1, new() { Timeout = 15_000 });
        // The entry also shows the document's subject ("· my-web-app 2.1.0") after the name.
        await Expect(Page.Locator(".recent-item-name")).ToHaveTextAsync(
            new System.Text.RegularExpressions.Regex(@"^cyclonedx-1\.6-minimal\.json( · .+)?$"));

        // And the store itself, independent of what the strip chooses to render.
        var stored = await Page.EvaluateAsync<string[]>("async () => (await window.sbomRecentList()).map(x => x.name)");
        Assert.That(stored, Is.EqualTo(new[] { "cyclonedx-1.6-minimal.json" }));
    }

    [Test]
    public async Task Sample_ChipSurvivesARoundTripThroughCompare()
    {
        await Page.Locator(".sample-link").ClickAsync();
        await Expect(Page.Locator(".sample-chip")).ToBeVisibleAsync(new() { Timeout = 15_000 });

        await Page.Locator(".btn-compare").ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex("/compare$"), new() { Timeout = 15_000 });

        await Page.GetByText("Open in viewer").First.ClickAsync();

        await Expect(Page.Locator(".sbom-meta-name")).ToContainTextAsync(SampleFile, new() { Timeout = 15_000 });
        await Expect(Page.Locator(".sample-chip")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Sample_ChipIsNotShownForAnUploadedFile()
    {
        var samplesDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../samples"));

        // Open the demo, go back, then open a real file — the label must not carry over.
        await Page.Locator(".sample-link").ClickAsync();
        await Expect(Page.Locator(".sample-chip")).ToBeVisibleAsync(new() { Timeout = 15_000 });

        await Page.Locator("button[title='New file']").ClickAsync();
        await Page.Locator("input[type='file']").SetInputFilesAsync(Path.Combine(samplesDir, "cyclonedx-1.6-minimal.json"));

        await Expect(Page.Locator(".sbom-meta-name")).ToContainTextAsync("cyclonedx-1.6-minimal.json", new() { Timeout = 15_000 });
        await Expect(Page.Locator(".sample-chip")).ToHaveCountAsync(0);
    }

    [Test, Category("Smoke")]
    public async Task Sample_DeepLinkOpensTheDashboardAndStripsTheParameter()
    {
        await Page.GotoAsync($"{BaseUrl}/?sample=1");

        await Expect(Page.Locator(".dashboard")).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Expect(Page.Locator(".sample-chip")).ToBeVisibleAsync();
        await Expect(Page).Not.ToHaveURLAsync(new Regex("sample="));
    }

    [Test]
    public async Task Sample_DeepLinkKeepsOtherQueryParameters()
    {
        // Shared links carry campaign parameters; only "sample" may be removed.
        await Page.GotoAsync($"{BaseUrl}/?utm_source=linkedin&sample=1&utm_campaign=launch");

        await Expect(Page.Locator(".sample-chip")).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Expect(Page).Not.ToHaveURLAsync(new Regex("sample="));
        await Expect(Page).ToHaveURLAsync(new Regex(@"utm_source=linkedin.*utm_campaign=launch"));
    }

    [Test]
    public async Task Sample_NewFileAfterDeepLinkReturnsToTheLandingPage()
    {
        await Page.GotoAsync($"{BaseUrl}/?sample=1");
        await Expect(Page.Locator(".dashboard")).ToBeVisibleAsync(new() { Timeout = 30_000 });

        await Page.Locator("button[title='New file']").ClickAsync();

        // The parameter was stripped, so the sample must not immediately reload.
        await Expect(Page.Locator(".dropzone")).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await Expect(Page.Locator(".dashboard")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Sample_FailedFetchShowsAReadableError()
    {
        await Page.RouteAsync("**/samples/*.json", route => route.AbortAsync());

        await Page.Locator(".sample-link").ClickAsync();

        await Expect(Page.Locator(".upload-error")).ToContainTextAsync("Couldn't load the sample", new() { Timeout = 15_000 });
        await Expect(Page.Locator(".upload-error")).ToBeInViewportAsync();
        await Expect(Page.Locator(".dropzone")).ToBeVisibleAsync();

        // The button is re-enabled, so the visitor can simply try again.
        await Expect(Page.Locator(".sample-link")).ToBeEnabledAsync();
    }
}
