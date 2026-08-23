# Data Exports — CSV / JSON / Markdown

## Context

The only export paths today are:

- `ExportReport()` (`Pages/Home.razor:120`) → `window.sbomPrintReport()` → `window.print()` against the hidden `.print-report` section (`Components/DynamicSbomViewer.razor:140`). A PDF suitable for filing, and nothing else.
- `DownloadSbom()` (`Pages/Home.razor:160`) → the raw uploaded JSON, byte-for-byte.

Between them they miss the thing the app actually produces. The raw download returns the file the user already had, with **zero enrichment**. The PDF *has* the enrichment but is not machine-readable. Everything this app adds on top of the source document — `LicenseClassifier` risk bands, OSV scan results, per-component worst severity and CVE counts, and the component-level diff between two documents — can currently leave the browser only as a PDF or not at all.

That is the gap. This plan closes it with CSV / JSON / Markdown exports, and it is purely additive: both existing paths stay.

## Priority

Ordered by value, which is **not** the order the tabs appear in the UI:

1. **The `/compare` diff** — highest value, currently zero export paths. "What changed between these two releases" is the most release-note-shaped, ticket-shaped output the app produces, and `SbomDiff` is already the right shape and the right size (tens of rows, not five hundred).
2. **Vulnerabilities** — feeds ticket creation and tracking.
3. **Components** — the enriched inventory: license risk + CVE counts, which exist nowhere else in machine-readable form.
4. **Compliance** — a filtered subset of Components. Nearly free once Components works, but not a fourth feature.

An earlier draft of this plan omitted the diff entirely; it was written in the same commit that shipped `/compare` (`af7c3be`) and never revised for it.

Per format: **CSV is the feature.** JSON is close to free (one `JsonSerializer.Serialize` call over the same rows) and is what a CI job would consume, so it is included. Markdown is narrower — a 500-row pipe table pasted into a ticket is unreadable — but it is the right format for the two places where output is naturally small: a filtered CVE list, and the diff. It ships everywhere anyway because the menu is one shared component and the escaping helper is shared with CSV; just don't expect it to be used on an unfiltered Components tab.

## Requirements

- Export the Components, Vulnerabilities and Compliance tabs, and the `/compare` diff, as CSV, JSON and Markdown.
- Viewer-tab exports reflect the tab's current filter/search state, not the unfiltered document — otherwise the export contradicts what the user sees. The diff is a deliberate exception; see [Diff export](#diff-export).
- Correct CSV escaping. License expressions contain commas (`(MIT OR Apache-2.0)`) and CVE summaries contain quotes and newlines; naive joining corrupts the file.

## Approach

### Serialisation

New `Services/ExportService.cs`, returning strings. It is pure and stateless, so make it `static` — no DI registration needed.

**The service must not take `IEnumerable<ComponentRow>` and `IEnumerable<VulnerabilityResult>`.** Neither matches what the tabs hold at the point the filter has been applied, and exports must reflect the filtered state:

- `RenderVulnsTab` flattens to `List<(string PackageName, string PackageVersion, VulnerabilityEntry v)>` *before* applying the search and severity filters. By the time the filtered collection exists, the nested `VulnerabilityResult` shape is gone.
- `RenderComponentsTab` renders two columns that are not on `ComponentRow` — worst severity and CVE count, read from the `_vulnWorstSev` / `_vulnCounts` dictionaries (`DynamicSbomViewer.razor:267-268`). Exporting `ComponentRow` alone silently drops columns the user is looking at.

So define two row shapes in the service file:

```csharp
record ComponentExportRow(ComponentRow Row, string WorstSeverity, int VulnCount);
record VulnExportRow(string PackageName, string PackageVersion, VulnerabilityEntry Entry);
```

Wrap rather than widen `ComponentRow` — that record is shared with `ComponentRowExtractor` and the diff pipeline (`SbomDiffService`), and adding vulnerability fields to it would leak scan state into the compare page.

The diff needs **no** new row shape: `ComponentChange` (`Models/SbomDiff.cs`) already carries `Key`, `Name`, nullable `Baseline` / `Current` rows and `Kind`.

Three serialisers per shape:

- **CSV** — RFC 4180: wrap a field in double quotes if it contains a comma, quote, CR or LF; escape embedded quotes by doubling them. Write a header row, terminate lines with `\r\n`. This is the part that must be unit-tested.
- **JSON** — `System.Text.Json`, `WriteIndented = true`. `ComponentExportRow` is flat. The vulnerability form keeps the nested per-package shape, which means regrouping the flat rows by package on the way out.
- **Markdown** — pipe table, escaping `|` in cell values **and collapsing CR/LF to a space** — a newline inside a cell breaks a pipe table, and CVE summaries are a realistic source of both.

CSV and Markdown flatten vulnerabilities one row per (package, CVE) pair.

All optional fields are nullable — `ComponentRow.Purl`, and `Summary` / `Severity` / `CvssScore` / `FixedVersion` on `VulnerabilityEntry`, plus both sides of every `ComponentChange`. Render `null` as an empty cell in CSV, `—` is *not* appropriate in an export (it is a display affordance from `Compare.razor:269`; a spreadsheet wants an empty cell).

### Diff export

The four buckets — Added, Removed, VersionChanged, LicenseChanged — export as **one artifact covering the whole diff**, not one bucket at a time. This is a deliberate divergence from the "respect the current filter" requirement, for two reasons:

- The four buckets are one logical document. A release note or a ticket wants all of it; exporting "Added" alone forces four downloads and a manual merge.
- The reason that requirement exists — that 500 unfiltered rows are unusable — does not apply. A diff is tens of rows, and the uniform schema carries a `Change` column, so the user can re-slice in the spreadsheet. That is what a spreadsheet is for.

`Compare.razor` renders **different columns per tab** (`:154-208`) — version shows From/To, license shows From/To plus Version, added/removed show Version/License. The export does not mirror that. It uses one uniform schema so all four buckets sit in one table:

| Change | Name | Purl | Baseline Version | Current Version | Baseline License | Current License | Baseline Risk | Current Risk |
|---|---|---|---|---|---|---|---|---|

`Purl` comes from `Current ?? Baseline`, matching the `row` local at `Compare.razor:179`.

- **CSV / JSON** — all four buckets. JSON additionally wraps the buckets in a metadata envelope: baseline and current file names, formats, component counts and `UnchangedCount`, all available on `SbomCompareState`. That envelope is what makes the JSON form useful to a CI job asserting "no new strong-copyleft licenses".
- **Markdown** — grouped, with an `###` heading and its own table per non-empty bucket, preceded by a `Comparing <baseline> → <current>` line. Omit empty buckets rather than emitting an empty table. **This is the artifact that justifies Markdown existing at all** — it pastes into a release note or PR description as-is.

The compare toolbar only renders when `!diff.IsIdentical` (`Compare.razor:97-146`), so there is no export control when the two documents match. That is correct — there is nothing to export.

### Download

**Reuse the existing JS helper** — `window.sbomDownloadFile(filename, content)` at `wwwroot/index.html:110`. It already builds a Blob, creates an object URL, clicks a synthetic anchor, and revokes the URL after a timeout.

One change needed: it hardcodes `type: 'application/json'` in the Blob constructor. Add a third MIME-type parameter, defaulted with `mimeType || 'application/json'`, so CSV downloads as `text/csv` and Markdown as `text/markdown`.

There are **two** existing two-argument call sites, not one — `ExportReport()` is unrelated (it calls `sbomPrintReport`), but `DownloadSbom()` at `Pages/Home.razor:160` downloads the raw SBOM JSON through this helper. Both must keep working untouched; this is the only thing the signature change can break.

`Compare.razor` does **not** currently inject `IJSRuntime` — it has no JS interop at all. Add `@inject IJSRuntime JS`.

### UI

One shared format-menu `RenderFragment` (CSV / JSON / Markdown), preferable to three separate buttons, dropped into the existing `.toolbar-right` beside the count label. A `string?` backing field holds the open menu's slug so only one is open at a time.

Note these toolbars are **not** Fluent UI despite the project convention — they are hand-rolled `.toolbar` / `.btn-ghost` / `.filter-btn` markup with styles in `wwwroot/css/app.css`. Match that, not `FluentMenuButton`. Because both pages share those CSS classes, the menu markup can be lifted into a small `Components/ExportMenu.razor` and used from both `DynamicSbomViewer.razor` and `Compare.razor` rather than duplicated.

Each call site already materialises its filtered collection immediately before rendering — serialise **that** variable, not the source collection:

| Page / renderer | Line | Collection | Filename suffix |
|---|---|---|---|
| `DynamicSbomViewer.RenderComponentsTab` | 580 | `list` — search + severity filter + sort | `.components` |
| `DynamicSbomViewer.RenderComplianceTab` | 677 | `list` — flagged-only + search + risk filter | `.compliance` |
| `DynamicSbomViewer.RenderVulnsTab` | 792 | `vulns` — search + severity filter | `.vulnerabilities` |
| `Compare.razor` | 143 | `CompareState.Diff` — all four buckets, unfiltered | `.diff` |

`RenderVulnsTab` returns early when `ChatState.VulnResults is null` (pre-scan) and while a scan is running, so the toolbar — and the export control with it — is absent in those states. That is correct; just confirm it.

**Filenames.** For the viewer tabs, `Path.GetFileNameWithoutExtension(SbomState.FileName)` (fallback `"sbom"`) + suffix + extension, e.g. `myapp-sbom.components.csv`. **Strip the extension** — `FileName` carries `.json`, so a naive concat yields `myapp-sbom.json.components.csv`. For the diff, join both stems: `<baseline>-vs-<current>.diff.md`, with the same fallback on either side.

## Files

| File | Change |
|------|--------|
| `Services/ExportService.cs` | New — row shapes + CSV / JSON / Markdown serialisers for components, vulns and diff |
| `Components/ExportMenu.razor` | New — shared format-menu control |
| `Components/DynamicSbomViewer.razor` | Export control in the three tab renderers |
| `Pages/Compare.razor` | Export control in the toolbar; inject `IJSRuntime` |
| `wwwroot/css/app.css` | Format-menu styles, alongside the existing toolbar rules |
| `wwwroot/index.html` | Add MIME-type parameter to `sbomDownloadFile` |
| `tests/SBOMViewer.Blazor.Tests/Services/ExportServiceTests.cs` | New — escaping, round-trip, empty, null, grouping |

No `Program.cs` change — `ExportService` is static.

## Verification

- Unit-test CSV output against a license expression containing a comma (`(MIT OR Apache-2.0), see NOTICE`), a CVE summary containing a double quote, and a value containing CR/LF.
- **Round-trip through a real CSV parser** and assert cell values equal the inputs. Add `CsvHelper` to the test project or write a short RFC 4180 reader in the test file — do not assert on an expected raw string, that tests the formatter against itself.
- Empty-collection case for every method — CSV/Markdown yield headers only, JSON yields `[]` or an envelope with empty buckets. Never an empty string, never a crash.
- Markdown: `|` in a cell is escaped, and a cell containing a newline does not break the row.
- Vulnerability grouping: two entries for one package produce one nested object in JSON but two rows in CSV/Markdown.
- Null optionals: one case per row shape with every nullable field null. For the diff specifically, an `Added` change (null `Baseline`) and a `Removed` change (null `Current`) must both produce a full-width row with the absent side blank.
- Diff Markdown: a diff with an empty bucket omits that section rather than emitting a headerless table.
- Diff round-trip on a real pair — compare two `samples/` fixtures **in different formats** (the diff's cross-format matching is its subtlest behaviour) and confirm the CSV row count equals `TotalChanges`.
- Manually download from each tab; open the CSV in a spreadsheet; confirm columns do not shift and the row count matches the on-screen count label after filtering.
- Regression: both the "Export Report" print path and the raw-JSON download still work after the `sbomDownloadFile` signature change.
- Optional E2E (`tests/SBOMViewer.E2E.Tests`): Playwright can intercept the download and assert the filename and first line — worth it for the extension-stripping and `-vs-` filename logic. `CompareTests.cs` already drives the two-slot upload, so the diff case has a home. Skip if download interception is flaky in the CI container.
