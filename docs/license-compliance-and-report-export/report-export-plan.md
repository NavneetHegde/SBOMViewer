# Plan: Printable Report Export — SBOMViewer

## Context

SBOMViewer gets ~10 visits/day and is a 100% client-side static app (no backend, no accounts). This plan adds a "Export Report" feature: a clean, printable report combining SBOM summary, vulnerability findings, and license compliance results, exported via the browser's native print dialog (save-as-PDF). This turns the tool into something usable as an actual audit deliverable — the kind of feature that justifies a paid/pro tier later — while staying fully client-side (no new backend, no new npm/CDN dependencies, no accounts).

**Release:** `license-compliance-report` (Phase 2)
**Depends on:** [license-compliance-plan.md](./license-compliance-plan.md) (Phase 1 — provides the license risk-bucket data and `LicenseRisk` classification used in the report)

Existing groundwork found in `src/SBOMViewer.Blazor/`:
- `Components/DynamicSbomViewer.razor` already computes everything the report needs: `_componentRows`, `ChatState.VulnResults`, `_generatedDate`, plus the new license risk-bucket counts from Phase 1.
- JS interop pattern: flat `window.sbomXxx` functions in `wwwroot/index.html` (e.g. `sbomDownloadFile`, lines 110-118), called via `JS.InvokeVoidAsync(...)`. A new `sbomPrintReport` function follows this exact pattern.
- No `@media print` rules exist yet in `wwwroot/css/app.css` (488 lines, checked).

---

## File Structure

```
src/SBOMViewer.Blazor/
├── Components/
│   └── DynamicSbomViewer.razor         # New print-report block + Export button (modified)
└── wwwroot/
    ├── index.html                      # New window.sbomPrintReport function (modified)
    └── css/app.css                     # New @media print rules (modified)
```

---

## Phase 2: Printable Report Export

### Approach

Use the browser's native print-to-PDF (no jsPDF/html2canvas) — zero bundle size cost, works offline, matches the "stay client-side only" constraint. Tradeoff: less pixel-perfect than a JS PDF library, acceptable for a v1 audit-style report.

### 2.1 Modify `DynamicSbomViewer.razor`

- Add an "Export Report" button near the SBOM meta widget (sidebar, line ~76-89) — calls `JS.InvokeVoidAsync("sbomPrintReport")`.
- Add a hidden-by-default `<div class="print-report">` block (rendered always, visibility controlled purely by CSS so print captures full data regardless of active tab) containing:
  - Header: filename, format/spec version, generated date, scan date (reuse existing fields: `FileName`, `SbomState.DetectedFormat`, `_generatedDate`)
  - Summary stats (component count, total vulns by severity, license risk bucket counts)
  - Full vulnerability list (package, CVE id, severity, CVSS, fixed version) — reuse data already in `ChatState.VulnResults`, no new extraction needed
  - Full license compliance list — components flagged `StrongCopyleft`/`Unknown` (from Phase 1), plus the full `_licenseDist` table
- This block reuses already-computed fields (`_componentRows`, `ChatState.VulnResults`, risk-bucket counts) — no duplicate data fetching.

### 2.2 Modify `wwwroot/index.html`

Add next to `sbomDownloadFile` (line ~118), following the same flat-function convention:
```js
window.sbomPrintReport = function () { window.print(); };
```

### 2.3 Modify `wwwroot/css/app.css`

- Add `.print-report { display: none; }` for screen.
- Add `@media print { .dashboard .sidebar, .stat-card button, .btn-ghost, .btn-primary { display: none; } .print-report { display: block; } ... }` — hide the live dashboard chrome, show only the print-report block, add `page-break-inside: avoid` on report sections and basic black-on-white report typography.

---

## Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| Browser print-to-PDF (not jsPDF/html2canvas) | Zero added bundle size, no new dependency, works fully offline — matches client-side-only constraint |
| Print-report block always rendered, CSS-gated | Print should capture the full report regardless of which tab is currently active on screen |

---

## Verification

1. `dotnet build SBOMViewer.slnx` — compiles with no errors.
2. Manual run (`dotnet run --project src/SBOMViewer.Blazor`):
   - Upload `samples/cyclonedx-1.7-full.json` or `samples/spdx-2.2-full.json`, run a vulnerability scan, then go to the Compliance tab (from Phase 1).
   - Click "Export Report" — verify the browser print dialog opens showing a clean report (no sidebar/buttons), save as PDF, confirm vulnerability and license sections render correctly with page breaks.
3. Test in both Chrome and Edge print preview to confirm consistent layout.

---

## Execution Plan (Step-by-Step)

### Step 1: Print Report Block
- [ ] Add `<div class="print-report">` block to `DynamicSbomViewer.razor` with header, summary, vuln list, license list
- [ ] Add "Export Report" button wired to `sbomPrintReport` JS interop
- [ ] Add `window.sbomPrintReport` to `index.html`

### Step 2: Print Styles
- [ ] Add `@media print` rules to `app.css` (hide chrome, show report, page breaks)
- [ ] Manual test: print preview / save-as-PDF in Chrome and Edge

### Step 3: Testing & Polish
- [ ] `dotnet build` + full manual end-to-end test with sample SBOMs (scan + export)
