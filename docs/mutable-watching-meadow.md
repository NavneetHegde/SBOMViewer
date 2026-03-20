# CI/CD Pipeline + Playwright E2E Plan

## Context

The current pipeline has multiple bugs (wrong .NET version, broken path filters, no tests before deploy, no PR preview support) and zero UI test coverage. The goal is to add a proper multi-stage CI/CD flow:
- Unit tests + E2E Playwright tests run on every PR before anything reaches production
- Azure SWA preview environments spin up for PRs and are torn down when PRs close
- Production deploy to `main` is gated behind tests passing
- Merges into `release/*` auto-bump the patch version, deploy a staging build, and open a PR to `main` if tests pass

---

## Branch Flow

```
feature  →  PR to release/*  →  merge triggers release-staging.yml:
                                  1. Bump patch in Directory.Build.props
                                  2. Commit + push version bump
                                  3. Build + unit tests + E2E on localhost
                                  4. Deploy to Azure SWA staging env
                                  5. If all green → auto-create PR to main
                                                       ↓
                                               azure-static-web-apps workflow:
                                               build + unit tests + publish + SWA deploy

PR to release/* also triggers ci.yml:
  build + unit tests + E2E Playwright
```

---

## Files to Create / Modify

| File | Action |
|---|---|
| `Directory.Build.props` | **Create** — centralized version, replaces `<Version>` in `.csproj` |
| `SBOMViewer.Blazor/SBOMViewer.Blazor.csproj` | **Modify** — remove `<Version>` (inherited from props) |
| `SBOMViewer.E2E/SBOMViewer.E2E.csproj` | Create |
| `SBOMViewer.E2E/PlaywrightSetup.cs` | Create |
| `SBOMViewer.E2E/TestBase.cs` | Create |
| `SBOMViewer.E2E/HomePageTests.cs` | Create |
| `SBOMViewer.E2E/FileUploadTests.cs` | Create |
| `SBOMViewer.sln` | Modify — add E2E project entry + config blocks |
| `.github/workflows/ci.yml` | Create |
| `.github/workflows/release-staging.yml` | **Create** — patch bump + staging deploy + auto-PR |
| `.github/workflows/azure-static-web-apps-sbomviewer.yml` | Replace |
| `.github/workflows/deploy-bicep.yml` | Replace |

---

## Step 0 — Centralized Versioning via `Directory.Build.props`

**`Directory.Build.props`** (repo root — MSBuild auto-imports this for every project)
```xml
<Project>
  <PropertyGroup>
    <!-- Single source of truth for the app version.
         release-staging.yml increments the patch on every merge to release/*. -->
    <Version>3.0.0</Version>
  </PropertyGroup>
</Project>
```

**Remove `<Version>` from `SBOMViewer.Blazor/SBOMViewer.Blazor.csproj`** — delete this block:
```xml
<PropertyGroup>
  <Version>3.0.0</Version>
  <UserSecretsId>3w7;g</UserSecretsId>
  <!-- Update this version -->
</PropertyGroup>
```
Replace with (keeping `UserSecretsId`):
```xml
<PropertyGroup>
  <UserSecretsId>3w7;g</UserSecretsId>
</PropertyGroup>
```

The `HomePageTests` footer assertion targets `"SBOM Viewer v3.0"` (major.minor only), so patch bumps do not break that test.

---

## Step 1 — E2E Test Project

**`SBOMViewer.E2E/SBOMViewer.E2E.csproj`**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="Microsoft.Playwright.NUnit" Version="1.49.0" />
    <PackageReference Include="NUnit" Version="4.1.0" />
    <PackageReference Include="NUnit3TestAdapter" Version="4.6.0" />
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
  </ItemGroup>
</Project>
```
Uses NUnit (not xUnit) because Playwright's .NET SDK ships a `PageTest` base class that integrates with NUnit lifecycle. No project reference to `SBOMViewer.Blazor` — E2E tests are black-box.

---

## Step 2 — E2E Test Infrastructure

**`SBOMViewer.E2E/TestBase.cs`**
```csharp
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace SBOMViewer.E2E;

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
```

**`SBOMViewer.E2E/PlaywrightSetup.cs`**
```csharp
using NUnit.Framework;

namespace SBOMViewer.E2E;

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
```

---

## Step 3 — `HomePageTests.cs`

Targets real DOM elements verified against `MainLayout.razor` and `UploadFile.razor`:

```csharp
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
```

---

## Step 4 — `FileUploadTests.cs`

All sample files confirmed present in `samples/`: `cyclonedx-1.6-minimal.json`, `cyclonedx-1.6-full.json`, `cyclonedx-1.7-full.json`, `spdx-2.2-minimal.json`, `cyclonedx-1.5-unsupported.json`.

`FluentInputFile` with `Mode="InputFileMode.Stream"` renders a hidden `<input type="file" accept=".json">`. `SetInputFilesAsync` bypasses visibility checks for file inputs and fires the browser-native `change` event, which Blazor picks up normally.

```csharp
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
```

---

## Step 5 — Update `SBOMViewer.sln`

Generate a new GUID: `pwsh -Command "[System.Guid]::NewGuid().ToString('B').ToUpper()"`

Add project entry after the existing `EndProject` for `SBOMViewer.Blazor.Tests`:
```
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "SBOMViewer.E2E", "SBOMViewer.E2E\SBOMViewer.E2E.csproj", "{NEW-GUID-HERE}"
EndProject
```

Add 6 config mappings in `GlobalSection(ProjectConfigurationPlatforms)` for all Debug/Release × Any CPU/x64/x86 combinations (same pattern as existing two projects).

---

## Step 6 — New `ci.yml` (PR Validation)

Triggers on PRs to `main` or `release/*`. Runs unit tests + E2E but does **not** deploy or version-bump.

```yaml
name: CI — Build, Unit Tests, and E2E Tests

on:
  pull_request:
    branches: [main, 'release/*']
    paths-ignore:
      - 'README.md'
      - 'CHANGELOG.md'
      - 'docs/**'
      - '**.txt'

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - run: dotnet restore SBOMViewer.sln

      - run: dotnet build SBOMViewer.sln --no-restore -c Release

      - name: Run unit tests
        run: dotnet test SBOMViewer.Blazor.Tests/SBOMViewer.Blazor.Tests.csproj
          --no-build -c Release
          --logger "trx;LogFileName=unit-tests.trx"
          --results-directory TestResults

      - run: dotnet publish SBOMViewer.Blazor/SBOMViewer.Blazor.csproj -c Release --output publish_output

      - uses: actions/setup-node@v4
        with: { node-version: '20' }

      - run: npm install -g serve

      - name: Start static file server (SPA mode)
        run: serve -s publish_output/wwwroot -l 5000 &

      - name: Wait for server
        run: |
          for i in $(seq 1 30); do
            curl -sf http://localhost:5000 > /dev/null 2>&1 && echo "Ready in ${i}s" && exit 0
            sleep 1
          done
          echo "Server not ready"; exit 1

      - name: Install Playwright Chromium
        run: pwsh SBOMViewer.E2E/bin/Release/net10.0/playwright.ps1 install --with-deps chromium

      - name: Run E2E tests
        env:
          BASE_URL: http://localhost:5000
          PLAYWRIGHT_BROWSERS_PATH: 0
        run: dotnet test SBOMViewer.E2E/SBOMViewer.E2E.csproj
          --no-build -c Release
          --logger "trx;LogFileName=e2e-tests.trx"
          --results-directory TestResults

      - if: always()
        uses: actions/upload-artifact@v4
        with:
          name: test-results
          path: TestResults/
          retention-days: 14

      - if: failure()
        uses: actions/upload-artifact@v4
        with:
          name: playwright-traces
          path: SBOMViewer.E2E/bin/Release/net10.0/playwright-traces/
          retention-days: 7
```

---

## Step 7 — New `release-staging.yml` (Patch Bump + Staging Deploy + Auto-PR)

Triggers on every push to any `release/*` branch (i.e., after a feature branch is merged in). Workflow:
1. Reads `<Version>` from `Directory.Build.props`, increments the patch segment
2. Commits + pushes the bumped version back to the release branch
3. Builds, runs all tests against a local server
4. Deploys a named staging environment to Azure SWA
5. If all steps pass, opens a PR from the release branch to `main`

> **Azure SWA note**: Named pre-production environments (`deployment_environment`) require the **Standard** plan. On the Free plan, remove the staging deploy step and only use this workflow for version bump + test gate + auto-PR.

```yaml
name: Release Staging — Bump Patch, Deploy, Create PR to main

on:
  push:
    branches: ['release/*']
    paths-ignore:
      - 'README.md'
      - 'CHANGELOG.md'
      - 'docs/**'
      - '**.txt'

jobs:
  staging:
    runs-on: ubuntu-latest
    permissions:
      contents: write       # push version-bump commit
      pull-requests: write  # create PR to main

    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0
          # Use a PAT if branch protection rules block the bot from pushing;
          # GITHUB_TOKEN works when "Allow GitHub Actions to create pull requests"
          # is enabled in repo Settings → Actions → General.
          token: ${{ secrets.GITHUB_TOKEN }}

      - name: Read and bump patch version
        id: version
        run: |
          CURRENT=$(grep -oP '(?<=<Version>)[^<]+' Directory.Build.props)
          MAJOR=$(echo "$CURRENT" | cut -d. -f1)
          MINOR=$(echo "$CURRENT" | cut -d. -f2)
          PATCH=$(echo "$CURRENT" | cut -d. -f3)
          NEW_PATCH=$((PATCH + 1))
          NEW_VERSION="$MAJOR.$MINOR.$NEW_PATCH"
          sed -i "s|<Version>$CURRENT</Version>|<Version>$NEW_VERSION</Version>|" Directory.Build.props
          echo "old=$CURRENT"  >> $GITHUB_OUTPUT
          echo "version=$NEW_VERSION" >> $GITHUB_OUTPUT
          echo "Bumped $CURRENT → $NEW_VERSION"

      - name: Commit and push version bump
        run: |
          git config user.name "github-actions[bot]"
          git config user.email "github-actions[bot]@users.noreply.github.com"
          git add Directory.Build.props
          git commit -m "chore: bump patch to ${{ steps.version.outputs.version }} [skip ci]"
          git push

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - run: dotnet restore SBOMViewer.sln

      - run: dotnet build SBOMViewer.sln --no-restore -c Release

      - name: Run unit tests
        run: dotnet test SBOMViewer.Blazor.Tests/SBOMViewer.Blazor.Tests.csproj
          --no-build -c Release
          --logger "trx;LogFileName=unit-tests.trx"
          --results-directory TestResults

      - run: dotnet publish SBOMViewer.Blazor/SBOMViewer.Blazor.csproj -c Release --output publish_output

      - name: Deploy staging environment to Azure SWA
        uses: Azure/static-web-apps-deploy@v1
        with:
          azure_static_web_apps_api_token: ${{ secrets.AZURE_STATIC_WEB_APPS_API_TOKEN }}
          repo_token: ${{ secrets.GITHUB_TOKEN }}
          action: "upload"
          app_location: "publish_output/wwwroot"
          api_location: ""
          output_location: ""
          skip_app_build: true
          deployment_environment: "staging"  # named pre-prod env (Standard plan only)

      - uses: actions/setup-node@v4
        with: { node-version: '20' }

      - run: npm install -g serve

      - name: Start static file server for E2E (SPA mode)
        run: serve -s publish_output/wwwroot -l 5000 &

      - name: Wait for server
        run: |
          for i in $(seq 1 30); do
            curl -sf http://localhost:5000 > /dev/null 2>&1 && echo "Ready in ${i}s" && exit 0
            sleep 1
          done
          echo "Server not ready"; exit 1

      - name: Install Playwright Chromium
        run: pwsh SBOMViewer.E2E/bin/Release/net10.0/playwright.ps1 install --with-deps chromium

      - name: Run E2E tests
        env:
          BASE_URL: http://localhost:5000
          PLAYWRIGHT_BROWSERS_PATH: 0
        run: dotnet test SBOMViewer.E2E/SBOMViewer.E2E.csproj
          --no-build -c Release
          --logger "trx;LogFileName=e2e-tests.trx"
          --results-directory TestResults

      - if: always()
        uses: actions/upload-artifact@v4
        with:
          name: test-results-staging
          path: TestResults/
          retention-days: 14

      - if: failure()
        uses: actions/upload-artifact@v4
        with:
          name: playwright-traces-staging
          path: SBOMViewer.E2E/bin/Release/net10.0/playwright-traces/
          retention-days: 7

      - name: Create PR to main
        if: success()
        env:
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
        run: |
          BRANCH="${GITHUB_REF_NAME}"
          VERSION="${{ steps.version.outputs.version }}"
          # gh pr create is idempotent — exits 0 if PR already exists
          gh pr create \
            --base main \
            --head "$BRANCH" \
            --title "Release v${VERSION}" \
            --body "$(cat <<'EOF'
          ## Release v${VERSION}

          Automated release PR created after staging CI passed on branch \`${BRANCH}\`.

          - Patch bumped: ${{ steps.version.outputs.old }} → ${VERSION}
          - All unit tests passed
          - All Playwright E2E tests passed against staging build

          **Merge this PR to deploy to production.**
          EOF
          )" \
            --label "release" 2>/dev/null || echo "PR already open — skipping creation"
```

> **`[skip ci]`** on the version-bump commit prevents `release-staging.yml` from triggering itself recursively (GitHub skips workflows whose HEAD commit message contains `[skip ci]`).

---

## Step 8 — Updated `azure-static-web-apps-sbomviewer.yml`

Fixes: .NET 9→10, action versions v3→v4, broken `paths`→`paths-ignore`, adds unit tests, adds PR preview + `close_pull_request` job, adds `skip_app_build: true`.

```yaml
name: Deploy Blazor WASM to Azure Static Web App

on:
  push:
    branches: [main]
    paths-ignore:
      - 'README.md'
      - 'CHANGELOG.md'
      - 'docs/**'
      - '**.txt'
  pull_request:
    types: [opened, synchronize, reopened, closed]
    branches: [main]
  workflow_dispatch:

jobs:
  build_and_deploy:
    runs-on: ubuntu-latest
    if: >
      github.event_name == 'push' ||
      github.event_name == 'workflow_dispatch' ||
      (github.event_name == 'pull_request' && github.event.action != 'closed')
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet restore SBOMViewer.sln
      - run: dotnet build SBOMViewer.sln --no-restore -c Release
      - name: Run unit tests
        run: dotnet test SBOMViewer.Blazor.Tests/SBOMViewer.Blazor.Tests.csproj
          --no-build -c Release
      - run: dotnet publish SBOMViewer.Blazor/SBOMViewer.Blazor.csproj -c Release --output publish_output
      - uses: Azure/static-web-apps-deploy@v1
        with:
          azure_static_web_apps_api_token: ${{ secrets.AZURE_STATIC_WEB_APPS_API_TOKEN }}
          repo_token: ${{ secrets.GITHUB_TOKEN }}
          action: "upload"
          app_location: "publish_output/wwwroot"
          api_location: ""
          output_location: ""
          skip_app_build: true   # Already published above

  close_pull_request:
    runs-on: ubuntu-latest
    if: github.event_name == 'pull_request' && github.event.action == 'closed'
    steps:
      - uses: Azure/static-web-apps-deploy@v1
        with:
          azure_static_web_apps_api_token: ${{ secrets.AZURE_STATIC_WEB_APPS_API_TOKEN }}
          repo_token: ${{ secrets.GITHUB_TOKEN }}
          action: "close"
```

---

## Step 9 — Fixed `deploy-bicep.yml`

Fixes: watched path `bicep/main.bicep` → `Infra/main.bicep`, template path fixed, add `branches: [main]` guard, upgrade action versions.

```yaml
name: Deploy Azure Static Web App Infrastructure

on:
  push:
    branches: [main]
    paths:
      - 'Infra/main.bicep'
  workflow_dispatch:

jobs:
  deploy-infra:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: azure/login@v2
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}
      - name: Deploy Bicep
        run: |
          az deployment group create \
            --resource-group MyResourceGroup \
            --template-file Infra/main.bicep
```

---

## Verification Steps

### Local end-to-end test before pushing

```bash
# 1. Build everything
dotnet restore SBOMViewer.sln
dotnet build SBOMViewer.sln -c Release

# 2. Run unit tests
dotnet test SBOMViewer.Blazor.Tests -c Release --no-build

# 3. Publish + serve
dotnet publish SBOMViewer.Blazor -c Release --output publish_output
npm install -g serve
serve -s publish_output/wwwroot -l 5000 &

# 4. Install Playwright Chromium
pwsh SBOMViewer.E2E/bin/Release/net10.0/playwright.ps1 install chromium

# 5. Run E2E tests
dotnet test SBOMViewer.E2E -c Release --no-build -e BASE_URL=http://localhost:5000
```

### GitHub CI verification
1. Merge a feature branch into `release/*` — confirm **release-staging** workflow runs, bumps the patch in `Directory.Build.props`, deploys staging, and opens a PR to `main`
2. Push a branch based on `release/*`, open PR to `release/*` — confirm **CI** check runs both unit tests and E2E tests
3. After the auto-PR to `main` is merged — confirm **Deploy** workflow runs and promotes to production
4. Open a PR directly to `main` — confirm SWA preview URL appears in the PR comments
5. Close/merge that PR — confirm the preview environment is torn down (`close_pull_request` job)
