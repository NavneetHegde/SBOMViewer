# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SBOM Viewer is a Blazor WebAssembly (WASM) client-side app that dynamically parses and displays SPDX 2.2/2.3/3.0.1 and CycloneDX 1.5/1.6/1.7 SBOM JSON files in the browser. The UI is generated dynamically from the uploaded JSON structure — no static model classes or hardcoded viewers. All processing happens client-side — there is no backend API. Deployed as an Azure Static Web App at sbomviewer.com.

The app includes a **vulnerability scanning** feature that checks all SBOM components against the [OSV.dev](https://osv.dev) (Open Source Vulnerabilities) database. Scanning is user-initiated and runs entirely client-side via the OSV.dev batch and detail APIs.

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
│       │   ├── Home.razor              # Main page — nav actions (export/compare/new) + DynamicSbomViewer
│       │   └── Compare.razor           # SBOM diff page (/compare) — two-slot upload + change tables
│       ├── Components/
│       │   ├── UploadFile.razor        # File upload screen — delegates parsing to SbomLoader
│       │   ├── CompareSlot.razor       # One baseline/current dropzone slot used by Compare.razor
│       │   ├── DynamicSbomViewer.razor # Top-level viewer — FluentCard + FluentAccordion sections + vuln scan
│       │   ├── DynamicSection.razor    # Array/object renderer — search, scroll, details/summary
│       │   └── DynamicObject.razor     # Recursive object renderer — key-value, badges, nested
│       ├── Services/
│       │   ├── SbomState.cs            # Singleton state: JsonDocument, SchemaNode, format, filename
│       │   ├── SbomLoader.cs           # Shared detect → validate → build-schema pipeline (+ saves recents)
│       │   ├── RecentSbomStore.cs      # IndexedDB-backed last-2-uploads store (best-effort)
│       │   ├── SbomCompareState.cs     # Singleton state: baseline + current documents, computed diff
│       │   ├── SbomDiffService.cs      # Component-level diff over ComponentRow lists
│       │   ├── SbomFormatDetector.cs   # Format detection + lightweight required-field validation
│       │   ├── SchemaService.cs        # Builds SchemaNode tree from uploaded JSON, applies render hints
│       │   ├── ChatState.cs            # Singleton state: vuln results, scan progress, warnings, chat messages
│       │   ├── PackageExtractor.cs     # Extracts packages from SBOM JSON (CycloneDX + SPDX 2.x/3.0)
│       │   ├── ComponentRowExtractor.cs # Extracts ComponentRow list (name, version, license, risk) for the Components/Compliance tabs
│       │   ├── LicenseClassifier.cs    # Classifies a license identifier into a LicenseRisk category
│       │   └── VulnerabilityService.cs # OSV.dev two-phase scan: batch query + per-vuln detail fetch
│       ├── Models/
│       │   ├── SbomFormat.cs           # Enum: CycloneDX_1_5/1_6/1_7, SPDX_2_2/2_3/3_0
│       │   ├── SbomDiff.cs             # SbomDiff, ComponentChange, DiffChangeKind
│       │   ├── RecentSbom.cs           # Stored-file metadata + size/age labels
│       │   ├── SbomFormatLabel.cs      # SbomFormat → display string
│       │   ├── SchemaNode.cs           # SchemaNode, SchemaNodeType, RenderHint
│       │   ├── PackageInfo.cs          # Package name, version, ecosystem, purl
│       │   ├── ComponentRow.cs         # Name, Version, Type, License, Purl, Risk — row shown in Components/Compliance tabs
│       │   ├── LicenseRisk.cs          # Enum: Permissive, WeakCopyleft, StrongCopyleft, Proprietary, Unknown
│       │   ├── VulnerabilityResult.cs  # CVE entries per package
│       │   └── ChatMessage.cs          # Chat message (role, content, timestamp)
│       └── wwwroot/
│           ├── index.html              # Host page (SEO meta, Google Analytics, Fluent theme loader)
│           ├── robots.txt              # Search engine crawl rules
│           ├── sitemap.xml             # Sitemap for SEO
│           ├── staticwebapp.config.json # Azure SWA SPA fallback so client routes (/compare) resolve
│           ├── js/sbom-recent.js       # IndexedDB helpers for the recent-files store
│           └── css/app.css             # App styles
└── tests/
    ├── SBOMViewer.Blazor.Tests/
    │   ├── TestData/
    │   │   └── TestJson.cs             # Inline JSON test data for SPDX and CycloneDX
    │   └── Services/
    │       ├── SbomStateTests.cs       # SbomState event and persistence tests
    │       ├── SbomFormatDetectorTests.cs  # Format detection + lightweight validation tests
    │       ├── SchemaServiceTests.cs   # SchemaNode building and render hint tests
    │       ├── ChatStateTests.cs       # ChatState event, clear, and vuln state tests
    │       ├── PackageExtractorTests.cs    # Package extraction from CycloneDX (1.5/1.6/1.7) + SPDX (2.2/2.3/3.0)
    │       ├── ComponentRowExtractorTests.cs # ComponentRow extraction + license risk classification per format
    │       ├── SbomLoaderTests.cs       # Shared load pipeline: success, bad format, failed validation
    │       ├── SbomDiffServiceTests.cs  # purl identity, add/remove/version/license, cross-format, edge cases
    │       ├── SbomCompareStateTests.cs # Two-slot state, diff recomputation, clear/replace
    │       ├── LicenseClassifierTests.cs   # License identifier → LicenseRisk classification tests
    │       └── VulnerabilityServiceTests.cs # OSV.dev API client tests
    └── SBOMViewer.E2E.Tests/
        ├── PlaywrightSetup.cs          # One-time Chromium install ([SetUpFixture])
        ├── TestBase.cs                 # PageTest base — reads BASE_URL env var, waits for Blazor bootstrap
        ├── HomePageTests.cs            # Smoke tests: title, header, upload button, badges, card, theme, footer
        ├── FileUploadTests.cs          # Upload tests: CycloneDX 1.5/1.6/1.7, SPDX 2.2/2.3/3.0.1, unsupported, invalid JSON, search
        ├── CompareTests.cs             # /compare route, two-slot upload, identical files, cross-format diff, swap, reset
        └── RecentSbomTests.cs          # IndexedDB persistence, 2-entry cap, dedup, clear, slot shortcut
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
2. **SbomFormatDetector** — detects format (CycloneDX/SPDX) and version by peeking at `bomFormat`/`spdxVersion`, or at the `CreationInfo` element inside `@graph` for SPDX 3.0's JSON-LD shape
3. **JsonDocument.Parse** — parses JSON into a read-only DOM tree
4. **SbomFormatDetector.Validate** — lightweight validation of required fields per format
5. **SchemaService.BuildFromJson** — builds a `SchemaNode` tree from the JSON structure, applies render hints
6. **SbomState** — singleton holding `JsonDocument`, `SchemaNode`, detected format, and filename; notifies subscribers via `OnChange`
7. **DynamicSbomViewer** → **DynamicSection** → **DynamicObject** — recursive components that walk `JsonElement` + `SchemaNode` to render Fluent UI
8. **Vulnerability scan** (user-initiated) — **PackageExtractor** extracts packages → **VulnerabilityService** queries OSV.dev (batch to collect IDs, then per-vuln detail fetch), then enriches with EPSS + CISA KEV → **ChatState** stores results → `RenderVulnsTab` in **DynamicSbomViewer** renders the sortable, filterable list

### Dynamic Rendering Pipeline

The UI is generated dynamically from the uploaded JSON — no static C# model classes or hardcoded viewer templates:

- **DynamicSbomViewer** — groups scalar properties into "General Information" accordion, creates accordion sections per complex (object/array) property, maps known property names to icons
- **DynamicSection** — renders arrays with `FluentSearch` filtering (>5 items), scrollable container, `<details>/<summary>` per item with indented content and left border
- **DynamicObject** — renders object properties recursively: key-value pairs for scalars, `FluentBadge` for tag-like string arrays, indented nested objects with border, delegates to `DynamicSection` for object arrays

### Vulnerability Scanning

User-initiated vulnerability scanning via the [OSV.dev](https://osv.dev) API — all processing is client-side:

- **PackageExtractor** — extracts `PackageInfo` (name, version, ecosystem, purl) from the SBOM JSON. CycloneDX uses the `components` array + purl; SPDX 2.x uses the `packages` array + `externalRefs`; SPDX 3.0 uses the `@graph` array, filtering `software_Package` elements
- **VulnerabilityService** — three-phase scan:
  - **Phase 1**: batches packages in groups of 100, POSTs to `https://api.osv.dev/v1/querybatch` to collect vuln IDs per package
  - **Phase 2**: fetches full details for each unique vuln ID via `GET https://api.osv.dev/v1/vulns/{id}` (up to 5 concurrent requests), parses complete severity, CVSS score, summary, fix version, and the `CVE-*` entries from `aliases`
  - **Phase 3**: exploitability enrichment — see [Exploitability enrichment](#exploitability-enrichment-epss--cisa-kev)
  - Hard caps prevent abuse: max **500 packages** scanned, max **200 unique vuln detail fetches** per scan. Exceeding either cap fires `onWarning` and surfaces a warning banner in the UI
  - Severity is resolved in priority order: `database_specific.severity` → `database_specific.cvss.score` → `ecosystem_specific.severity` → `severity[].score`. `"MODERATE"` (GitHub Advisory DB) is normalised to `"MEDIUM"`
- **ChatState** — singleton holding scan results, progress, warnings, and error state. `ClearVulnerabilities()` is called on new file upload to reset stale data. `ScanWarnings` accumulates non-fatal cap/truncation notices
- **DynamicSbomViewer** — Vulnerabilities accordion section with "Scan for Vulnerabilities" button, progress overlay (tracks vuln detail fetches), count badge, warning banners, and hover info popover. `RenderVulnsTab` is the live vulnerability list: search, severity filters, KEV-only and has-fix toggles, and a sort control (CVSS / EPSS / package name)

### Exploitability enrichment (EPSS + CISA KEV)

CVSS measures potential impact, not likelihood. Phase 3 adds two free public datasets so the list can be triaged by what is actually being exploited. Both are fetched **client-side**, like everything else.

- **Join key is the CVE ID.** OSV IDs are frequently `GHSA-*` / `PYSEC-*` / `GO-*`, so `ParseVulnerability` retains the `CVE-*` entries from the OSV record's `aliases` array. A vuln with no CVE alias is simply not enrichable and is skipped
- **EPSS** — `GET https://api.first.org/data/v1/epss?cve=…`, batched at 100 CVEs per request. The `epss` field is a **string**, parsed with `InvariantCulture`. `EpssScore` is `double?` — null (not scored) is deliberately distinct from `0.0` (scored, negligible)
- **KEV** — fetched from **`raw.githubusercontent.com/cisagov/kev-data`, not `cisa.gov`**. The canonical CISA feed returns no `Access-Control-Allow-Origin` header, so a browser fetch is blocked; the GitHub mirror is the same first-party `cisagov` org and sends `ACAO: *`. Cached in a field for the lifetime of the service, so a re-scan does not refetch it
- **Enrichment is strictly non-fatal and independently wrapped.** EPSS and KEV each have their own try/catch, so one failing does not cost the other, and neither can fail the scan. A failure leaves results unenriched and adds a `ScanWarnings` note — the OSV results still render

Enrichment is applied to the phase-2 `vulnDetails` dictionary *before* it is projected back onto packages, so each vuln is enriched once and the result fans out to every affected package.

### SBOM Diff (`/compare`)

Compares two SBOM documents entirely in the browser — no login, no database, no upload. Both `JsonDocument`s are held in memory for the lifetime of the tab; a page refresh discards them.

- **SbomLoader** — the shared `detect → validate → BuildFromJson` pipeline (20MB cap, `.json` only). Both `UploadFile.razor` and `CompareSlot.razor` call it, so size limits, format support and error wording cannot drift apart. Returns an `SbomLoadResult`; on failure the parsed `JsonDocument` is disposed rather than leaked
- **SbomCompareState** — singleton holding the baseline and current slots plus the computed `SbomDiff`. `Diff` is null until both slots are filled, and is recomputed whenever either slot changes. Replacing or clearing a slot disposes the previous `JsonDocument`
- **SbomDiffService** — diffs the `ComponentRow` lists produced by `ComponentRowExtractor`, not raw JSON, so a baseline in one format can be compared against a current document in another (e.g. SPDX 2.2 vs CycloneDX 1.6). Matching runs in two passes:
  - **Pass 1** on version-stripped purl. `PurlIdentity` strips the subpath, qualifiers and version segment — **a purl embeds the version, so matching on the raw purl would report every version bump as an add plus a remove.** Scoped npm names encode `@` as `%40`, so the *last* `@` is the version separator
  - **Pass 2** on component name (case-insensitive), for whatever pass 1 left unmatched. This is what makes cross-format comparison work when one side carries no purl
  - Duplicate identities within a document are paired in document order; surplus occurrences fall through to added/removed. A component whose version *and* license both changed is reported in both buckets
- **Compare.razor / CompareSlot.razor** — two-slot dropzone, four stat cards, and a tab per change bucket with a searchable `data-table`. When only one slot is filled, the other is highlighted (`.compare-dropzone.awaiting`) and the hint names the file already chosen

Two entry points:
- **Landing page** — a `.compare-cta` card below the dropzone in `UploadFile.razor`
- **From the viewer** — `CompareWithAnother()` in `Home.razor` seeds the open file as the baseline and navigates to `/compare`. It **re-parses via `GetRawText()` rather than sharing `SbomState`'s `JsonDocument`**: both states own and dispose their documents, so sharing one instance would leave the compare page holding a disposed document once the viewer is cleared

- **Swap** — the control between the slots calls `SbomCompareState.Swap()`, exchanging the two slots and recomputing so the diff runs the other way (Added and Removed trade places). No document is created or disposed, so it is safe with one slot filled or both. Recovers from files chosen in the wrong order, and from the viewer handoff always seeding the open file as the *baseline*
- **Back to a single file** — each filled slot has "↗ Open in viewer" (`OpenInViewer()` in `Compare.razor`), which loads that document into `SbomState` and navigates to `/`. Also re-parsed, for the same ownership reason. The comparison is left intact, and `CompareWithAnother()` detects that the open file is already one side of it and navigates back instead of resetting

Document-level actions (Export Report, Compare, New file) live in the top nav in `Home.razor`, not in the viewer sidebar. Their labels are wrapped in `.nav-action-label`, which collapses to icon-only below 940px so the bar cannot overflow.

### Recent SBOMs (IndexedDB)

The most recent **2** uploads are kept in IndexedDB so they can be reopened on a later visit without locating the file again. Storage is local to the browser — nothing is transmitted.

- **`wwwroot/js/sbom-recent.js`** — flat `sbomRecent*` globals (`Save`/`List`/`Get`/`Clear`), matching the naming convention of the inline scripts in `index.html`. Metadata and file content live in **separate object stores** so listing recents does not read tens of megabytes back out. `sbomRecentSave` does all its reads and writes inside one transaction — awaiting between steps would let the transaction go inactive. Re-uploading a file replaces its earlier entry rather than duplicating it
- **`RecentSbomStore`** — thin interop wrapper. **Every call is best-effort**: IndexedDB is unavailable in some private-browsing modes, can be disabled, and can hit quota. Failures degrade to "no recents" and must never break an upload
- **`SbomLoader.LoadAsync`** saves; the `Load(string, string)` overload does not. That split is deliberate — the string overload is used to re-parse a document when handing it between the viewer and the compare page, and those hand-offs must not churn the history
- **UI** — a "Recent" row under the two cards on the landing page (click to reopen, plus Clear), and `Recent:` shortcut chips inside an empty comparison slot. The chips sit *outside* the dropzone because the `InputFile` overlay covers that whole area

Because files now persist, the compare page's privacy copy says "nothing is sent to a server" rather than the previous "nothing is stored".

### License Risk & Compliance Reporting

- **ComponentRowExtractor** — extracts `ComponentRow` (name, version, type, license, purl, risk) per component/package, format-aware (CycloneDX `components[].licenses`, SPDX 2.x `packages[].licenseConcluded`, SPDX 3.0 `@graph` `software_Package` elements' `software_concludedLicenseExpression`/`software_declaredLicenseExpression`)
- **LicenseClassifier** — classifies a license identifier string into a `LicenseRisk` category (Permissive, WeakCopyleft, StrongCopyleft, Proprietary, Unknown) via a curated identifier lookup
- **DynamicSbomViewer** — Compliance tab shows license risk distribution and a searchable table of flagged (strong-copyleft/unknown) components; an "Export Report" button triggers a hidden `.print-report` section combining vulnerability + license summaries for browser print-to-PDF (via the `sbomPrintReport()` JS function)

### Key Models

- `SchemaNode` — lightweight tree built from JSON data: `PropertyName`, `Title`, `NodeType`, `Properties` dict, `PropertyOrder`, `ItemSchema` (for arrays), `RenderHint`
- `SchemaNodeType` enum — String, Integer, Number, Boolean, Array, Object, Unknown
- `RenderHint` enum — Auto, AccordionSection, SearchableList, BadgeList, KeyValueGroup
- `SbomFormat` enum — CycloneDX_1_5, CycloneDX_1_6, CycloneDX_1_7, SPDX_2_2, SPDX_2_3, SPDX_3_0
- `SbomDiff` record — Added, Removed, VersionChanged, LicenseChanged, UnchangedCount, BaselineCount, CurrentCount
- `ComponentChange` record — Key, Name, Baseline/Current `ComponentRow` (one is null for adds/removes), Kind
- `PackageInfo` record — Name, Version, Ecosystem, Purl
- `ComponentRow` record — Name, Version, Type, License, Purl, Risk
- `LicenseRisk` enum — Permissive, WeakCopyleft, StrongCopyleft, Proprietary, Unknown
- `VulnerabilityResult` record — PackageName, PackageVersion, list of `VulnerabilityEntry`
- `VulnerabilityEntry` record — Id, Summary, Severity, CvssScore, FixedVersion
- `ChatMessage` record — Role, Content, Timestamp

### Lightweight Validation

`SbomFormatDetector.Validate(JsonElement, SbomFormat)` checks required fields per format:
- **CycloneDX** (1.5/1.6/1.7): `bomFormat`, `specVersion`, `metadata` (object), `components` (array)
- **SPDX 2.2/2.3**: `spdxVersion`, `name`, `SPDXID`, `dataLicense`, `documentNamespace`, `creationInfo` (object)
- **SPDX 3.0.1** (JSON-LD): `@graph` (array) containing an `SpdxDocument` element and a `CreationInfo` element

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
- **Vulnerability scanning**: User-initiated two-phase scan. Phase 1: `POST /v1/querybatch` collects vuln IDs per package. Phase 2: `GET /v1/vulns/{id}` fetches full details for each unique ID (max 5 concurrent). Hard caps: 500 packages, 200 vuln detail fetches. Results and warnings stored in `ChatState`. Cleared on new file upload via `ChatState.ClearVulnerabilities()`.
- **Accordion item counts**: Array sections show item count as a `FluentBadge` with accent fill. Vulnerabilities section shows count with red badge (`#d32f2f`).
- **E2E tests**: Use NUnit + Playwright (`PageTest` base class). Tests are black-box — no project reference to `SBOMViewer.Blazor`. Target URL is controlled via `BASE_URL` env var (default `http://localhost:5000`).

## Adding a New SBOM Format

1. Add a value to the `SbomFormat` enum in `src/SBOMViewer.Blazor/Models/SbomFormat.cs`
2. Add detection logic in `SbomFormatDetector.DetectWithDetails()` (peek at a distinguishing JSON property)
3. Add validation logic in `SbomFormatDetector.Validate()` (check required fields)
4. Update `SbomFormatDetector.SupportedVersions` array
5. If the new format reuses an existing field layout (e.g. a minor spec revision), just add it to the existing `switch` arm in `PackageExtractor.ExtractPackages()` and `ComponentRowExtractor.Extract()`. If it's a structurally different document model (e.g. SPDX 3.0's JSON-LD `@graph`), add a new extraction method to both
6. No new viewer components needed — the dynamic rendering pipeline (`SchemaService`, `DynamicSection`, `DynamicObject`) handles any JSON structure automatically

## Branch Strategy

- `main` — production branch, triggers deployment and GitHub release creation
- `release/*` — release branches; merges into these trigger patch version bump + staging deploy + auto-PR to `main`
