# Plan: License Compliance Report — SBOMViewer

## Context

SBOMViewer gets ~10 visits/day and is a 100% client-side static app (no backend, no accounts). To grow organic traffic and make the product more credible for enterprise/compliance use, this plan adds license-risk classification: instead of just listing the raw license string per component, components are bucketed into permissive / weak-copyleft / strong-copyleft / unknown so risky or unclear licenses are surfaced automatically. This is the single most-searched-for SBOM use case after vulnerability scanning, and the raw license data is already half-extracted.

Stays fully client-side — no new backend, no new npm/CDN dependencies, no accounts.

**Release:** `license-compliance-report` (Phase 1)
**Companion plan:** [report-export-plan.md](./report-export-plan.md) (Phase 2 — printable report, depends on this phase's risk-bucket data)

Existing groundwork found in `src/SBOMViewer.Blazor/Components/DynamicSbomViewer.razor`:
- `ComponentRow` record (line 156) and `ExtractComponentRows()` (line 236) already extract `License` per component (CycloneDX `licenses[].license.id`/`expression`, SPDX `licenseConcluded`). It's missing the CycloneDX `license.name` fallback (e.g. "PostgreSQL License" sample currently falls through as license.id-only — needs a name fallback too).
- `_licenseDist` (line 135/191-197) already builds a top-5 license histogram shown in the Overview tab's "License Distribution" widget — this is reused, not replaced.
- Sidebar tab pattern (lines 26-90) — `_activeTab`, `SetTab()`, `sidebar-item` buttons with icon/label/count badge — new "Compliance" tab follows this exact pattern.
- Stat-card / sev-bar CSS classes already in `wwwroot/css/app.css` are reused for the new tab's summary cards.

---

## File Structure

```
src/SBOMViewer.Blazor/
├── Models/
│   └── LicenseRisk.cs                  # Enum: Permissive, WeakCopyleft, StrongCopyleft, Proprietary, Unknown
├── Services/
│   └── LicenseClassifier.cs            # Static SPDX-id -> LicenseRisk classifier
└── Components/
    └── DynamicSbomViewer.razor         # New "Compliance" tab (modified)

tests/SBOMViewer.Blazor.Tests/
└── Services/
    └── LicenseClassifierTests.cs       # Table-driven classification tests
```

---

## Phase 1: License Compliance Classification

### 1.1 New Model

**`Models/LicenseRisk.cs`**
```csharp
public enum LicenseRisk { Permissive, WeakCopyleft, StrongCopyleft, Proprietary, Unknown }
```

### 1.2 LicenseClassifier Service (`Services/LicenseClassifier.cs`)

Static class, `Classify(string? license) -> LicenseRisk`. Curated lookup table (~50-60 common SPDX identifiers covering licenses likely to appear in real-world .NET/npm/PyPI SBOMs):

- **Permissive**: MIT, Apache-2.0, BSD-2-Clause, BSD-3-Clause, ISC, 0BSD, Unlicense, PostgreSQL, Python-2.0, Zlib
- **Weak-copyleft**: LGPL-2.1, LGPL-3.0, MPL-2.0, EPL-1.0, EPL-2.0, CDDL-1.0/1.1
- **Strong-copyleft**: GPL-2.0, GPL-3.0, AGPL-3.0, SSPL-1.0
- **Unknown**: anything unmatched — custom license name, `NOASSERTION`, empty, or unrecognized SPDX id

Match case-insensitively; strip common suffixes (`-only`, `-or-later`, trailing `+`) before lookup.

### 1.3 Unit Tests

**`tests/SBOMViewer.Blazor.Tests/Services/LicenseClassifierTests.cs`** — table-driven tests covering each bucket, case-insensitivity, and unknown/empty fallback.

### 1.4 Modify `DynamicSbomViewer.razor`

- Extend `ComponentRow` with `LicenseRisk Risk`.
- In `ExtractComponentRows()`: add `license.name` fallback for CycloneDX (when `license.id` is absent), call `LicenseClassifier.Classify(license)` when building each row.
- In `BuildDerivedData()`: compute risk-bucket counts (`_permissiveCount`, `_weakCopyleftCount`, `_strongCopyleftCount`, `_unknownLicenseCount`) alongside the existing `_licenseDist`.
- Add new sidebar tab `"compliance"` (between "Vulnerabilities" and dynamic sections), with a badge showing strong-copyleft + unknown count in a warning color when > 0 (mirrors the vuln count badge pattern at lines 49-52).
- New `RenderComplianceTab()` method (mirrors `RenderComponentsTab()` pattern):
  - 4 stat-cards (Permissive / Weak Copyleft / Strong Copyleft / Unknown) reusing `.stat-card` styling.
  - A flagged list: all components with `Risk` in `{StrongCopyleft, Unknown}`, sortable/searchable like the Components tab (new `_complianceSearch` / `_complianceRiskFilter` local state, same pattern as `_compSearch`).
  - Each row shows component name, version, raw license string, and a colored risk badge (new `.license-risk-badge` CSS classes: `.risk-strong` red, `.risk-weak` amber, `.risk-unknown` gray, `.risk-permissive` green — same visual language as `.sev-badge`).

---

## Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| Curated SPDX lookup table (not full SPDX license list) | Covers the licenses that actually appear in .NET/npm/PyPI SBOMs; full list (~600 entries) is unnecessary bundle weight for marginal coverage gain |
| Unknown bucket as default fallback | Custom/non-SPDX license strings should be flagged for manual review, not silently miscategorized |
| New tab follows existing sidebar/stat-card pattern | Visual consistency with Overview/Components/Vulnerabilities tabs already in `DynamicSbomViewer.razor` |

---

## Verification

1. `dotnet build SBOMViewer.slnx` — compiles with no errors.
2. `dotnet test --filter "FullyQualifiedName~LicenseClassifier"` — new classifier tests pass.
3. `dotnet test` — full suite still passes (existing `SchemaServiceTests`, `PackageExtractorTests`, etc. unaffected since changes are additive).
4. Manual run (`dotnet run --project src/SBOMViewer.Blazor`):
   - Upload `samples/cyclonedx-1.7-full.json` and `samples/spdx-2.2-full.json` — verify the new "Compliance" tab shows correct risk-bucket counts and flags any GPL/AGPL/unknown-licensed components.
5. Re-check the CycloneDX `license.name` fallback against a sample component that only has `license.name` (e.g. "PostgreSQL License" in the existing sample) to confirm it no longer shows as blank/Unknown incorrectly.

---

## Execution Plan (Step-by-Step)

### Step 1: License Risk Model & Classifier
- [ ] Create `src/SBOMViewer.Blazor/Models/LicenseRisk.cs`
- [ ] Create `src/SBOMViewer.Blazor/Services/LicenseClassifier.cs` with curated lookup table
- [ ] Create `tests/SBOMViewer.Blazor.Tests/Services/LicenseClassifierTests.cs`
- [ ] `dotnet test --filter "FullyQualifiedName~LicenseClassifier"`

### Step 2: Wire Classification into Component Extraction
- [ ] Extend `ComponentRow` with `LicenseRisk Risk` in `DynamicSbomViewer.razor`
- [ ] Add CycloneDX `license.name` fallback in `ExtractComponentRows()`
- [ ] Call `LicenseClassifier.Classify()` per row
- [ ] Compute risk-bucket counts in `BuildDerivedData()`

### Step 3: Compliance Tab UI
- [ ] Add `"compliance"` sidebar tab with warning badge
- [ ] Implement `RenderComplianceTab()` — stat-cards + flagged/searchable list
- [ ] Add `.license-risk-badge` CSS variants to `app.css`
- [ ] `dotnet build` + manual test with sample SBOMs

### Step 4: Testing & Polish
- [ ] Run all unit tests: `dotnet test`
- [ ] Manual end-to-end test with `samples/cyclonedx-1.7-full.json` and `samples/spdx-2.2-full.json`
- [ ] Verify CycloneDX `license.name` fallback fix doesn't regress existing license.id-based rows
