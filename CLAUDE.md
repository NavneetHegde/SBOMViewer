# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SBOM Viewer is a Blazor WebAssembly (WASM) client-side app that parses and displays SPDX 2.2 and CycloneDX 1.6 SBOM JSON files in the browser. All processing happens client-side — there is no backend API. Deployed as an Azure Static Web App at sbomviewer.com.

## Build & Run Commands

```bash
dotnet restore                          # Restore NuGet packages
dotnet build                            # Build the solution
dotnet run --project SBOMViewer.Blazor   # Run locally (https://localhost:5157)
dotnet publish -c Release --output publish_output  # Publish for deployment
```

```bash
dotnet test                                            # Run all tests
dotnet test --filter "FullyQualifiedName~SpdxParser"   # Run single test class
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
├── SBOMViewer.Blazor.Tests/
│   ├── TestData/
│   │   └── TestJson.cs                     # Inline JSON test data for SPDX and CycloneDX
│   └── Services/
│       ├── SbomStateTests.cs               # SbomState event and persistence tests
│       ├── SpdxParserTests.cs              # SPDX parser unit tests
│       └── CycloneDXParserTests.cs         # CycloneDX parser unit tests
└── SBOMViewer.Blazor/
    ├── Program.cs                          # Entry point — DI registration (SbomState, FluentUI)
    ├── App.razor                           # Blazor router
    ├── _Imports.razor                      # Global usings and Icons alias
    ├── Layout/
    │   └── MainLayout.razor                # App shell: header, toolbar, body, footer, theme toggle
    ├── Pages/
    │   └── Home.razor                      # Main page — renders viewer based on SbomState
    ├── Components/
    │   ├── UploadFile.razor                # Format selector + JSON file upload
    │   ├── SpdxViewer.razor                # SPDX document renderer (accordion + search)
    │   └── CycloneDXViewer.razor           # CycloneDX document renderer (accordion + search)
    ├── Services/
    │   ├── SbomState.cs                    # Singleton state container (pub/sub via OnChange)
    │   ├── SpdxParser.cs                   # SPDX JSON → SpdxDocument
    │   └── CycloneDXParser.cs              # CycloneDX JSON → CycloneDXDocument
    ├── Models/
    │   ├── SbomFormat.cs                   # Enum: CycloneDX_1_6, SPDX_2_2
    │   ├── SpdxDocument.cs                 # SPDX model hierarchy (SpdxDocument, SpdxPackage, SpdxFile, etc.)
    │   └── CycloneDXDocument.cs            # CycloneDX model hierarchy (CycloneDXDocument, Component, Dependency, etc.)
    └── wwwroot/
        ├── index.html                      # Host page (SEO meta, Google Analytics, Fluent theme loader)
        └── css/app.css                     # App styles
```

## Environment

- This is a Windows development environment. Always use forward slashes or properly escaped paths in shell commands. Never use raw backslashes in Bash commands.
- When running PowerShell commands, prefer `pwsh -Command` or use PowerShell-native syntax.

## Architecture

**Single-project solution** (`SBOMViewer.Blazor`) targeting .NET 9.0 with Fluent UI components.

### Data Flow

1. **UploadFile** component — user selects SBOM format (SPDX or CycloneDX) and uploads a JSON file
2. **Parser services** (`SpdxParser`, `CycloneDXParser`) — static classes that deserialize JSON into model objects using `System.Text.Json`
3. **SbomState** — singleton service acting as shared state between components; holds parsed document data and notifies subscribers via `OnChange` event
4. **Viewer components** (`SpdxViewer`, `CycloneDXViewer`) — render the parsed SBOM data using Fluent UI accordions with search/filter support
5. **Home page** — subscribes to `SbomState.OnChange` and conditionally renders the appropriate viewer

### Key Directories

- `Models/Spdx/` — SPDX document model classes (in `SpdxDocument.cs`)
- `Models/CycloneDX/` — CycloneDX document model classes (in `CycloneDXDocument.cs`)
- `Models/SbomFormat.cs` — enum defining supported formats
- `Services/` — parsers and `SbomState`
- `Components/` — `UploadFile`, `SpdxViewer`, `CycloneDXViewer`
- `Layout/MainLayout.razor` — app shell with header, toolbar, body, footer; includes theme toggling and a duplicate file-upload handler (legacy from v1)
- `Infra/main.bicep` — Azure Static Web App infrastructure definition

### UI Framework

Uses **Microsoft.FluentUI.AspNetCore.Components** (v4.12.0) for all UI components. The `Icons` alias is set in `_Imports.razor`:
```razor
@using Icons = Microsoft.FluentUI.AspNetCore.Components.Icons
```

### CI/CD

- `.github/workflows/azure-static-web-apps-sbomviewer.yml` — builds and deploys to Azure Static Web Apps on push to `main`
- `.github/workflows/deploy-bicep.yml` — deploys infrastructure changes

## Coding Conventions

- **JSON models**: Use `System.Text.Json` with `[JsonPropertyName]` attributes. No Newtonsoft.
- **Parsers**: Static methods returning `Task<T?>`. Return `null` on parse failure — don't throw.
- **State management**: Shared data flows through the `SbomState` singleton. Components subscribe to `OnChange` and call `StateHasChanged()` to re-render.
- **UI components**: Use Fluent UI (`FluentCard`, `FluentAccordion`, `FluentSearch`, etc.). Reference icons via the `Icons` alias from `_Imports.razor`.
- **File uploads**: Max 20MB, `.json` only. Uploading one SBOM format clears the other.

## Adding a New SBOM Format

1. Add a value to the `SbomFormat` enum in `Models/SbomFormat.cs`
2. Create model classes in a new file under `Models/`
3. Create a parser in `Services/` following the existing static pattern
4. Create a viewer component in `Components/`
5. Update `UploadFile.razor` to handle the new format and clear other formats
6. Update `Home.razor` to conditionally render the new viewer

## Branch Strategy

- `main` — production branch, triggers deployment
- `release/2.0` — release branch used as PR base
