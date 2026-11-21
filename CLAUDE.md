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

Smoke subset (five read-only tests, safe against a live site). `EXPECTED_VERSION` makes the footer test assert the exact version:

```bash
dotnet test tests/SBOMViewer.E2E.Tests -c Release --no-build --filter "TestCategory=Smoke" -e BASE_URL=https://sbomviewer.com -e EXPECTED_VERSION=3.2.8
```

#### E2E from Visual Studio Test Explorer

The E2E tests never start the app — with nothing listening they all fail in setup with `net::ERR_CONNECTION_REFUSED`. For Test Explorer, start the app first (`dotnet run --project src/SBOMViewer.Blazor`, or F5), then run the tests: `tests/SBOMViewer.E2E.Tests/local.runsettings` sets `BASE_URL=https://localhost:5157` (the dev server). The csproj applies it **only inside Visual Studio** (`BuildingInsideVisualStudio`), so `dotnet test` and CI keep using the `BASE_URL` environment variable. The dev server is slower than the published build (about 9 minutes for the suite versus 4).

## Project Structure

```
SBOMViewer.slnx
├── Directory.Build.props               # Release line (MAJOR.MINOR); patch comes from git tags
├── CLAUDE.md
├── AGENTS.md
├── Infra/
│   └── main.bicep                      # Azure Static Web App infrastructure
├── .github/workflows/
│   ├── ci.yml                          # PR validation: build + unit tests + E2E (required check)
│   ├── release.yml                     # Push to main: build once → staging → approve → production → tag + release
│   ├── smoke.yml                       # Reusable: Smoke-category E2E tests against a deployed site
│   ├── rollback.yml                    # Manual: redeploy a previous release's zip, then push a revert branch
│   ├── claude.yml                      # Agent: `feature-request` label or @claude comment → claude/* branch
│   ├── claude-review.yml               # Agent: review comments on PRs (never approves)
│   ├── claude-propose.yml              # Agent: weekly, opens at most one `proposal` issue
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
│       │   ├── QualityTab.razor        # SBOM quality readiness checklist (NTIA 2021 / CISA 2026 profiles)
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
│       │   ├── QualityFactsExtractor.cs # Per-format extraction of QualityFacts (Present / DeclaredUnknown / Missing / NotExpressible)
│       │   ├── SbomQualityService.cs   # Pure rule evaluation: QualityFacts + profile → QualityReport
│       │   ├── QualityReportExporter.cs # QualityReport → JSON for the Quality tab's "Download JSON"
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
│       │   ├── QualityReport.cs        # FactState, ComponentFacts, QualityFacts, QualityProfile, CheckStatus, QualityCheck, QualityReport
│       │   ├── VulnerabilityResult.cs  # CVE entries per package
│       │   └── ChatMessage.cs          # Chat message (role, content, timestamp)
│       └── wwwroot/
│           ├── index.html              # Host page (SEO meta, Google Analytics, Fluent theme loader)
│           ├── robots.txt              # Search engine crawl rules
│           ├── sitemap.xml             # Sitemap for SEO
│           ├── staticwebapp.config.json # Azure SWA SPA fallback so client routes (/compare) resolve
│           ├── js/sbom-recent.js       # IndexedDB helpers for the recent-files store
│           ├── samples/demo-storefront.cdx.json # Bundled demo SBOM behind "Try a sample" (deployed)
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
    │       ├── SampleSbomTests.cs       # Bundled demo SBOM: loads, scannable, license mix, dependencies resolve
    │       ├── SbomDiffServiceTests.cs  # purl identity, add/remove/version/license, cross-format, edge cases
    │       ├── SbomCompareStateTests.cs # Two-slot state, diff recomputation, clear/replace
    │       ├── LicenseClassifierTests.cs   # License identifier → LicenseRisk classification tests
    │       ├── QualityFactsExtractorTests.cs # Per-format facts, NOASSERTION handling, both CycloneDX tools shapes, fixtures
    │       ├── SbomQualityServiceTests.cs  # Pass/Partial/Fail thresholds, NotExpressible, zero components, profiles, real-generator fixtures
    │       ├── QualityReportExporterTests.cs # JSON shape per check scope, uncapped affected lists, download file names
    │       └── VulnerabilityServiceTests.cs # OSV.dev API client tests
    └── SBOMViewer.E2E.Tests/
        ├── PlaywrightSetup.cs          # One-time Chromium install ([SetUpFixture])
        ├── TestBase.cs                 # PageTest base — reads BASE_URL env var, waits for Blazor bootstrap
        ├── HomePageTests.cs            # Smoke tests: title, header, upload button, badges, card, theme, footer
        ├── FileUploadTests.cs          # Upload tests: CycloneDX 1.5/1.6/1.7, SPDX 2.2/2.3/3.0.1, unsupported, invalid JSON, search
        ├── CompareTests.cs             # /compare route, two-slot upload, identical files, cross-format diff, swap, reset
        ├── RecentSbomTests.cs          # IndexedDB persistence, 2-entry cap, dedup, clear, slot shortcut
        ├── SampleTests.cs              # "Try a sample" link, Sample chip, /?sample=1 deep link, recents untouched
        └── QualityTests.cs             # Quality tab: headline, profile switch, expand check, Overview card, JSON download, zero requests
```

## Environment

- This is a Windows development environment. Always use forward slashes or properly escaped paths in shell commands. Never use raw backslashes in Bash commands.
- When running PowerShell commands, prefer `pwsh -Command` or use PowerShell-native syntax.

## Architecture

**Solution** (`SBOMViewer.slnx`) with one app project (`src/SBOMViewer.Blazor`) and two test projects under `tests/`, all targeting .NET 10.0.

### Versioning

`Directory.Build.props` holds only the release line: `<VersionPrefix>3.3.0</VersionPrefix>` plus `<VersionSuffix>local</VersionSuffix>`, so a local build reports `3.3.0-local`. Edit it by hand only for a minor or major bump. **The patch comes from git tags**: `release.yml` takes the highest `vMAJOR.MINOR.*` tag, adds one, and builds with `-p:Version=…`. Nothing commits a version back to the repo, and the tag is created only after production passes its smoke test — so a tag always means "this went live", always on a `main` commit. Tags are never moved or deleted.

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
- **UI** — a compact "Recent" strip in the `.quick-row` above the two cards on the landing page, beside the "Try a sample" button (click to reopen, plus Clear) — above, not below, so neither is pushed under the fold, and `Recent:` shortcut chips inside an empty comparison slot. The chips sit *outside* the dropzone because the `InputFile` overlay covers that whole area

Because files now persist, the compare page's privacy copy says "nothing is sent to a server" rather than the previous "nothing is stored".

### Try a Sample (demo SBOM)

A visitor with no SBOM on disk can open a bundled demo from the landing page ("Try a sample →") or via the deep link `/?sample=1`.

- **`wwwroot/samples/demo-storefront.cdx.json`** — hand-curated CycloneDX 1.6 document for a fictional app. **Every component is a real package at a real version** (an invented purl returns nothing from OSV), across Maven, npm, PyPI and NuGet, with real registry hashes. It deliberately includes `log4j-core 2.14.1` (Log4Shell, KEV-listed), GPL/AGPL/LGPL components, one component with no license, and a few components missing hashes or supplier. Distinct from the repo-root `samples/`, which are small test fixtures and are not deployed
- **The scan result drifts** — new CVEs are published against old versions. Never assert exact vulnerability counts against this file; `SampleSbomTests` checks structure only. Keep the total comfortably under the 200 vuln-detail cap (about 120 when curated; Django and Pillow were left out because either alone returns 80+ advisories)
- **Loading** — `UploadFile.LoadSample()` fetches the file with an absolute URL built from `NavigationManager.BaseUri` (the registered `HttpClient` has no `BaseAddress`) and parses it with `SbomLoader.Load(string, string)`, which **does not save to recents** — the demo must not evict a real recent file
- **`SbomState.IsSample`** — drives the "Sample" chip beside the file name in the viewer. It has a private setter: the only way to open a document is `SbomState.Load(result, isSample)`, which assigns every field and sets `Document` last, since assigning the document is what notifies subscribers. `Clear()` resets it. `Load` and `Clear` also reset the scan results (`SbomState` takes `ChatState` as an optional constructor dependency), so no caller has to remember `ChatState.ClearVulnerabilities()`. `SbomCompareState` carries the flag per slot (`BaselineIsSample` / `CurrentIsSample`) so the chip survives viewer → compare → "Open in viewer"
- **Deep link** — `UploadFile.OnInitializedAsync` strips `?sample=1` with a replace-navigation before loading, so "New file" does not land on a URL that reloads the sample. Only the `sample` key is removed (`GetUriWithQueryParameter`), so campaign parameters on a shared link survive. The recents read is started but not awaited first, so a slow IndexedDB cannot delay the demo
- **OSV package names** — SBOMs carry the short name (`log4j-core`); OSV indexes the namespaced one: `group:artifact` (Maven), `@scope/name` (npm), `vendor/name` (Packagist), full module path (Go). `PackageExtractor.OsvName` derives it **from the purl**, for every format, with the CycloneDX `group` field only as a fallback. Scan results are therefore keyed by the OSV name, not the display name. `ComponentRowExtractor` calls the same `OsvName` with the same inputs and stores it on `ComponentRow.OsvName`, which is what the Components tab looks results up by — `Name` is for display only. For SPDX 2.x both extractors take the purl from `PackageExtractor.ExtractPurlFromExternalRefs` so they cannot pick different references. Purls are parsed by hand (`ParsePurl`), not by regex: a scoped npm purl may spell the scope `%40scope` or a literal `@scope`

### License Risk & Compliance Reporting

- **ComponentRowExtractor** — extracts `ComponentRow` (name, version, type, license, purl, risk) per component/package, format-aware (CycloneDX `components[].licenses`, SPDX 2.x `packages[].licenseConcluded`, SPDX 3.0 `@graph` `software_Package` elements' `software_concludedLicenseExpression`/`software_declaredLicenseExpression`)
- **LicenseClassifier** — classifies a license identifier string into a `LicenseRisk` category (Permissive, WeakCopyleft, StrongCopyleft, Proprietary, Unknown) via a curated identifier lookup
- **DynamicSbomViewer** — Compliance tab shows license risk distribution and a searchable table of flagged (strong-copyleft/unknown) components; an "Export Report" button triggers a hidden `.print-report` section combining vulnerability + license summaries for browser print-to-PDF (via the `sbomPrintReport()` JS function)

### SBOM Quality (readiness check)

The **Quality** tab answers "is this SBOM complete enough to hand over?" — distinct from the Compliance tab, which is about license risk. It checks that fields are **populated**, never that values are correct, and the UI never says "compliant" or "certified". Runs entirely locally; no network requests.

- **Profiles** — `CISA 2026` (default; *2026 Minimum Elements for an SBOM*, v2.1, July 2026 — 16 checks) and `NTIA 2021` (7 checks). The CISA document has **no numbered sections**, so each rule's `Reference` names the category and element (`CISA 2026 · Component Data: Component Producer`). Element lists and mapping decisions are recorded in `docs/feature-enhancements/November26/02-quality-scorecard.md`
- **QualityFactsExtractor** — format-switched like `ComponentRowExtractor` but separate from it: `ComponentRowExtractor` collapses `NOASSERTION` to `""`, which loses the distinction the scorecard reports. Every fact is a `FactState`:
  - `Present`; `DeclaredUnknown` (`NOASSERTION`, `NONE`, literal `unknown` — but license `NONE` is Present, since it asserts "no license"); `Missing` (absent or empty); `NotExpressible` (the format has no field — e.g. generation context and signature in SPDX 2.x, SBOM version and signature in SPDX 3.0.1)
  - CycloneDX `metadata.tools` has **two shapes** (legacy array; 1.5+ object with `components`/`services`). SBOM author is `metadata.authors` only — `metadata.supplier`/`manufacturer` describe the component, not the SBOM author
  - SPDX tool version comes from the `name-version` convention (`Tool: syft-1.18.0`) — neither SPDX 2.x nor the 3.0.1 `Tool` class has a version field
  - Unnamed components are kept (with a placeholder label) so "Component name" can fail
  - CycloneDX `cryptographic-asset` and `file` components are skipped, so the Quality component count can be lower than the Components tab's. An algorithm has no supplier, version or hash in the CISA sense; a `file` is the counterpart of an SPDX File, and the SPDX extractors count packages only (syft writes the same workflow files both ways)
  - SPDX 3.0.1 package hash also follows `hasDistributionArtifact` to the file's `verifiedUsing` — converters from 2.x move the package checksum there, leaving only a `PackageVerificationCode` on the package
- **SbomQualityService** — static and pure. A profile is a list of `Rule`s (fact selector, reference, fix hint); adding BSI TR-03183-2 is a new list. Component checks: **Pass at 100%, Fail at 0% (or zero components), Partial in between** — no invented pass mark. `NotExpressible` checks are excluded from `Applicable`. Declared unknowns are counted as affected but reported separately. Coverage is a labelled heuristic: it shows components that appear in a relationship, not that transitive dependencies are complete
- **UI** — `QualityTab.razor` is presentational (takes the `QualityReport` + `OnProfileChanged`). `DynamicSbomViewer` owns the facts and report, because the sidebar count (`10/14`), the Overview "Quality" card (fifth card, `.stats-grid-5`) and the `.print-report` Quality section all use the same result. Profile changes re-evaluate only; facts are extracted once per `JsonDocument`. Affected-component lists are uncapped in the model and capped at `QualityTab.AffectedCap` (200) when rendered, with a filter box
- **Never on the load path.** `BuildQuality()` (called from `OnParametersSet`) only resets state on a new document. At or under `DynamicSbomViewer.QualityAutoRunLimit` (2,000) components the check is queued for `OnAfterRenderAsync`, after the dashboard has painted; above it the tab shows "Run readiness check" and waits for a click — WebAssembly is single-threaded, so the pause should be one the user chose. `RunQualityAsync` yields with `Task.Delay(1)` so the spinner paints, and discards its result if the document was replaced meanwhile. Measured: 5,000 components ≈ 870 ms in the published build; `QualityTests.LargeSbom_WaitsForAClick_ThenRuns` logs `QUALITY-PERF` for re-measuring. Export before the check has run prints "not run" rather than computing it
- **Real-generator fixtures are local-only.** cdxgen, syft, trivy, sbom-tool and spdx-maven-plugin output lives in a developer's `samples/` folder (listed in `.git/info/exclude`, not committed). Their tests in `SbomQualityServiceTests` use `[LocalSampleFact("file")]` / `[LocalSampleTheory]` (`TestData/LocalSampleFactAttribute.cs`), which skip when the file is absent, so CI stays green. Every behaviour those files uncovered also has an inline test in `QualityFactsExtractorTests`
- **Download JSON.** The Quality toolbar's button saves the selected profile's report via `QualityReportExporter` and the existing `sbomDownloadFile` helper, as `<stem>.quality-<cisa-2026|ntia-2021>.json`. It contains every check, summary counts, the disclaimer and the **full, uncapped** affected-component lists, plus the source file name. That is fine because it is saved locally. Written with `Utf8JsonWriter`, not reflection; `schemaVersion` is bumped when a field is renamed or removed. File-name sanitising uses a fixed Windows-safe character set and splits on both `/` and `\`, because `Path` in WebAssembly behaves as on Linux
- **Beta + "Report a wrong result".** The tab, Overview card and print heading carry a Beta label. Each check links to `FeedbackLink.ForQualityCheck`, a pre-filled GitHub issue naming the profile, check id, element reference and counts and asking which tool generated the SBOM — **never the file name or component names** (an internal package name can be as revealing as a file name). `Home.razor` passes `AppVersion` and the user agent down for the prefill

### Key Models

- `SchemaNode` — lightweight tree built from JSON data: `PropertyName`, `Title`, `NodeType`, `Properties` dict, `PropertyOrder`, `ItemSchema` (for arrays), `RenderHint`
- `SchemaNodeType` enum — String, Integer, Number, Boolean, Array, Object, Unknown
- `RenderHint` enum — Auto, AccordionSection, SearchableList, BadgeList, KeyValueGroup
- `SbomFormat` enum — CycloneDX_1_5, CycloneDX_1_6, CycloneDX_1_7, SPDX_2_2, SPDX_2_3, SPDX_3_0
- `SbomDiff` record — Added, Removed, VersionChanged, LicenseChanged, UnchangedCount, BaselineCount, CurrentCount
- `ComponentChange` record — Key, Name, Baseline/Current `ComponentRow` (one is null for adds/removes), Kind
- `PackageInfo` record — Name, Version, Ecosystem, Purl
- `ComponentRow` record — Name, Version, Type, License, Purl, Risk, plus `OsvName` (the key scan results are looked up by)
- `LicenseRisk` enum — Permissive, WeakCopyleft, StrongCopyleft, Proprietary, Unknown
- `QualityFacts` / `ComponentFacts` records — format-neutral `FactState` per field; `QualityReport` — profile + `QualityCheck` list (`Passed`, `Applicable`, `ComponentsWithGap`, `AverageComponentCoverage`)
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
| `ci.yml` | PR to `main` | Build, unit tests, Playwright E2E (skipped for docs-only changes); required check `build-and-test` |
| `release.yml` | Push to `main` | Compute version → build once → staging → smoke → **approval** → production → smoke → tag, GitHub release with the site zip attached |
| `smoke.yml` | Called by `release.yml` and `rollback.yml`; manual | `Smoke`-category E2E tests against a URL, with `EXPECTED_VERSION` |
| `rollback.yml` | Manual (`gh workflow run rollback.yml --ref main`) | **Approval** → redeploy a previous release's attached zip (no rebuild) → smoke → push `revert/vX` with one revert commit and open an issue linking to "create the PR". See `docs/runbook-rollback.md` |
| `claude.yml` | `feature-request` label, or `@claude` comment — **owner only** | Claude implements on a `claude/*` branch and replies with a create-PR link |
| `claude-review.yml` | PR opened / ready / reopened by the owner | Claude review: inline comments + one sticky summary. Comments only |
| `claude-propose.yml` | Mondays 14:00 UTC; manual | Opens at most one `proposal` issue from plans, open issues and failures |
| `deploy-bicep.yml` | Change to `Infra/main.bicep` | Deploy Azure infrastructure |

Deploy jobs declare a GitHub Environment — `staging` (staging-sbomviewer app, staging.sbomviewer.com) or `production` (app-sbomviewer-eastus-001, sbomviewer.com; accepts `main` only, **required reviewer**). Each environment holds its own `AZURE_STATIC_WEB_APPS_API_TOKEN`, so a job gets the token of the environment it declares. The production approval is the release decision; rejecting it ends the run with no tag. Runs are serialized (`concurrency: release`), so a merge made while a run awaits approval queues behind it.

### Agentic flow

```
issue + `feature-request` label ─► claude.yml: branch claude/issue-N-… + "create PR" link
   you open the PR ─► ci.yml (required) + claude-review.yml (comments)
   you squash-merge ─► release.yml: staging ─► smoke ─► PR comment "approve production"
   you approve ─► production ─► smoke ─► tag + release ─► PR comment "released" (issue closes via "Closes #N")
   production smoke fails ─► rollback.yml requested automatically (waits for your approval) + issue
weekly ─► claude-propose.yml opens ≤1 `proposal` issue; relabel it `feature-request` to act on it
```

The agent never merges, approves, or deploys. The human gates are: opening the PR, merging it, and approving the `production` environment. Guard rails:

- **Owner-only triggers.** The repo is public; every Claude workflow checks the sender is the repository owner, so nobody else can spend the Claude subscription or steer the agent.
- **`.github/CODEOWNERS`** covers `.github/`, `Infra/` and `Directory.Build.props`; the `main` ruleset requires code-owner review. An agent cannot change the pipeline, its permissions or the release line without your review. Your own PRs touching those paths need the ruleset bypass at merge time (you cannot approve your own PR).
- **Admin bypass is `pull_request` mode** — overrides happen only when merging a PR; nobody pushes to `main` directly.
- **Actions may not create or approve PRs** (repo setting, kept off). Workflows push branches and post links instead; a PR you open runs the required CI.
- `CLAUDE_CODE_OAUTH_TOKEN` (repo secret) authenticates the Claude workflows. Refresh with `claude setup-token` → `gh secret set CLAUDE_CODE_OAUTH_TOKEN`.

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
5. If the new format reuses an existing field layout (e.g. a minor spec revision), just add it to the existing `switch` arm in `PackageExtractor.ExtractPackages()`, `ComponentRowExtractor.Extract()` and `QualityFactsExtractor.Extract()`. If it's a structurally different document model (e.g. SPDX 3.0's JSON-LD `@graph`), add a new extraction method to all three
6. No new viewer components needed — the dynamic rendering pipeline (`SchemaService`, `DynamicSection`, `DynamicObject`) handles any JSON structure automatically

## Branch Strategy

Trunk-based. Feature branches open a PR to `main` and are **squash-merged** (enforced by the ruleset), so one change is one commit and one `git revert` removes it. `build-and-test` must pass. Merging starts `release.yml`. The old `release/*` branches are kept as history only; nothing deploys from them.
