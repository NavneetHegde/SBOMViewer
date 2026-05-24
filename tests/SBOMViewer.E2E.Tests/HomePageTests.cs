using NUnit.Framework;

namespace SBOMViewer.E2E.Tests;

[TestFixture]
public class HomePageTests : TestBase
{
    [Test]
    public async Task PageTitle_ContainsSbomViewer() =>
        Assert.That(await Page.TitleAsync(), Does.Contain("SBOM Viewer"));

    [Test]
    public async Task NavBar_DisplaysAppName() =>
        // Home.razor: <div class="nav-logo">SBOM Viewer</div>
        await Expect(Page.Locator(".nav-logo")).ToContainTextAsync("SBOM Viewer");

    [Test]
    public async Task Dropzone_IsVisible() =>
        // UploadFile.razor: <div class="dropzone ...">
        await Expect(Page.Locator(".dropzone")).ToBeVisibleAsync();

    [Test]
    public async Task SupportedFormatChips_AreDisplayed()
    {
        // UploadFile.razor: <span class="format-chip">@version</span> for each SupportedVersions
        await Expect(Page.Locator(".format-chip", new() { HasText = "CycloneDX 1.6" })).ToBeVisibleAsync();
        await Expect(Page.Locator(".format-chip", new() { HasText = "CycloneDX 1.7" })).ToBeVisibleAsync();
        await Expect(Page.Locator(".format-chip", new() { HasText = "SPDX 2.2" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task NoSbomLoaded_ShowsUploadHero()
    {
        // UploadFile.razor: <h1>Inspect your <span>SBOM</span><br/>in seconds.</h1>
        await Expect(Page.Locator(".upload-hero")).ToBeVisibleAsync();
        await Expect(Page.Locator(".upload-hero h1")).ToContainTextAsync("SBOM");
    }

    [Test]
    public async Task ThemeToggle_IsVisible() =>
        // Home.razor: <button class="theme-toggle" ...>
        await Expect(Page.Locator("button.theme-toggle")).ToBeVisibleAsync();

    [Test]
    public async Task FontControls_AreVisible()
    {
        // Home.razor: A− / A+ font scale buttons
        await Expect(Page.Locator(".font-controls")).ToBeVisibleAsync();
        await Expect(Page.Locator("button.font-btn.minus")).ToBeVisibleAsync();
        await Expect(Page.Locator("button.font-btn.plus")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Footer_ShowsAppVersion() =>
        // Home.razor footer: <span>SBOM Viewer v@AppVersion</span>
        await Expect(Page.Locator(".app-footer")).ToContainTextAsync("v");
}
