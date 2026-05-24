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
        // Wait for Blazor WASM to bootstrap — .nav is always present in Home.razor
        await Page.WaitForSelectorAsync(".nav", new() { Timeout = 30_000 });
    }
}
