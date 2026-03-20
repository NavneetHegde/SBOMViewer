using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace SBOMViewer.E2E.Tests;

public abstract class TestBase : PageTest
{
    protected string BaseUrl { get; private set; } = string.Empty;

    [SetUp]
    public async Task NavigateToHome()
    {
        BaseUrl = Environment.GetEnvironmentVariable("BASE_URL") ?? "http://localhost:5000";
        await Page.GotoAsync(BaseUrl);
        // Wait for Blazor WASM to bootstrap — fluent-toolbar is always present in MainLayout.razor
        await Page.WaitForSelectorAsync("fluent-toolbar", new() { Timeout = 30_000 });
    }
}
