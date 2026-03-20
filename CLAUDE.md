# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SBOM Viewer is a Blazor WebAssembly (WASM) client-side app that dynamically parses and displays SPDX 2.2, CycloneDX 1.6, and CycloneDX 1.7 SBOM JSON files in the browser. The UI is generated dynamically from the uploaded JSON structure — no static model classes or hardcoded viewers. All processing happens client-side — there is no backend API. Deployed as an Azure Static Web App at sbomviewer.com.

## Build & Run Commands

```bash
dotnet restore                                        # Restore NuGet packages
dotnet build SBOMViewer.slnx                          # Build the solution
dotnet run --project src/SBOMViewer.Blazor            # Run locally (https://localhost:5157)
dotnet publish src/SBOMViewer.Blazor -c Release --output publish_output  # Publish for deployment
```

```bash
dotnet test                                                # Run all tests
dotnet test --filter "FullyQualifiedName~SchemaService"    # Run single test class
```

### E2E Tests (Playwright)

```bash
dotnet build SBOMViewer.slnx -c Release
dotnet publish src/SBOMViewer.Blazor -c Release --output publish_output
npx serve -s publish_output/wwwroot -l 5000 &
pwsh tests/SBOMViewer.E2E.Tests/bin/Release/net10.0/playwright.ps1 install chromium  # first time only
dotnet test tests/SBOMViewer.E2E.Tests -c Release --no-build -e BASE_URL=http://localhost:5000
```

## Project Structure

```
SBOMViewer.slnx
├── Directory.Build.props               # Centralized version (read/written by release-staging.yml)
├── CLAUDE.md
├── AGENTS.md
├── Infra/
│   └── main.bicep                      # Azure Static Web App infrastructure
├── .github/workflows/
│   ├── ci.yml                          # PR validation: build + unit tests + E2E
│   ├── release-staging.yml             # Push to release/*: bump patch, deploy staging, create PR to main
│   ├── azure-static-web-apps-sbomviewer.yml  # Push to main: deploy + create GitHub release
│   └── deploy-bicep.yml                # Infrastructure deployment
├── docs/                               # Design docs and plans
├── samples/                            # Sample SBOM JSON files for testing
├── src/
│   └── SBOMViewer.Blazor/
│       ├── Program.cs                  # Entry point — DI registration (SbomState, SchemaService, FluentUI)
│       ├── App.razor                   # Blazor router
│       ├── _Imports.razor              # Global usings, Icons alias, System.Text.Json
│       ├── Layout/
│       │   └── MainLayout.razor        # App shell: header, toolbar, body, footer, theme toggle
│       ├── Pages/
│       │   └── Home.razor              # Main page — renders DynamicSbomViewer based on SbomState
│       ├── Components/
│       │   ├── UploadFile.razor        # File upload, format detection, validation, JSON parsing
│       │   ├── DynamicSbomViewer.razor # Top-level viewer — FluentCard + FluentAccordion sections
│       │   ├── DynamicSection.razor    # Array/object renderer — search, scroll, details/summary
│       │   └── DynamicObject.razor     # Recursive object renderer — key-value, badges, nested
│       ├── Services/
│       │   ├── SbomState.cs            # Singleton state: JsonDocument, SchemaNode, format, filename
│       │   ├── SbomFormatDetector.cs   # Format detection + lightweight required-field validation
│       │   └── SchemaService.cs        # Builds SchemaNode tree from uploaded JSON, applies render hints
│       ├── Models/
│       │   ├── SbomFormat.cs           # Enum: CycloneDX_1_6, CycloneDX_1_7, SPDX_2_2
│       │   └── SchemaNode.cs           # SchemaNode, SchemaNodeType, RenderHint
│       └── wwwroot/
│           ├── index.html              # Host page (SEO meta, Google Analytics, Fluent theme loader)
│           ├── robots.txt              # Search engine crawl rules
│           ├── sitemap.xml             # Sitemap for SEO
│           └── css/app.css             # App styles
└── tests/
    ├── SBOMViewer.Blazor.Tests/
    │   ├── TestData/
    │   │   └── TestJson.cs             # Inline JSON test data for SPDX and CycloneDX
    │   └── Services/
    │       ├── SbomStateTests.cs       # SbomState event and persistence tests
    │       ├── SbomFormatDetectorTests.cs  # Format detection + lightweight validation tests
    │       └── SchemaServiceTests.cs   # SchemaNode building and render hint tests
    └── SBOMViewer.E2E.Tests/
        ├── PlaywrightSetup.cs          # One-time Chromium install ([SetUpFixture])
        ├── TestBase.cs                 # PageTest base — reads BASE_URL env var, waits for Blazor bootstrap
        ├── HomePageTests.cs            # Smoke tests: title, header, upload button, badges, card, theme, footer
        └── FileUploadTests.cs          # Upload tests: CycloneDX 1.6/1.7, SPDX 2.2, unsupported, invalid JSON, search
```

## Environment

- This is a Windows development environment. Always use forward slashes or properly escaped paths in shell commands. Never use raw backslashes in Bash commands.
- When running PowerShell commands, prefer `pwsh -Command` or use PowerShell-native syntax.

## Architecture

**Solution** (`SBOMViewer.slnx`) with one app project (`src/SBOMViewer.Blazor`) and two test projects under `tests/`, all targeting .NET 10.0.

### Versioning

The app version lives in `Directory.Build.props` at the repo root and is inherited by all projects. `release-staging.yml` increments the patch segment on every merge to a `release/*` branch and commits it back with `[skip ci]`.

### Data Flow

1. **UploadFile** component — user uploads a JSON file (max 20MB, `.json` only)
2. **SbomFormatDetector** — detects format (CycloneDX/SPDX) and version by peeking at `bomFormat`/`spdxVersion`
3. **JsonDocument.Parse** — parses JSON into a read-only DOM tree
4. **SbomFormatDetector.Validate** — lightweight validation of required fields per format
5. **SchemaService.BuildFromJson** — builds a `SchemaNode` tree from the JSON structure, applies render hints
6. **SbomState** — singleton holding `JsonDocument`, `SchemaNode`, detected format, and filename; notifies subscribers via `OnChange`
7. **DynamicSbomViewer** → **DynamicSection** → **DynamicObject** — recursive components that walk `JsonElement` + `SchemaNode` to render Fluent UI

### Dynamic Rendering Pipeline

The UI is generated dynamically from the uploaded JSON — no static C# model classes or hardcoded viewer templates:

- **DynamicSbomViewer** — groups scalar properties into "General Information" accordion, creates accordion sections per complex (object/array) property, maps known property names to icons
- **DynamicSection** — renders arrays with `FluentSearch` filtering (>5 items), scrollable container, `<details>/<summary>` per item with indented content and left border
- **DynamicObject** — renders object properties recursively: key-value pairs for scalars, `FluentBadge` for tag-like string arrays, indented nested objects with border, delegates to `DynamicSection` for object arrays

### Key Models

- `SchemaNode` — lightweight tree built from JSON data: `PropertyName`, `Title`, `NodeType`, `Properties` dict, `PropertyOrder`, `ItemSchema` (for arrays), `RenderHint`
- `SchemaNodeType` enum — String, Integer, Number, Boolean, Array, Object, Unknown
- `RenderHint` enum — Auto, AccordionSection, SearchableList, BadgeList, KeyValueGroup
- `SbomFormat` enum — CycloneDX_1_6, CycloneDX_1_7, SPDX_2_2

### Lightweight Validation

`SbomFormatDetector.Validate(JsonElement, SbomFormat)` checks required fields per format:
- **CycloneDX**: `bomFormat`, `specVersion`, `metadata` (object), `components` (array)
- **SPDX**: `spdxVersion`, `name`, `SPDXID`, `dataLicense`, `documentNamespace`, `creationInfo` (object)

### UI Framework

Uses **Microsoft.FluentUI.AspNetCore.Components** (v4.13.2) for all UI components. The `Icons` alias is set in `_Imports.razor`:
```razor
@using Icons = Microsoft.FluentUI.AspNetCore.Components.Icons
```

### CI/CD

| Workflow | Trigger | What it does |
|----------|---------|--------------|
| `ci.yml` | PR to `main` or `release/*` | Build, unit tests, Playwright E2E |
| `release-staging.yml` | Push to `release/*` | Bump patch in `Directory.Build.props`, deploy staging env, run all tests, open PR to `main` |
| `azure-static-web-apps-sbomviewer.yml` | Push to `main` | Build, unit tests, deploy to production SWA, create GitHub release |
| `deploy-bicep.yml` | Change to `Infra/main.bicep` | Deploy Azure infrastructure |

## Coding Conventions

- **JSON handling**: Use `System.Text.Json.JsonDocument` for parsing uploaded files. No typed deserialization, no Newtonsoft.
- **Schema inference**: `SchemaService.BuildFromJson()` infers types from JSON data. No external schema files loaded at runtime.
- **Validation**: `SbomFormatDetector.Validate()` for lightweight required-field checks. Returns `null` if valid or an error message string.
- **State management**: Shared data flows through the `SbomState` singleton holding `JsonDocument` + `SchemaNode`. Components subscribe to `OnChange` and call `StateHasChanged()` to re-render. `SbomState.Clear()` disposes `JsonDocument`.
- **UI components**: Use Fluent UI (`FluentCard`, `FluentAccordion`, `FluentSearch`, `FluentBadge`, etc.). Reference icons via the `Icons` alias from `_Imports.razor`.
- **Dynamic rendering**: All three viewer components (`DynamicSbomViewer`, `DynamicSection`, `DynamicObject`) work with `JsonElement` + `SchemaNode` — no format-specific logic.
- **File uploads**: Max 20MB, `.json` only. Auto-detects format from JSON content.
- **E2E tests**: Use NUnit + Playwright (`PageTest` base class). Tests are black-box — no project reference to `SBOMViewer.Blazor`. Target URL is controlled via `BASE_URL` env var (default `http://localhost:5000`).

## Adding a New SBOM Format

1. Add a value to the `SbomFormat` enum in `src/SBOMViewer.Blazor/Models/SbomFormat.cs`
2. Add detection logic in `SbomFormatDetector.DetectWithDetails()` (peek at a distinguishing JSON property)
3. Add validation logic in `SbomFormatDetector.Validate()` (check required fields)
4. Update `SbomFormatDetector.SupportedVersions` array
5. No new models, parsers, or viewer components needed — the dynamic rendering pipeline handles any JSON structure automatically

## Branch Strategy

- `main` — production branch, triggers deployment and GitHub release creation
- `release/*` — release branches; merges into these trigger patch version bump + staging deploy + auto-PR to `main`
