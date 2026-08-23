# Data Exports — Implementation Tasks

Task breakdown for [`data-export-plan.md`](data-export-plan.md). Verified against `origin/main` @ `f6e9837`.

The corrections that were listed here have been folded into the plan itself, so the two documents now agree. In summary: `ExportReport()` is in `Home.razor`, not `DynamicSbomViewer.razor`; `sbomDownloadFile` has a second call site; neither tab holds the record type the plan first named; the tabs are not Fluent UI; and `ExportService` is static so needs no DI registration.

The plan has since been reworked to add the **`/compare` diff export**, which it originally omitted and which is now its highest-value piece. Tasks below reflect that.

---

## Task 1 — `Services/ExportService.cs`

New static class. Three formats × three row shapes.

**Row shapes:**
- Components/Compliance: `record ComponentExportRow(ComponentRow Row, string WorstSeverity, int VulnCount)` in the service file — do **not** widen `ComponentRow` itself, it is shared with the diff pipeline (`SbomDiffService`, `ComponentRowExtractor`).
- Vulnerabilities: `record VulnExportRow(string PackageName, string PackageVersion, VulnerabilityEntry Entry)` — matches the tuple `RenderVulnsTab` already builds.
- Diff: **no new shape.** `ComponentChange` already has `Key`, `Name`, nullable `Baseline`/`Current` and `Kind`.

**API:**
```csharp
static string ComponentsCsv(IEnumerable<ComponentExportRow> rows);
static string ComponentsJson(IEnumerable<ComponentExportRow> rows);
static string ComponentsMarkdown(IEnumerable<ComponentExportRow> rows);
static string VulnsCsv(IEnumerable<VulnExportRow> rows);
static string VulnsJson(IEnumerable<VulnExportRow> rows);
static string VulnsMarkdown(IEnumerable<VulnExportRow> rows);
static string DiffCsv(SbomDiff diff);
static string DiffJson(SbomDiff diff, DiffExportMeta meta);
static string DiffMarkdown(SbomDiff diff, DiffExportMeta meta);
```

`DiffExportMeta` carries baseline/current file names, formats and counts — all available on `SbomCompareState`. Only the JSON and Markdown forms need it (envelope and heading line respectively); `DiffCsv` is the bare table.

**CSV (RFC 4180)** — one private `CsvField(string?)` helper: quote if the value contains `,` `"` `\r` or `\n`; double embedded quotes; `null` → empty. Header row always written. Line terminator `\r\n`.

**JSON** — `JsonSerializer.Serialize` with `WriteIndented = true`. Vulns JSON regroups the flat rows back into the nested per-package shape. Components JSON is flat. Diff JSON is a metadata envelope + the four buckets.

**Markdown** — pipe table, header + `---` separator row, escape `|` as `\|` in cells, collapse CR/LF in cell values to a space (a newline breaks a pipe table). CVE summaries are the realistic source of both. Diff Markdown is grouped: `### <Bucket>` + table per **non-empty** bucket, after a `Comparing <baseline> → <current>` line.

**Diff uniform schema** — all four buckets share one table, so they can sit in one file:
`Change | Name | Purl | Baseline Version | Current Version | Baseline License | Current License | Baseline Risk | Current Risk`.
Do **not** mirror `Compare.razor`'s per-tab columns (`:154-208`), which differ by tab. `Purl` from `Current ?? Baseline`, matching the `row` local at `Compare.razor:179`.

**Empty collection** — every method returns headers only (CSV/Markdown), `[]` (JSON), or an envelope with empty buckets (diff JSON). Never empty string, never throw.

Render `null` as an **empty cell**, not `—`. That em dash is a display affordance from `Compare.razor:269`; a spreadsheet wants blank.

## Task 2 — `wwwroot/index.html`

Add a third parameter to `sbomDownloadFile`:

```js
window.sbomDownloadFile = function (filename, content, mimeType) {
  var blob = new Blob([content], { type: mimeType || 'application/json' });
  ...
};
```

`||` rather than a default parameter so the two existing two-arg call sites (`Home.razor:160`, and any future) are unaffected.

## Task 3 — `Components/ExportMenu.razor`

Shared control, used from both `DynamicSbomViewer.razor` and `Compare.razor` — both pages already share the `.toolbar` / `.btn-ghost` CSS, so duplicating the markup would be waste. Dropped into `.toolbar-right` beside the existing count label.

- Parameters: a slug, and a `Func<string, (string Content, string Mime, string Ext)>` or three callbacks — whichever keeps the call sites readable.
- Closed state: a `btn-ghost` "↓ Export" button. Open state: CSV / JSON / Markdown.
- Backing field holding the open slug (`string?`) so only one menu is open at a time; close on selection.
- CSS in `wwwroot/css/app.css` alongside the existing toolbar rules.

## Task 4 — Wire the three viewer tabs

Each already materialises its filtered, sorted list immediately before returning the fragment — serialise **that** variable, not the source collection.

| Renderer | Line | Collection | Filename suffix |
|---|---|---|---|
| `RenderComponentsTab` | 580 | `list` (search + severity filter + sort applied) | `.components` |
| `RenderComplianceTab` | 677 | `list` (flagged-only + search + risk filter) | `.compliance` |
| `RenderVulnsTab` | 792 | `vulns` (search + severity filter applied) | `.vulnerabilities` |

For Components/Compliance, project to `ComponentExportRow` using `_vulnWorstSev.GetValueOrDefault(r.Name, "NONE")` and `_vulnCounts.GetValueOrDefault(r.Name, 0)` — the same lookups the table cells use.

Vulnerabilities: guard the control when `ChatState.VulnResults is null` (pre-scan) — that branch returns early before `vulns` exists, so there is nothing to export and the toolbar isn't rendered anyway. Verify no export button appears in the empty state.

**Filename** — helper `ExportFileName(string tabSlug, string ext)`:
`Path.GetFileNameWithoutExtension(SbomState.FileName)` (fallback `"sbom"`) + `"." + tabSlug + "." + ext`. e.g. `myapp-sbom.components.csv`. Strip the extension — `SbomState.FileName` carries `.json`, so a naive concat yields `myapp-sbom.json.components.csv`.

**MIME** — `text/csv`, `application/json`, `text/markdown`.

## Task 5 — Wire `/compare` (the high-value one)

`Pages/Compare.razor`, toolbar at `:143-145`, beside the `@Visible.Count of @Rows.Count` label.

- **Add `@inject IJSRuntime JS`** — the page has no JS interop today, so the injection is missing entirely.
- Export the **whole diff** (`CompareState.Diff`), not the active tab and not the `Visible` search filter. Rationale is in the plan: the four buckets are one logical document, the row count is small, and the uniform schema's `Change` column preserves the slicing.
- Build `DiffExportMeta` from `CompareState` — `BaselineFileName`, `CurrentFileName`, `BaselineFormat`, `CurrentFormat`, `BaselineComponentCount`, `CurrentComponentCount`, plus `Diff.UnchangedCount`.
- Filename: `<baselineStem>-vs-<currentStem>.diff.<ext>`, `Path.GetFileNameWithoutExtension` with a fallback on each side.
- The toolbar only renders inside the `!diff.IsIdentical` branch (`:97-146`), so no control appears when the documents match. Correct — nothing to export. Confirm it.

## Task 6 — `tests/SBOMViewer.Blazor.Tests/Services/ExportServiceTests.cs`

New test class, mirroring the existing service-test layout.

- CSV field containing a comma: license `(MIT OR Apache-2.0), see NOTICE` → field is quoted, column count preserved.
- CSV field containing a double quote: CVE summary with `"` → quote doubled, field quoted.
- CSV field containing CR/LF → quoted, and the row parses back as one record.
- **Round-trip** — parse the produced CSV with a real parser and assert cell values match the inputs exactly. Add `CsvHelper` to the test project, or write a ~30-line RFC 4180 reader in the test file; do not assert on raw string equality, that tests the formatter against itself.
- Empty collection, each of the nine methods: CSV/Markdown → header row present and exactly one/two lines; JSON → `[]` or an envelope with empty buckets.
- Markdown: `|` in a cell is escaped; a value with a newline does not break the row.
- Vulns JSON regroups: two entries for the same package produce one object with two nested vulnerabilities.
- Vulns CSV/Markdown flatten: two entries for the same package produce two rows.
- Null handling: `Purl`, `Summary`, `Severity`, `CvssScore`, `FixedVersion` are all nullable — one test per shape with all-null optionals, asserting a **blank** cell, not `—`.
- **Diff, one-sided changes**: an `Added` change (null `Baseline`) and a `Removed` change (null `Current`) each produce a full-width row with the absent side blank. This is the diff export's equivalent of the CSV-escaping risk — the uniform schema means every row must fill every column regardless of `Kind`.
- **Diff Markdown grouping**: a diff with an empty bucket omits that section entirely rather than emitting a headerless table.
- **Diff row count**: `DiffCsv` line count equals `diff.TotalChanges` + 1 header.

## Task 7 — Manual / E2E verification

- Upload a sample, filter the Components tab, export CSV, confirm the row count matches the on-screen count label.
- Open a CSV containing a comma-bearing license in a spreadsheet; confirm columns do not shift.
- **Diff against two `samples/` fixtures in different formats** — cross-format matching (purl pass then name pass) is `SbomDiffService`'s subtlest behaviour, so it is the case most likely to surface a wrong-looking export.
- Paste the diff Markdown into a PR description and confirm it renders as grouped tables. This is the artifact that justifies the Markdown format existing.
- Regression: "Export Report" (print-to-PDF) and the raw-JSON download in the top nav both still work after the `sbomDownloadFile` signature change. **These are the only two things Task 2 can break.**
- Optional E2E: Playwright can intercept the download and assert the filename and first line. Worth it for the extension-stripping and `-vs-` filename logic. `CompareTests.cs` already drives the two-slot upload, so the diff case has a home; `FileUploadTests.cs` or a new `ExportTests.cs` for the viewer tabs. Skip if download interception proves flaky in CI.

## Suggested order

1 → 6 (serialisers test-first; that is where the real risk is) → 2 → 3 → **5** → 4 → 7.

Task 5 (`/compare`) before Task 4 (viewer tabs), against UI order but with value: it is the piece with no existing export path at all, and it exercises the shared menu from the simpler of the two pages.

Tasks 1/6 and 2 are independent and can land as separate commits. A reasonable first PR is 1 + 6 + 2 + 3 + 5 — the diff export end to end — with the viewer tabs following.
