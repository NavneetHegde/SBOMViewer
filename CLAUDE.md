# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SBOM Viewer is a Blazor WebAssembly (WASM) client-side app that dynamically parses and displays SPDX 2.2, CycloneDX 1.6, and CycloneDX 1.7 SBOM JSON files in the browser. The UI is generated dynamically from the uploaded JSON structure — no static model classes or hardcoded viewers. All processing happens client-side — there is no backend API. Deployed as an Azure Static Web App at sbomviewer.com.

## Build & Run Commands

```bash
dotnet restore                          # Restore NuGet packages
dotnet build                            # Build the solution
dotnet run --project SBOMViewer.Blazor   # Run locally (https://localhost:5157)
dotnet publish -c Release --output publish_output  # Publish for deployment
```

```bash
dotnet test                                                # Run all tests
dotnet test --filter "FullyQualifiedName~SchemaService"    # Run single test class
```

## Project Structure

```
SBOMViewer.sln
├── CLAUDE.md
├── AGENTS.md
├── Infra/
│   └── main.bicep                          # Azure Static Web App infrastructure
├── .github/workflows/
│   ├── azure-static-web-apps-sbomviewer.yml  # Build + deploy on push to main
│   └── deploy-bicep.yml                      # Infrastructure deployment
├── docs/
│   └── dynamic-ui-plan.md                  # Dynamic UI design document
├── SBOMViewer.Blazor.Tests/
│   ├── TestData/
│   │   └── TestJson.cs                     # Inline JSON test data for SPDX and CycloneDX
│   └── Services/
│       ├── SbomStateTests.cs               # SbomState event and persistence tests
│       ├── SbomFormatDetectorTests.cs      # Format detection + lightweight validation tests
│       └── SchemaServiceTests.cs           # SchemaNode building and render hint tests
└── SBOMViewer.Blazor/
    ├── Program.cs                          # Entry point — DI registration (SbomState, SchemaService, FluentUI)
    ├── App.razor                           # Blazor router
    ├── _Imports.razor                      # Global usings, Icons alias, System.Text.Json
    ├── Layout/
    │   └── MainLayout.razor                # App shell: header, toolbar, body, footer, theme toggle
    ├── Pages/
    │   └── Home.razor                      # Main page — renders DynamicSbomViewer based on SbomState
    ├── Components/
    │   ├── UploadFile.razor                # File upload, format detection, validation, JSON parsing
    │   ├── DynamicSbomViewer.razor         # Top-level viewer — FluentCard + FluentAccordion sections
    │   ├── DynamicSection.razor            # Array/object renderer — search, scroll, details/summary
    │   └── DynamicObject.razor             # Recursive object renderer — key-value, badges, nested
    ├── Services/
    │   ├── SbomState.cs                    # Singleton state: JsonDocument, SchemaNode, format, filename
    │   ├── SbomFormatDetector.cs           # Format detection + lightweight required-field validation
    │   └── SchemaService.cs                # Builds SchemaNode tree from uploaded JSON, applies render hints
    ├── Models/
    │   ├── SbomFormat.cs                   # Enum: CycloneDX_1_6, CycloneDX_1_7, SPDX_2_2
    │   └── SchemaNode.cs                   # SchemaNode, SchemaNodeType, RenderHint
    └── wwwroot/
        ├── index.html                      # Host page (SEO meta, Google Analytics, Fluent theme loader)
        └── css/app.css                     # App styles
```

## Environment

- This is a Windows development environment. Always use forward slashes or properly escaped paths in shell commands. Never use raw backslashes in Bash commands.
- When running PowerShell commands, prefer `pwsh -Command` or use PowerShell-native syntax.

## Architecture

**Single-project solution** (`SBOMViewer.Blazor`) targeting .NET 10.0 with Fluent UI components.

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

- `.github/workflows/azure-static-web-apps-sbomviewer.yml` — builds and deploys to Azure Static Web Apps on push to `main`
- `.github/workflows/deploy-bicep.yml` — deploys infrastructure changes

## Coding Conventions

- **JSON handling**: Use `System.Text.Json.JsonDocument` for parsing uploaded files. No typed deserialization, no Newtonsoft.
- **Schema inference**: `SchemaService.BuildFromJson()` infers types from JSON data. No external schema files loaded at runtime.
- **Validation**: `SbomFormatDetector.Validate()` for lightweight required-field checks. Returns `null` if valid or an error message string.
- **State management**: Shared data flows through the `SbomState` singleton holding `JsonDocument` + `SchemaNode`. Components subscribe to `OnChange` and call `StateHasChanged()` to re-render. `SbomState.Clear()` disposes `JsonDocument`.
- **UI components**: Use Fluent UI (`FluentCard`, `FluentAccordion`, `FluentSearch`, `FluentBadge`, etc.). Reference icons via the `Icons` alias from `_Imports.razor`.
- **Dynamic rendering**: All three viewer components (`DynamicSbomViewer`, `DynamicSection`, `DynamicObject`) work with `JsonElement` + `SchemaNode` — no format-specific logic.
- **File uploads**: Max 20MB, `.json` only. Auto-detects format from JSON content.

## Adding a New SBOM Format

1. Add a value to the `SbomFormat` enum in `Models/SbomFormat.cs`
2. Add detection logic in `SbomFormatDetector.DetectWithDetails()` (peek at a distinguishing JSON property)
3. Add validation logic in `SbomFormatDetector.Validate()` (check required fields)
4. Update `SbomFormatDetector.SupportedVersions` array
5. No new models, parsers, or viewer components needed — the dynamic rendering pipeline handles any JSON structure automatically

## Branch Strategy

- `main` — production branch, triggers deployment
- `release/2.0` — release branch used as PR base
- `release/3.0` — development branch for dynamic UI + CycloneDX 1.7 + auto-detect
