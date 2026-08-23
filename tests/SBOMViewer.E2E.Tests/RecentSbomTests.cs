using NUnit.Framework;

namespace SBOMViewer.E2E.Tests;

/// <summary>
/// Covers the IndexedDB-backed "recent files" shortcut. Each test starts by clearing the store so
/// the shared browser profile cannot leak state between tests.
/// </summary>
[TestFixture]
public class RecentSbomTests : TestBase
{
    private static string SamplesDir =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../samples"));

    private static string Sample(string name) => Path.Combine(SamplesDir, name);

    private async Task ClearStoreAndReload()
    {
        await Page.EvaluateAsync("() => window.sbomRecentClear()");
        await Page.ReloadAsync();
        await Page.WaitForSelectorAsync(".nav", new() { Timeout = 30_000 });
    }

    private async Task UploadAndReturnHome(string file)
    {
        await Page.Locator("input[type='file']").SetInputFilesAsync(file);
        await Expect(Page.Locator(".dashboard")).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await Page.ReloadAsync();
        await Page.WaitForSelectorAsync(".dropzone", new() { Timeout = 30_000 });
    }

    [Test]
    public async Task Recent_NotShownBeforeAnyUpload()
    {
        await ClearStoreAndReload();
        await Expect(Page.Locator(".recent-bar")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Recent_UploadedFileSurvivesAReload()
    {
        await ClearStoreAndReload();
        await UploadAndReturnHome(Sample("cyclonedx-1.6-full.json"));

        await Expect(Page.Locator(".recent-bar")).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await Expect(Page.Locator(".recent-item-name")).ToContainTextAsync("cyclonedx-1.6-full.json");
    }

    [Test]
    public async Task Recent_ClickingAnEntryReopensIt()
    {
        await ClearStoreAndReload();
        await UploadAndReturnHome(Sample("cyclonedx-1.6-full.json"));

        await Page.Locator(".recent-item").First.ClickAsync();

        await Expect(Page.Locator(".dashboard")).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await Expect(Page.Locator(".sbom-meta-name")).ToContainTextAsync("cyclonedx-1.6-full.json");
    }

    [Test]
    public async Task Recent_KeepsAtMostTwoAndEvictsTheOldest()
    {
        await ClearStoreAndReload();
        await UploadAndReturnHome(Sample("spdx-2.2-full.json"));
        await UploadAndReturnHome(Sample("cyclonedx-1.6-full.json"));
        await UploadAndReturnHome(Sample("cyclonedx-1.7-full.json"));

        await Expect(Page.Locator(".recent-item")).ToHaveCountAsync(2, new() { Timeout = 15_000 });

        // The two newest survive; the first upload has been evicted.
        var names = await Page.Locator(".recent-item-name").AllInnerTextsAsync();
        Assert.That(string.Join(",", names), Does.Not.Contain("spdx-2.2-full.json"));
        Assert.That(string.Join(",", names), Does.Contain("cyclonedx-1.7-full.json"));
    }

    [Test]
    public async Task Recent_ReuploadingSameFileDoesNotDuplicateIt()
    {
        await ClearStoreAndReload();
        await UploadAndReturnHome(Sample("cyclonedx-1.6-full.json"));
        await UploadAndReturnHome(Sample("cyclonedx-1.6-full.json"));

        await Expect(Page.Locator(".recent-item")).ToHaveCountAsync(1, new() { Timeout = 15_000 });
    }

    [Test]
    public async Task Recent_ClearRemovesEverything()
    {
        await ClearStoreAndReload();
        await UploadAndReturnHome(Sample("cyclonedx-1.6-full.json"));
        await Expect(Page.Locator(".recent-bar")).ToBeVisibleAsync(new() { Timeout = 15_000 });

        await Page.Locator(".recent-clear").ClickAsync();

        await Expect(Page.Locator(".recent-bar")).ToHaveCountAsync(0, new() { Timeout = 15_000 });
    }

    [Test]
    public async Task Recent_ShortcutFillsAComparisonSlot()
    {
        await ClearStoreAndReload();
        await UploadAndReturnHome(Sample("cyclonedx-1.6-full.json"));

        await Page.GotoAsync($"{BaseUrl}/compare");
        await Page.WaitForSelectorAsync(".compare-slots", new() { Timeout = 30_000 });

        await Page.Locator(".slot-recent-item").First.ClickAsync();

        await Expect(Page.Locator(".compare-slot-filled")).ToHaveCountAsync(1, new() { Timeout = 15_000 });
        await Expect(Page.Locator(".compare-slot-file")).ToContainTextAsync("cyclonedx-1.6-full.json");
    }
}
