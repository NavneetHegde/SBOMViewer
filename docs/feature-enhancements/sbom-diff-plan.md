# SBOM Diff — Compare Two Documents

## Context

The app can only view one SBOM at a time. `SbomState` holds a single `JsonDocument`, and `Pages/Home.razor` renders either the upload screen or the viewer for that one document.

The question users actually have at release time is *"what changed since the last version, and did we introduce new CVEs?"* Answering it today means opening two browser tabs and comparing by eye. This is also rare as a free, fully client-side tool — most alternatives require uploading SBOMs to a server.

## Requirements

- Compare two SBOM documents side by side.
- Report components added, removed, version-changed, and license-changed.
- Comparison must work across formats (e.g. an SPDX 2.2 baseline against a CycloneDX 1.6 current), since teams do migrate formats between releases.
- Stay fully client-side; no upload to any backend.

## Approach

### State

Extend `Services/SbomState.cs` with a second slot (`BaselineDocument`, `BaselineSchema`, `BaselineFormat`, `BaselineFileName`), or introduce a parallel `SbomCompareState` singleton. Prefer the latter if adding fields to `SbomState` complicates the single-document path that `Home.razor` and `DynamicSbomViewer` depend on — `SbomState.Clear()` and the `Document` setter both dispose the `JsonDocument`, and that lifecycle should not become conditional.

### Extraction — reuse what exists

**Do not write new parsing logic.** `Services/ComponentRowExtractor.cs` already yields a `ComponentRow` (Name, Version, Type, License, Purl, Risk) for every supported format, and `Services/LicenseClassifier.cs` already maps a license identifier to a `LicenseRisk`. The diff operates on extracted rows, not raw JSON — which is exactly why cross-format comparison works for free.

### Diff engine

New `Services/SbomDiffService.cs` performing a keyed join over the two `ComponentRow` lists:

- Key on `Purl` when present on both sides; fall back to `Name` (case-insensitive).
- Emit a `SbomDiff` model (new, `Models/SbomDiff.cs`):
  - `Added` — rows present only in current
  - `Removed` — rows present only in baseline
  - `VersionChanged` — same key, different `Version` (carry old and new)
  - `LicenseChanged` — same key, different `License` or `Risk` (carry old and new)
- Unchanged components are counted but not enumerated, to keep the UI scannable.

Edge cases to handle explicitly: duplicate keys within one document (same package at two versions), rows with neither purl nor name, and an empty baseline.

### UI

New route `Pages/Compare.razor` with a two-slot dropzone (baseline / current).

Reuse the parse pipeline from `Components/UploadFile.razor` rather than duplicating it — the 20MB cap, `.json` extension check, `SbomFormatDetector.DetectWithDetails` → `SbomFormatDetector.Validate` → `SchemaService.BuildFromJson` sequence, and the unsupported-version and invalid-JSON error messages. Extract that sequence into a shared method or service first, then call it from both pages.

Render the four diff buckets as sections with counts, following the table and search patterns already used in `RenderComponentsTab` in `Components/DynamicSbomViewer.razor`.

### Phase 2 — new vulnerabilities (optional, highest signal)

Run `Services/VulnerabilityService.cs` against both package sets (via `PackageExtractor`) and surface CVEs present in current but not baseline. This is the single most valuable output of the diff and should be built once phase 1 is stable. Note the existing hard caps (500 packages, 200 vuln detail fetches) apply per scan — scanning two documents doubles the API cost, so consider whether the caps need to be per-comparison rather than per-scan.

## Files

| File | Change |
|------|--------|
| `Models/SbomDiff.cs` | New — diff result model |
| `Services/SbomDiffService.cs` | New — keyed join over `ComponentRow` lists |
| `Pages/Compare.razor` | New — two-slot upload + diff rendering |
| `Services/SbomState.cs` | Second document slot (or new `SbomCompareState`) |
| `Components/UploadFile.razor` | Extract shared parse pipeline |
| `Program.cs` | Register `SbomDiffService` |

## Verification

- Unit-test `SbomDiffService` with cross-format pairs from `samples/`: `spdx-2.2-full.json` vs `spdx-2.3-full.json`, and a CycloneDX-vs-SPDX pair. Cover all four buckets plus the duplicate-key and empty-baseline edge cases.
- Comparing a file against itself must produce an empty diff — a good canary for key-matching bugs.
- Manually verify in the browser (`dotnet run --project src/SBOMViewer.Blazor`) that uploading two files, then replacing one, refreshes the diff without leaking the previous `JsonDocument`.
- Add an E2E test in `tests/SBOMViewer.E2E.Tests/` following the `FileUploadTests.cs` pattern.
