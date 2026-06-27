using NUnit.Framework;

namespace SBOMViewer.E2E.Tests;

[TestFixture]
public class FileUploadTests : TestBase
{
    // Navigate 4 levels up from bin/Release/net10.0/ to repo root, then into samples/
    private static string SamplesDir =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../samples"));

    private async Task UploadFile(string filePath)
    {
        await Page.Locator("input[type='file']").SetInputFilesAsync(filePath);
    }

    [Test]
    public async Task Upload_CycloneDX16_RendersDashboard()
    {
        await UploadFile(Path.Combine(SamplesDir, "cyclonedx-1.6-minimal.json"));
        // DynamicSbomViewer renders .dashboard after a successful upload
        await Expect(Page.Locator(".dashboard")).ToBeVisibleAsync(new() { Timeout = 15_000 });
    }

    [Test]
    public async Task Upload_CycloneDX16_ShowsOverviewTab()
    {
        await UploadFile(Path.Combine(SamplesDir, "cyclonedx-1.6-minimal.json"));
        // Overview is the default active tab with stat cards
        await Expect(Page.Locator(".stats-grid")).ToBeVisibleAsync(new() { Timeout = 15_000 });
    }

    [Test]
    public async Task Upload_CycloneDX16_ShowsComponentsSidebarItem()
    {
        await UploadFile(Path.Combine(SamplesDir, "cyclonedx-1.6-minimal.json"));
        // DynamicSbomViewer: sidebar-item for Components when _componentCount > 0
        await Expect(Page.Locator(".sidebar-item", new() { HasText = "Components" }))
            .ToBeVisibleAsync(new() { Timeout = 15_000 });
    }

    [Test]
    public async Task Upload_SPDX22_RendersDashboard()
    {
        await UploadFile(Path.Combine(SamplesDir, "spdx-2.2-minimal.json"));
        await Expect(Page.Locator(".dashboard")).ToBeVisibleAsync(new() { Timeout = 15_000 });
    }

    [Test]
    public async Task Upload_SPDX22_ShowsComponentsSidebarItem()
    {
        await UploadFile(Path.Combine(SamplesDir, "spdx-2.2-minimal.json"));
        // SPDX packages are extracted as component rows and shown under "Components" in the sidebar
        await Expect(Page.Locator(".sidebar-item", new() { HasText = "Components" }))
            .ToBeVisibleAsync(new() { Timeout = 15_000 });
    }

    [Test]
    public async Task Upload_CycloneDX17_RendersDashboard()
    {
        await UploadFile(Path.Combine(SamplesDir, "cyclonedx-1.7-full.json"));
        await Expect(Page.Locator(".dashboard")).ToBeVisibleAsync(new() { Timeout = 15_000 });
    }

    [Test]
    public async Task Upload_UnsupportedVersion_ShowsError()
    {
        await UploadFile(Path.Combine(SamplesDir, "cyclonedx-1.4-unsupported.json"));
        // UploadFile.razor: <div class="upload-error">⚠ Version "..." is not supported.</div>
        await Expect(Page.Locator(".upload-error", new() { HasText = "not supported" }))
            .ToBeVisibleAsync(new() { Timeout = 10_000 });
    }

    [Test]
    public async Task Upload_CycloneDX15_RendersDashboard()
    {
        await UploadFile(Path.Combine(SamplesDir, "cyclonedx-1.5-full.json"));
        await Expect(Page.Locator(".dashboard")).ToBeVisibleAsync(new() { Timeout = 15_000 });
    }

    [Test]
    public async Task Upload_SPDX23_RendersDashboard()
    {
        await UploadFile(Path.Combine(SamplesDir, "spdx-2.3-full.json"));
        await Expect(Page.Locator(".dashboard")).ToBeVisibleAsync(new() { Timeout = 15_000 });
    }

    [Test]
    public async Task Upload_SPDX30_RendersDashboard()
    {
        await UploadFile(Path.Combine(SamplesDir, "spdx-3.0.1-full.json"));
        await Expect(Page.Locator(".dashboard")).ToBeVisibleAsync(new() { Timeout = 15_000 });
    }

    [Test]
    public async Task Upload_InvalidJson_ShowsError()
    {
        var tempPath = Path.ChangeExtension(Path.GetTempFileName(), ".json");
        await File.WriteAllTextAsync(tempPath, "{ invalid json {{{");
        try
        {
            await UploadFile(tempPath);
            // UploadFile.razor: <div class="upload-error">⚠ Unrecognized SBOM format...</div>
            await Expect(Page.Locator(".upload-error", new() { HasText = "Unrecognized SBOM format" }))
                .ToBeVisibleAsync(new() { Timeout = 10_000 });
        }
        finally { File.Delete(tempPath); }
    }

    [Test]
    public async Task Search_InComponentsTab_FiltersResults()
    {
        // cyclonedx-1.6-full.json contains .NET packages — "json" matches Newtonsoft.Json
        await UploadFile(Path.Combine(SamplesDir, "cyclonedx-1.6-full.json"));
        await Expect(Page.Locator(".dashboard")).ToBeVisibleAsync(new() { Timeout = 15_000 });

        // Navigate to the Components tab via the sidebar
        await Page.Locator(".sidebar-item", new() { HasText = "Components" }).ClickAsync();

        // The toolbar search box is a plain <input> inside .search-box
        var searchInput = Page.Locator(".search-box input").First;
        await Expect(searchInput).ToBeVisibleAsync(new() { Timeout = 10_000 });
        await searchInput.FillAsync("json");

        // Use Expect (auto-waits for Blazor re-render) to confirm at least one row is visible
        await Expect(Page.Locator(".data-table tbody tr").First).ToBeVisibleAsync(new() { Timeout = 5_000 });
    }
}
