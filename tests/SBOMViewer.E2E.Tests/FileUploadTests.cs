using NUnit.Framework;

namespace SBOMViewer.E2E;

[TestFixture]
public class FileUploadTests : TestBase
{
    // Navigate 4 levels up from bin/Release/net10.0/ to repo root, then into samples/
    private static string SamplesDir =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../samples"));

    private async Task UploadFile(string filePath)
    {
        await Page.Locator("input[type='file']").SetInputFilesAsync(filePath);
    }

    [Test]
    public async Task Upload_CycloneDX16_RendersAccordion()
    {
        await UploadFile(Path.Combine(SamplesDir, "cyclonedx-1.6-minimal.json"));
        await Expect(Page.Locator("fluent-accordion")).ToBeVisibleAsync(new() { Timeout = 15_000 });
    }

    [Test]
    public async Task Upload_CycloneDX16_ShowsGeneralInfoSection()
    {
        await UploadFile(Path.Combine(SamplesDir, "cyclonedx-1.6-minimal.json"));
        await Expect(Page.Locator("fluent-accordion-item", new() { HasText = "General Information" }))
            .ToBeVisibleAsync(new() { Timeout = 15_000 });
    }

    [Test]
    public async Task Upload_CycloneDX16_ShowsComponentsSection()
    {
        await UploadFile(Path.Combine(SamplesDir, "cyclonedx-1.6-minimal.json"));
        await Expect(Page.Locator("fluent-accordion-item", new() { HasText = "Components" }))
            .ToBeVisibleAsync(new() { Timeout = 15_000 });
    }

    [Test]
    public async Task Upload_SPDX22_RendersAccordion()
    {
        await UploadFile(Path.Combine(SamplesDir, "spdx-2.2-minimal.json"));
        await Expect(Page.Locator("fluent-accordion")).ToBeVisibleAsync(new() { Timeout = 15_000 });
    }

    [Test]
    public async Task Upload_SPDX22_ShowsPackagesSection()
    {
        await UploadFile(Path.Combine(SamplesDir, "spdx-2.2-minimal.json"));
        await Expect(Page.Locator("fluent-accordion-item", new() { HasText = "Packages" }))
            .ToBeVisibleAsync(new() { Timeout = 15_000 });
    }

    [Test]
    public async Task Upload_CycloneDX17_RendersAccordion()
    {
        await UploadFile(Path.Combine(SamplesDir, "cyclonedx-1.7-full.json"));
        await Expect(Page.Locator("fluent-accordion")).ToBeVisibleAsync(new() { Timeout = 15_000 });
    }

    [Test]
    public async Task Upload_UnsupportedVersion_ShowsError()
    {
        await UploadFile(Path.Combine(SamplesDir, "cyclonedx-1.5-unsupported.json"));
        // UploadFile.razor: errorMessage = $"Version \"...\" is not supported."
        await Expect(Page.Locator("fluent-badge", new() { HasText = "not supported" }))
            .ToBeVisibleAsync(new() { Timeout = 10_000 });
    }

    [Test]
    public async Task Upload_InvalidJson_ShowsError()
    {
        var tempPath = Path.ChangeExtension(Path.GetTempFileName(), ".json");
        await File.WriteAllTextAsync(tempPath, "{ invalid json {{{");
        try
        {
            await UploadFile(tempPath);
            // UploadFile.razor: errorMessage = "Unrecognized SBOM format. Please upload a valid..."
            await Expect(Page.Locator("fluent-badge", new() { HasText = "Unrecognized SBOM format" }))
                .ToBeVisibleAsync(new() { Timeout = 10_000 });
        }
        finally { File.Delete(tempPath); }
    }

    [Test]
    public async Task Search_InComponentsSection_FiltersResults()
    {
        // cyclonedx-1.6-full.json has enough components to trigger FluentSearch (>5 items)
        await UploadFile(Path.Combine(SamplesDir, "cyclonedx-1.6-full.json"));
        await Page.Locator("fluent-accordion-item", new() { HasText = "Components" }).ClickAsync();
        var searchBox = Page.Locator("fluent-search").First;
        await Expect(searchBox).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await searchBox.FillAsync("express");
        // Verify some filtered results remain visible
        Assert.That(await Page.Locator("details:visible").CountAsync(), Is.GreaterThan(0));
    }
}
