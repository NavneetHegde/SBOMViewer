using Microsoft.Playwright;
using NUnit.Framework;

namespace SBOMViewer.E2E.Tests;

[TestFixture]
public class QualityTests : TestBase
{
    private static string SamplesDir =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../samples"));

    private async Task UploadAndOpenQuality(string file)
    {
        await Page.Locator("input[type='file']").SetInputFilesAsync(Path.Combine(SamplesDir, file));
        await Expect(Page.Locator(".dashboard")).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await Page.Locator(".sidebar-item", new() { HasText = "Quality" }).ClickAsync();
        await Expect(Page.Locator(".quality-headline")).ToBeVisibleAsync(new() { Timeout = 10_000 });
    }

    [Test]
    public async Task Quality_ShowsHeadlineAndChecklist()
    {
        await UploadAndOpenQuality("cyclonedx-1.6-full.json");

        await Expect(Page.Locator(".quality-headline")).ToContainTextAsync("checks pass against CISA 2026");
        await Expect(Page.Locator(".quality-check")).ToHaveCountAsync(16);
    }

    [Test]
    public async Task Quality_SwitchingProfile_ChangesTheCheckCount()
    {
        await UploadAndOpenQuality("cyclonedx-1.6-full.json");
        await Expect(Page.Locator(".quality-check")).ToHaveCountAsync(16);

        await Page.Locator("[data-profile='Ntia2021']").ClickAsync();

        await Expect(Page.Locator(".quality-headline")).ToContainTextAsync("NTIA 2021");
        await Expect(Page.Locator(".quality-check")).ToHaveCountAsync(7);
    }

    [Test]
    public async Task Quality_ExpandingACheck_ShowsFixHintAndAffectedComponents()
    {
        await UploadAndOpenQuality("cyclonedx-1.6-full.json");

        var hash = Page.Locator(".quality-check[data-check='hash']");
        await hash.Locator("summary").ClickAsync();

        await Expect(hash.Locator(".quality-hint")).ToBeVisibleAsync();
        await Expect(hash.Locator(".quality-affected li", new() { HasText = "Serilog" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task Quality_Spdx2_MarksGenerationContextNotExpressible()
    {
        await UploadAndOpenQuality("spdx-2.3-full.json");

        await Expect(Page.Locator(".quality-check[data-check='generation-context']"))
            .ToContainTextAsync("not expressible in this format");
    }

    [Test]
    public async Task Overview_QualityCard_OpensTheQualityTab()
    {
        await Page.Locator("input[type='file']").SetInputFilesAsync(Path.Combine(SamplesDir, "cyclonedx-1.6-minimal.json"));
        await Expect(Page.Locator(".dashboard")).ToBeVisibleAsync(new() { Timeout = 15_000 });

        await Page.Locator(".quality-stat").ClickAsync();

        await Expect(Page.Locator(".quality-headline")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Quality_ShowsBetaAndAReportLinkPerCheck()
    {
        await UploadAndOpenQuality("cyclonedx-1.6-full.json");

        await Expect(Page.Locator(".quality-headline .beta-chip")).ToBeVisibleAsync();
        var hash = Page.Locator(".quality-check[data-check='hash']");
        await hash.Locator("summary").ClickAsync();

        var link = hash.Locator(".quality-report-link");
        await Expect(link).ToBeVisibleAsync();
        var href = await link.GetAttributeAsync("href") ?? "";
        Assert.That(href, Does.StartWith("https://github.com/NavneetHegde/SBOMViewer/issues/new"));
        Assert.That(href, Does.Not.Contain("cyclonedx-1.6-full"), "The prefill must never carry the file name");
        Assert.That(href, Does.Not.Contain("Serilog"), "The prefill must never carry component names");
    }

    /// <summary>
    /// Above the auto-run limit (2,000 components) the check must not run on load — it waits for a click.
    /// Also logs in-browser timings, which is what the provisional limit should be tuned against.
    /// </summary>
    [Test]
    public async Task LargeSbom_WaitsForAClick_ThenRuns()
    {
        const int count = 5000;
        var path = Path.Combine(Path.GetTempPath(), $"quality-large-{Guid.NewGuid():N}.json");
        var comps = string.Join(",", Enumerable.Range(0, count).Select(i =>
            $"{{\"type\":\"library\",\"bom-ref\":\"r{i}\",\"name\":\"pkg{i}\",\"version\":\"1.0.{i}\"," +
            $"\"purl\":\"pkg:npm/pkg{i}@1.0.{i}\",\"licenses\":[{{\"license\":{{\"id\":\"MIT\"}}}}]}}"));
        var deps = string.Join(",", Enumerable.Range(0, count).Select(i =>
            $"{{\"ref\":\"r{i}\",\"dependsOn\":[\"r{(i + 1) % count}\"]}}"));
        await File.WriteAllTextAsync(path,
            $"{{\"bomFormat\":\"CycloneDX\",\"specVersion\":\"1.6\",\"version\":1," +
            $"\"metadata\":{{\"timestamp\":\"2026-01-01T00:00:00Z\"}},\"components\":[{comps}],\"dependencies\":[{deps}]}}");

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await Page.Locator("input[type='file']").SetInputFilesAsync(path);
            await Expect(Page.Locator(".dashboard")).ToBeVisibleAsync(new() { Timeout = 60_000 });
            var loadMs = sw.ElapsedMilliseconds;

            await Expect(Page.Locator(".quality-stat")).ToContainTextAsync("Not checked");
            await Page.Locator(".sidebar-item", new() { HasText = "Quality" }).ClickAsync();
            await Expect(Page.Locator(".quality-headline")).ToHaveCountAsync(0);

            sw.Restart();
            await Page.Locator(".quality-run-btn").ClickAsync();
            await Expect(Page.Locator(".quality-headline")).ToBeVisibleAsync(new() { Timeout = 60_000 });
            var checkMs = sw.ElapsedMilliseconds;

            sw.Restart();
            await Page.Locator("[data-profile='Ntia2021']").ClickAsync();
            await Expect(Page.Locator(".quality-headline")).ToContainTextAsync("NTIA 2021");
            var switchMs = sw.ElapsedMilliseconds;

            // Affected lists render at most 200 rows however many components fail.
            var supplier = Page.Locator(".quality-check[data-check='supplier']");
            await supplier.Locator("summary").ClickAsync();
            await Expect(supplier.Locator(".quality-affected li")).ToHaveCountAsync(200);
            await Expect(supplier.Locator(".quality-more")).ToContainTextAsync("4800 more");

            TestContext.Out.WriteLine(
                $"QUALITY-PERF components={count} load={loadMs}ms check={checkMs}ms profileSwitch={switchMs}ms");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Quality_DownloadJson_SavesTheSelectedProfileWithFullAffectedLists()
    {
        await UploadAndOpenQuality("cyclonedx-1.6-full.json");
        await Page.Locator("[data-profile='Ntia2021']").ClickAsync();
        await Expect(Page.Locator(".quality-headline")).ToContainTextAsync("NTIA 2021");

        var download = await Page.RunAndWaitForDownloadAsync(() => Page.Locator(".quality-download-btn").ClickAsync());

        Assert.That(download.SuggestedFilename, Is.EqualTo("cyclonedx-1.6-full.quality-ntia-2021.json"));
        var path = await download.PathAsync();
        using var doc = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(path!));
        var root = doc.RootElement;
        Assert.That(root.GetProperty("profile").GetProperty("id").GetString(), Is.EqualTo("ntia-2021"));
        Assert.That(root.GetProperty("checks").GetArrayLength(), Is.EqualTo(7));
        Assert.That(root.GetProperty("source").GetProperty("fileName").GetString(), Is.EqualTo("cyclonedx-1.6-full.json"));
        Assert.That(root.GetProperty("disclaimer").GetString(), Does.Contain("not a compliance determination"));

        // Unlike the GitHub report link, the local file names the affected components.
        var supplier = root.GetProperty("checks").EnumerateArray().Single(c => c.GetProperty("id").GetString() == "supplier");
        Assert.That(supplier.GetProperty("affectedComponents").GetArrayLength(),
            Is.EqualTo(supplier.GetProperty("affected").GetInt32()));
    }

    [Test]
    public async Task Quality_MakesNoNetworkRequests()
    {
        await Page.Locator("input[type='file']").SetInputFilesAsync(Path.Combine(SamplesDir, "cyclonedx-1.6-full.json"));
        await Expect(Page.Locator(".dashboard")).ToBeVisibleAsync(new() { Timeout = 15_000 });

        var requests = new List<string>();
        void OnRequest(object? _, IRequest r) => requests.Add(r.Url);
        Page.Request += OnRequest;

        await Page.Locator(".sidebar-item", new() { HasText = "Quality" }).ClickAsync();
        await Expect(Page.Locator(".quality-headline")).ToBeVisibleAsync();
        await Page.Locator("[data-profile='Ntia2021']").ClickAsync();
        await Expect(Page.Locator(".quality-headline")).ToContainTextAsync("NTIA 2021");

        Page.Request -= OnRequest;
        Assert.That(requests, Is.Empty, "The Quality tab must not make network requests");
    }
}
