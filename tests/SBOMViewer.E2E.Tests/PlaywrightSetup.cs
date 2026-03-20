using NUnit.Framework;

namespace SBOMViewer.E2E.Tests;

[SetUpFixture]
public class PlaywrightSetup
{
    [OneTimeSetUp]
    public void InstallBrowsers()
    {
        // Safety net for local runs; CI installs via playwright.ps1 step
        var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
        if (exitCode != 0)
            throw new Exception($"Playwright install failed: exit code {exitCode}");
    }
}
