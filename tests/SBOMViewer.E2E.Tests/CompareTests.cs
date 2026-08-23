using NUnit.Framework;

namespace SBOMViewer.E2E.Tests;

[TestFixture]
public class CompareTests : TestBase
{
    // Navigate 4 levels up from bin/Release/net10.0/ to repo root, then into samples/
    private static string SamplesDir =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../samples"));

    private static string Sample(string name) => Path.Combine(SamplesDir, name);

    private async Task GotoCompare()
    {
        await Page.GotoAsync($"{BaseUrl}/compare");
        await Page.WaitForSelectorAsync(".compare-slots", new() { Timeout = 30_000 });
    }

    /// <summary>Fills the baseline slot, then the current slot. Each slot renders its own file input.</summary>
    private async Task UploadPair(string baseline, string current)
    {
        await Page.Locator(".compare-slot input[type='file']").Nth(0).SetInputFilesAsync(baseline);
        // Once the baseline slot is filled it swaps to a summary card, so the remaining
        // file input is the current slot's.
        await Expect(Page.Locator(".compare-slot-filled")).ToHaveCountAsync(1, new() { Timeout = 15_000 });
        await Page.Locator(".compare-slot input[type='file']").First.SetInputFilesAsync(current);
        await Expect(Page.Locator(".compare-slot-filled")).ToHaveCountAsync(2, new() { Timeout = 15_000 });
    }

    [Test]
    public async Task Compare_RouteLoads()
    {
        await GotoCompare();
        await Expect(Page.Locator(".compare-slots")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Compare_CardOnLandingPage_IsVisible()
    {
        await Expect(Page.Locator(".compare-card")).ToBeVisibleAsync();
        await Expect(Page.Locator(".compare-card")).ToContainTextAsync("Compare two SBOMs");
    }

    [Test]
    public async Task Compare_CardFromUploadScreen_Navigates()
    {
        await Page.Locator(".compare-card").ClickAsync();
        await Expect(Page.Locator(".compare-slots")).ToBeVisibleAsync(new() { Timeout = 15_000 });
    }

    [Test]
    public async Task Compare_FromViewer_SeedsBaselineAndAsksForSecondFile()
    {
        // Open a single file in the normal viewer first.
        await Page.Locator("input[type='file']").SetInputFilesAsync(Sample("cyclonedx-1.6-minimal.json"));
        await Expect(Page.Locator(".dashboard")).ToBeVisibleAsync(new() { Timeout = 15_000 });

        await Page.Locator(".btn-compare").ClickAsync();

        // The opened file becomes the baseline and the current slot is highlighted as pending.
        await Expect(Page.Locator(".compare-slots")).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await Expect(Page.Locator(".compare-slot-filled")).ToHaveCountAsync(1);
        await Expect(Page.Locator(".compare-slot-file")).ToContainTextAsync("cyclonedx-1.6-minimal.json");
        await Expect(Page.Locator(".compare-dropzone.awaiting")).ToBeVisibleAsync();
        await Expect(Page.Locator(".compare-hint")).ToContainTextAsync("baseline");
    }

    [Test]
    public async Task Compare_FromViewer_ThenSecondFile_ProducesDiff()
    {
        await Page.Locator("input[type='file']").SetInputFilesAsync(Sample("cyclonedx-1.6-minimal.json"));
        await Expect(Page.Locator(".dashboard")).ToBeVisibleAsync(new() { Timeout = 15_000 });

        await Page.Locator(".btn-compare").ClickAsync();
        await Expect(Page.Locator(".compare-slot-filled")).ToHaveCountAsync(1, new() { Timeout = 15_000 });

        await Page.Locator(".compare-slot input[type='file']").First
            .SetInputFilesAsync(Sample("cyclonedx-1.6-full.json"));

        await Expect(Page.Locator(".stats-grid")).ToBeVisibleAsync(new() { Timeout = 15_000 });
    }

    [Test]
    public async Task Compare_BeforeBothFilesChosen_ShowsHint()
    {
        await GotoCompare();
        await Expect(Page.Locator(".compare-hint")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Compare_SameFileTwice_ReportsNoDifferences()
    {
        await GotoCompare();
        await UploadPair(Sample("cyclonedx-1.6-full.json"), Sample("cyclonedx-1.6-full.json"));

        await Expect(Page.Locator(".compare-hint", new() { HasText = "No component-level differences" }))
            .ToBeVisibleAsync(new() { Timeout = 15_000 });
    }

    [Test]
    public async Task Compare_DifferentFiles_ShowsDiffStats()
    {
        await GotoCompare();
        await UploadPair(Sample("cyclonedx-1.6-minimal.json"), Sample("cyclonedx-1.6-full.json"));

        await Expect(Page.Locator(".stats-grid")).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await Expect(Page.Locator(".tab-bar")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Compare_AcrossFormats_ProducesADiff()
    {
        await GotoCompare();
        await UploadPair(Sample("spdx-2.2-full.json"), Sample("cyclonedx-1.6-full.json"));

        await Expect(Page.Locator(".stats-grid")).ToBeVisibleAsync(new() { Timeout = 15_000 });
    }

    [Test]
    public async Task Compare_OpenInViewer_OpensThatFileAlone()
    {
        await GotoCompare();
        await UploadPair(Sample("spdx-2.2-full.json"), Sample("cyclonedx-1.6-full.json"));

        // Open the baseline (first slot) on its own.
        await Page.Locator(".btn-open-viewer").First.ClickAsync();

        await Expect(Page.Locator(".dashboard")).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await Expect(Page.Locator(".sbom-meta-name")).ToContainTextAsync("spdx-2.2-full.json");
    }

    [Test]
    public async Task Compare_OpenInViewer_ThenBack_KeepsTheComparison()
    {
        await GotoCompare();
        await UploadPair(Sample("spdx-2.2-full.json"), Sample("cyclonedx-1.6-full.json"));

        await Page.Locator(".btn-open-viewer").First.ClickAsync();
        await Expect(Page.Locator(".dashboard")).ToBeVisibleAsync(new() { Timeout = 15_000 });

        // Returning must not reset the comparison — both slots should still be filled.
        await Page.Locator(".btn-compare").ClickAsync();

        await Expect(Page.Locator(".compare-slot-filled")).ToHaveCountAsync(2, new() { Timeout = 15_000 });
        await Expect(Page.Locator(".stats-grid")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Compare_Swap_ReversesTheDirection()
    {
        await GotoCompare();
        await UploadPair(Sample("cyclonedx-1.6-minimal.json"), Sample("cyclonedx-1.6-full.json"));
        await Expect(Page.Locator(".stats-grid")).ToBeVisibleAsync(new() { Timeout = 15_000 });

        var addedBefore   = await Page.Locator(".stat-card").Nth(0).Locator(".stat-value").InnerTextAsync();
        var removedBefore = await Page.Locator(".stat-card").Nth(1).Locator(".stat-value").InnerTextAsync();

        await Page.Locator(".compare-swap").ClickAsync();

        // Baseline and current change places, so added and removed trade values.
        await Expect(Page.Locator(".compare-slot-file").First).ToContainTextAsync("cyclonedx-1.6-full.json");
        await Expect(Page.Locator(".stat-card").Nth(0).Locator(".stat-value")).ToHaveTextAsync(removedBefore);
        await Expect(Page.Locator(".stat-card").Nth(1).Locator(".stat-value")).ToHaveTextAsync(addedBefore);
    }

    [Test]
    public async Task Compare_SwapButton_HiddenUntilAFileIsChosen()
    {
        await GotoCompare();
        await Expect(Page.Locator(".compare-swap")).ToHaveCountAsync(0);

        await Page.Locator(".compare-slot input[type='file']").Nth(0)
            .SetInputFilesAsync(Sample("cyclonedx-1.6-minimal.json"));

        await Expect(Page.Locator(".compare-swap")).ToBeVisibleAsync(new() { Timeout = 15_000 });
    }

    [Test]
    public async Task Compare_Reset_ClearsBothSlots()
    {
        await GotoCompare();
        await UploadPair(Sample("cyclonedx-1.6-minimal.json"), Sample("cyclonedx-1.6-full.json"));

        await Page.Locator(".btn-ghost", new() { HasText = "Reset" }).ClickAsync();

        await Expect(Page.Locator(".compare-slot-filled")).ToHaveCountAsync(0, new() { Timeout = 15_000 });
        await Expect(Page.Locator(".compare-hint")).ToBeVisibleAsync();
    }
}
