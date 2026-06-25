# SPDX 3.0.1 support

## Context

SPDX 3.0.1 (released 2024, current latest major SPDX version) is a genuinely different
document model from 2.x — JSON-LD instead of plain JSON, no top-level
`packages`/`SPDXID`/`spdxVersion` fields, no single root document. Everything lives in a
flat `@graph` array of typed elements (`Build`, `software_Package`, `Relationship`,
etc.) with profile declarations (`core`, `software`, `simpleLicensing`, `security`, ...).
Real-world adoption is growing (e.g. Yocto 5.1). This is a new format family, not a
version bump — it needs real extraction logic, not just a version-string change.

The dynamic rendering pipeline (`SchemaService.BuildFromJson`, `DynamicSection`,
`DynamicObject`) is format-agnostic and needs no changes — it walks whatever JSON shape
it's given. The work is entirely in detection, validation, and the two extractors.

## Changes

- `Models/SbomFormat.cs`: add `SPDX_3_0`.
- `Services/SbomFormatDetector.cs`:
  - Detect by looking for the JSON-LD shape: a top-level `@graph` array (or `@context`
    referencing `spdx.org`) rather than a `spdxVersion` string at the root. The spec
    version actually lives on a `CreationInfo` element inside `@graph` (look for an
    element with `"type": "CreationInfo"` and read its `specVersion`).
  - Add `ValidateSpdx3`: check for `@graph` (array, non-empty) and at least one element
    typed `"SpdxDocument"` and one `"CreationInfo"`.
  - Add `"SPDX 3.0.1"` to `SupportedVersions`.
- `Services/PackageExtractor.cs` / `Services/ComponentRowExtractor.cs`: new
  `ExtractSpdx3` method — iterate `@graph`, filter elements where `type` is
  `"software_Package"`, read `software_packageVersion` for version,
  `software_concludedLicenseExpression`/`software_declaredLicenseExpression` for
  license, and `software_packageUrl` directly for the purl (the simplified, most common
  case — full spec also allows purl via a separate `ExternalIdentifier` element of type
  `"PackageManager"`, not implemented here since no real-world generator we've seen
  uses that form for purl).
- Add an SPDX 3.0.1 sample file (a simplified, JSON-Schema-conformant document — not
  full RDF/JSON-LD tooling output) under `samples/`.
- Add a corresponding test fixture and unit tests.

## Tasks

- [x] Add `SPDX_3_0` to `SbomFormat` enum
- [x] Detect `@graph` / JSON-LD shape and locate `CreationInfo.specVersion` in
      `SbomFormatDetector.DetectWithDetails`
- [x] Add `ValidateSpdx3` (checks `@graph`, `SpdxDocument` element, `CreationInfo` element)
- [x] Add `"SPDX 3.0.1"` to `SupportedVersions`
- [x] Implement `ExtractSpdx3` in `PackageExtractor` (iterate `@graph`, filter
      `software_Package` elements, read version/purl)
- [x] Implement `ExtractSpdx3` in `ComponentRowExtractor` (same filter, read license
      expression fields)
- [x] Add SPDX 3.0.1 sample file under `samples/` (`samples/spdx-3.0.1-full.json`)
- [x] Add SPDX 3.0.1 fixture + detector/extractor unit tests
- [x] Update CLAUDE.md format list

## Verification

- `dotnet test tests/SBOMViewer.Blazor.Tests` — all 148 tests pass.
- `dotnet build SBOMViewer.slnx` — 0 warnings, 0 errors.
- Manual: upload `samples/spdx-3.0.1-full.json` and confirm format badge, components
  table, license/compliance sections, and vulnerability scan all populate correctly.

## Security & build profile fields

`samples/spdx-3.0.1-full.json` was enriched to exercise SPDX 3.0's headline new
capabilities beyond a plain component list:
- **`Agent`** element — `CreationInfo.createdBy` now references an `Agent` element's
  `spdxId` instead of a bare string, matching how real SPDX 3.0 documents structure
  creator attribution (and showing `profileConformance` declared on `CreationInfo`).
- **`security_Vulnerability`** element — SPDX 3.0's native security/VEX profile lets a
  vulnerability (e.g. a CVE) live as a first-class graph element, linked to an affected
  package via a `Relationship` of type `hasAssociatedVulnerability`. This is the
  capability most often cited as SPDX 3.0's biggest practical improvement over 2.x,
  where vulnerability data had to live in a separate VEX document.
- **`Build`** element — the build profile (`buildType`, `configSourceEntrypoint`,
  `buildStartTime`/`buildEndTime`), linked to the package it produced via a
  `generates` `Relationship`.

None of these required extractor changes: `ExtractSpdx3` only reads elements where
`type == "software_Package"`, so `Agent`/`security_Vulnerability`/`Build`/`Relationship`
elements are silently skipped. This is locked in by
`ValidSpdx30WithSecurityAndBuildProfile` in `TestJson.cs` and the corresponding tests
`ExtractPackages_Spdx30_IgnoresSecurityAndBuildProfileElements_StillFindsPackage`
(`PackageExtractorTests.cs`) and `Extract_Spdx30_SecurityAndBuildProfileElements_DontBreakExtraction`
(`ComponentRowExtractorTests.cs`).

## Known limitation

`ExtractSpdx3` only recognizes purl via the direct `software_packageUrl` field, not via
a separate `ExternalIdentifier` element. If real-world SPDX 3.0 SBOMs from common
generators (e.g. syft, sbom-tool once they add 3.0 support) commonly use the
`ExternalIdentifier` form instead, this extractor will need a second lookup path.
