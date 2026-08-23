# Data Exports — CSV / JSON / Markdown

## Context

The only export path today is `ExportReport()` in `Components/DynamicSbomViewer.razor:319`, which calls `window.sbomPrintReport()` → `window.print()` against the hidden `.print-report` section. That produces a PDF suitable for filing, and nothing else.

Users want the component and vulnerability lists in a spreadsheet, a Jira ticket, or a compliance attachment. Each tab already sits on a clean in-memory collection — `ComponentRow` for Components and Compliance, `VulnerabilityResult` for Vulnerabilities — so the data is there; only the serialisation and download are missing.

This is purely additive. The existing print-to-PDF path stays.

## Requirements

- Export the Components, Vulnerabilities, and Compliance tabs as CSV, JSON, and Markdown.
- Exports reflect the tab's current filter/search state, not the unfiltered document — otherwise the export contradicts what the user sees.
- Correct CSV escaping. License expressions contain commas (`(MIT OR Apache-2.0)`) and CVE summaries contain quotes; naive joining corrupts the file.

## Approach

### Serialisation

New `Services/ExportService.cs` with methods over `IEnumerable<ComponentRow>` and `IEnumerable<VulnerabilityResult>`, returning strings:

- **CSV** — RFC 4180: wrap a field in double quotes if it contains a comma, quote, CR or LF; escape embedded quotes by doubling them. Write a header row. This is the part that must be unit-tested.
- **JSON** — `System.Text.Json` serialisation of the row records. Records are already flat, so no custom converters are needed.
- **Markdown** — pipe table, escaping `|` in cell values. Good for pasting into a ticket or PR description.

`VulnerabilityResult` holds a nested list of `VulnerabilityEntry`, so its CSV/Markdown form should be flattened one row per (package, CVE) pair. The JSON form can keep the nested shape.

### Download

**Reuse the existing JS helper** — `window.sbomDownloadFile(filename, content)` at `wwwroot/index.html:110`. It already builds a Blob, creates an object URL, clicks a synthetic anchor, and revokes the URL after a timeout.

One change needed: it hardcodes `type: 'application/json'` in the Blob constructor. Add an optional MIME-type parameter (defaulting to `application/json` so the existing call site keeps working) so CSV downloads as `text/csv` and Markdown as `text/markdown`.

### UI

Add an export control to each tab renderer in `Components/DynamicSbomViewer.razor` — `RenderComponentsTab`, `RenderVulnsTab`, `RenderComplianceTab`. The filtered row collections are already materialised inside those methods, so the control should serialise the same collection the table binds to.

A small format menu (CSV / JSON / Markdown) per tab is preferable to three separate buttons. Name files predictably from `SbomState.FileName` — e.g. `myapp-sbom.components.csv`.

## Files

| File | Change |
|------|--------|
| `Services/ExportService.cs` | New — CSV / JSON / Markdown serialisers |
| `Components/DynamicSbomViewer.razor` | Export control in the three tab renderers |
| `wwwroot/index.html` | Add MIME-type parameter to `sbomDownloadFile` |
| `Program.cs` | Register `ExportService` |

## Verification

- Unit-test `ExportService` CSV output against a license expression containing a comma (`(MIT OR Apache-2.0), see NOTICE`) and a CVE summary containing a double quote. Round-trip the result through a CSV parser in the test to prove it is well-formed.
- Test the empty-collection case — exporting a tab with no rows should yield a header row, not an empty file or a crash.
- Manually download from each tab and open the CSV in a spreadsheet; confirm columns do not shift.
- Confirm the existing "Export Report" print path still works after the `sbomDownloadFile` signature change.
