# CycloneDX 1.5 support (backward compatibility)

## Context

The app supported CycloneDX 1.6/1.7 but rejected 1.5 (released June 2023) as
unsupported, even though the fields this app reads — `bomFormat`, `specVersion`,
`metadata`, `components[].name/version/purl/licenses` — are unchanged between 1.5 and
1.6. The only schema difference is `evidence.identity` going from a single object
(1.5) to an array (1.6+), which this app doesn't read. So this was a trivial,
same-shape addition — not a new format family like SPDX 3.0.

## Changes (implemented)

- `Models/SbomFormat.cs`: added `CycloneDX_1_5`.
- `Services/SbomFormatDetector.cs`:
  - Added `"1.5"` to `SupportedCycloneDXVersions`.
  - `DetectWithDetails` now maps `specVersion == "1.5"` → `SbomFormat.CycloneDX_1_5`.
  - Routed `CycloneDX_1_5` through the existing `ValidateCycloneDX`.
  - Added `"CycloneDX 1.5"` to `SupportedVersions`.
- `Services/PackageExtractor.cs` and `Services/ComponentRowExtractor.cs`: added
  `SbomFormat.CycloneDX_1_5` to the existing CycloneDX case in each `switch` — no new
  extraction code needed.
- Renamed sample `samples/cyclonedx-1.5-unsupported.json` → `samples/cyclonedx-1.5-full.json`
  (it's no longer an "unsupported version" fixture).
- Added a CycloneDX 1.5 fixture (`ValidCycloneDX15Minimal`, `ValidCycloneDX15WithComponents`)
  to `TestJson.cs` and corresponding detector/extractor unit tests.
- Updated `GetFormatName`/`GetFormatVersion` (`DynamicSbomViewer.razor`) and
  `FormatDisplay` (`Home.razor`) to display CycloneDX 1.5.

## Tasks

- [x] Add `CycloneDX_1_5` to `SbomFormat` enum
- [x] Add `"1.5"` to `SupportedCycloneDXVersions` + mapping in `DetectWithDetails`
- [x] Route `CycloneDX_1_5` through existing `ValidateCycloneDX`
- [x] Add `"CycloneDX 1.5"` to `SupportedVersions`
- [x] Add `CycloneDX_1_5` to the CycloneDX case in `PackageExtractor.ExtractPackages`
- [x] Add `CycloneDX_1_5` to the CycloneDX case in `ComponentRowExtractor.Extract`
- [x] Add/rename CycloneDX 1.5 sample file under `samples/`
- [x] Add CycloneDX 1.5 fixture to `TestJson.cs` + detector/extractor unit tests
- [x] Update CLAUDE.md format list

## Verification

- `dotnet test tests/SBOMViewer.Blazor.Tests` — all 144 tests pass.
- `dotnet build SBOMViewer.slnx` — 0 warnings, 0 errors.
- Manual: upload `samples/cyclonedx-1.5-full.json` and confirm format badge, components
  table, license/compliance sections render correctly.
