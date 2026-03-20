using NUnit.Framework;

namespace SBOMViewer.E2E;

[TestFixture]
public class HomePageTests : TestBase
{
    [Test]
    public async Task PageTitle_ContainsSbomViewer() =>
        Assert.That(await Page.TitleAsync(), Does.Contain("SBOM Viewer"));

    [Test]
    public async Task Header_DisplaysAppName() =>
        await Expect(Page.Locator("fluent-header span")).ToContainTextAsync("SBOM Viewer");

    [Test]
    public async Task UploadButton_IsVisible() =>
        // <FluentButton Id="MyUploadStream">Upload SBOM</FluentButton> in UploadFile.razor
        await Expect(Page.Locator("fluent-button#MyUploadStream")).ToBeVisibleAsync();

    [Test]
    public async Task SupportedFormatBadges_AreDisplayed()
    {
        await Expect(Page.Locator("fluent-badge", new() { HasText = "CycloneDX 1.6" })).ToBeVisibleAsync();
        await Expect(Page.Locator("fluent-badge", new() { HasText = "CycloneDX 1.7" })).ToBeVisibleAsync();
        await Expect(Page.Locator("fluent-badge", new() { HasText = "SPDX 2.2" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task NoSbomLoaded_ShowsPlaceholderCard()
    {
        // Home.razor: <FluentCard>No SBOM loaded yet. Please select a format and upload a file.</FluentCard>
        await Expect(Page.Locator("fluent-card", new() { HasText = "No SBOM loaded yet" }))
            .ToBeVisibleAsync();
    }

    [Test]
    public async Task ThemeToggle_IsVisible() =>
        // <FluentButton Title="Theme" /> in MainLayout.razor
        await Expect(Page.Locator("fluent-button[title='Theme']")).ToBeVisibleAsync();

    [Test]
    public async Task Footer_ContainsCopyright() =>
        // FluentFooter in MainLayout.razor: "© 2025. Licensed under the MIT License. SBOM Viewer v3.0"
        await Expect(Page.Locator("fluent-footer")).ToContainTextAsync("SBOM Viewer v3.0");
}
