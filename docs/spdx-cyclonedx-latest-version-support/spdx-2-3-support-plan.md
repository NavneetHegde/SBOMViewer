# SPDX 2.3 support

## Context

The app currently supports CycloneDX 1.6/1.7 (1.7 is the current latest CycloneDX
release) and only SPDX 2.2. SPDX 2.3 (released 2023) uses the same plain-JSON shape as
2.2 — it's purely additive (new optional fields like snippet info, additional license
fields) — so this is a small, low-risk addition using the existing
"Adding a New SBOM Format" recipe in CLAUDE.md.

## Changes

- `Models/SbomFormat.cs`: add `SPDX_2_3`.
- `Services/SbomFormatDetector.cs`:
  - Add `"SPDX-2.3"` to `SupportedSpdxVersions`.
  - In `DetectWithDetails`, map `"SPDX-2.3"` → `SbomFormat.SPDX_2_3` (mirror the
    CycloneDX 1.6/1.7 branching already there for `version == "1.7"`).
  - Route `SbomFormat.SPDX_2_3` through the existing `ValidateSpdx` (required fields are
    identical to 2.2).
  - Add `"SPDX 2.3"` to `SupportedVersions`.
- `Services/PackageExtractor.cs` and `Services/ComponentRowExtractor.cs`: add
  `SbomFormat.SPDX_2_3` to the `switch` alongside `SPDX_2_2` — same extraction method,
  no new code needed (field names `packages`, `versionInfo`, `licenseConcluded`,
  `externalRefs` are unchanged in 2.3).
- Add an SPDX 2.3 sample file under `samples/`.
- Add an SPDX 2.3 fixture to `tests/SBOMViewer.Blazor.Tests/TestData/TestJson.cs` (copy
  the existing SPDX 2.2 fixture, bump `spdxVersion` to `"SPDX-2.3"`).

## Tasks

- [x] Add `SPDX_2_3` to `SbomFormat` enum
- [x] Add `"SPDX-2.3"` detection + mapping in `SbomFormatDetector.DetectWithDetails`
- [x] Route `SPDX_2_3` through existing `ValidateSpdx`
- [x] Add `"SPDX 2.3"` to `SupportedVersions`
- [x] Add `SPDX_2_3` case to `PackageExtractor.ExtractPackages` switch
- [x] Add `SPDX_2_3` case to `ComponentRowExtractor.Extract` switch
- [x] Add SPDX 2.3 sample file under `samples/` (`samples/spdx-2.3-full.json`)
- [x] Add SPDX 2.3 fixture to `TestJson.cs` + detector/extractor unit tests
- [x] Update CLAUDE.md format list
- [x] Verify SPDX 2.3's new security/supply-chain fields render correctly and don't
      break extraction (see "Security & supply-chain fields" below)

## Verification

- `dotnet test tests/SBOMViewer.Blazor.Tests` — all 146 tests pass.
- `dotnet build SBOMViewer.slnx` — 0 warnings, 0 errors.
- Manual: upload `samples/spdx-2.3-full.json` and confirm format badge, components
  table, license/compliance sections, and vulnerability scan all populate correctly.

## Security & supply-chain fields (the reason 2.3 matters)

SPDX 2.3's main additions over 2.2 are metadata fields used by modern SBOM generators
(syft, sbom-tool, Microsoft's tooling) for supply-chain and security tracking:
- `primaryPackagePurpose` (e.g. `LIBRARY`, `APPLICATION`, `CONTAINER`, `OPERATING-SYSTEM`)
- `builtDate`, `releaseDate`, `validUntilDate` on packages
- A new `SECURITY` `externalRefs` category, with `referenceType`s like `cpe23Type`,
  `cpe22Type`, and `advisory`

None of these required code changes:
- The dynamic rendering pipeline (`SchemaService`, `DynamicSection`, `DynamicObject`) is
  JSON-driven and renders any field present in the uploaded document automatically — no
  static SPDX model to extend.
- `PackageExtractor`/`ComponentRowExtractor` only read the specific fields they need
  (`name`, `versionInfo`, `licenseConcluded`, and `externalRefs` entries where
  `referenceType == "purl"`), so the new fields are silently ignored rather than
  breaking extraction. In particular, `SECURITY` refs (`cpe23Type`, `advisory`) are
  never mistaken for a `purl` reference, since the extractor checks `referenceType`,
  not just presence of an `externalRefs` entry.
- `samples/spdx-2.3-full.json` now includes a package with `primaryPackagePurpose`,
  `builtDate`/`releaseDate`/`validUntilDate`, and both a `PACKAGE-MANAGER`/`purl` ref and
  two `SECURITY` refs, to exercise this in the real app.
- `TestJson.ValidSpdx23WithSecurityAndSupplyChainFields` + the two new tests
  (`ExtractPackages_Spdx23_IgnoresSecurityRefs_StillFindsPurl` in
  `PackageExtractorTests.cs`, `Extract_Spdx23_NewSupplyChainAndSecurityFields_DontBreakExtraction`
  in `ComponentRowExtractorTests.cs`) lock this behavior in.
