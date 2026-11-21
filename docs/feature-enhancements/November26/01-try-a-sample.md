# Try a Sample — Load a Demo SBOM Without a File

> **November 2026 release — feature 1 of 4.** Next: [02 · Quality Scorecard](02-quality-scorecard.md). Epic: [README](README.md).

## Context

The landing page (`Components/UploadFile.razor`) offers two things: drop a file, or compare two files. Both assume the visitor already has an SBOM on disk. Someone who arrived from a search for "SBOM viewer" to see what the tool does has nothing to drop, and leaves without ever seeing the dashboard, the scan, or the compliance tab.

A one-click sample removes that dead end. It is the smallest of the three planned features and should ship first — the other features in this release (quality scorecard, recents, scan history) are all easier to demo and to test manually once a rich sample exists.

### What already exists

- `samples/` at the repo root holds nine fixtures, but they are **test fixtures, not demos**: 1–8 KB, a handful of components, and nothing in them is known-vulnerable. Scanning one produces an empty result, which is the worst possible first impression of the flagship feature. They are also outside `wwwroot`, so they are not deployed.
- `SbomLoader.Load(string, string)` already parses a string **without** saving to the recents store. That is exactly the behaviour a sample needs — a demo file must not evict one of the user's two real recents.
- `staticwebapp.config.json` already excludes `*.json` from the SPA fallback, so a JSON file under `wwwroot` is served as-is.

## Requirements

- One click on the landing page opens a realistic sample in the viewer.
- The sample exercises every tab: components, a scan that finds something (including at least one KEV-listed CVE), mixed license risk, and dependencies.
- Loading the sample does not touch the recents store.
- A deep link (`/?sample=1`) opens the sample directly, so the README, the GitHub repo description and social posts can link straight into a loaded dashboard.

## Approach

### The sample file

Add `wwwroot/samples/demo-storefront.cdx.json` — a hand-curated CycloneDX 1.6 document for a fictional application, roughly 30–40 components. CycloneDX 1.6 because it carries the most metadata (lifecycles, hashes, suppliers), which the quality scorecard will want to show off.

Curate it deliberately:

| Needs | So that |
|---|---|
| A few components at known-vulnerable versions, e.g. `log4j-core 2.14.1` (Log4Shell, KEV-listed) | The scan finds results and the KEV indicator lights up |
| A mix of ecosystems (Maven, npm, PyPI, NuGet) | Purl handling and ecosystem mapping are visibly exercised |
| At least one GPL/AGPL component and one with no license | The Compliance tab has flagged rows |
| A populated `dependencies` array | The dependency section is not empty |
| Mostly complete metadata, with a few deliberate gaps (some components missing hashes or supplier) | The quality scorecard shows a realistic "mostly passes, here is what to fix" result rather than 100% or 0% |

Keep it under ~50 KB. Every component must be a real package at a real version — an invented purl would return nothing from OSV and make the demo look broken.

**The scan result will drift.** New CVEs get published against old versions, so never assert exact vulnerability counts against this file in tests — assert "at least one" and "Log4Shell is KEV-flagged".

### Loading

`Program.cs:16` registers `new HttpClient()` with **no `BaseAddress`**, so a relative URL will throw. Build the absolute URL from `NavigationManager.BaseUri`:

```csharp
var url  = new Uri(new Uri(Nav.BaseUri), "samples/demo-storefront.cdx.json");
var json = await Http.GetStringAsync(url);
var result = SbomLoader.Load(json, "demo-storefront.cdx.json");
```

Then set `SbomState` exactly as `OpenRecent` does (`UploadFile.razor:132-136`), including `ChatState.ClearVulnerabilities()`. `OpenRecent`, `OnFileChanged` and this new path all end in the same four `SbomState` assignments — extract a small private `Show(SbomLoadResult)` helper rather than adding a third copy.

A failed fetch (offline, blocked) sets `_errorMessage` and leaves the landing page as it was.

### UI

A single text link under the two cards, above the Recent row:

```
No SBOM handy?  Try a sample →
```

**It must sit outside `.dropzone`.** The `InputFile` overlay covers the entire dropzone (`UploadFile.razor:28`) and would swallow the click — the same reason the recent-file chips on the compare page sit outside it.

In the viewer, show a small "Sample" chip next to the file name so nobody mistakes demo data for their own. Add `bool IsSample` to `SbomState`, set by the sample path and reset by every other load and by `Clear()`.

### Deep link

In `UploadFile.OnInitializedAsync`, if the query string contains `sample=1` and no document is loaded, run the same load. Then strip the parameter with `Nav.NavigateTo("/", replace: true)` so that "New file" does not immediately reload the sample.

### Not in this plan

- **A sample comparison pair** ("Try a sample comparison" on `/compare`, with a v2 of the same app). Cheap follow-up once the first file exists; needs a second curated file.
- **Fetching an SBOM from a GitHub repo URL.** Deferred — needs a CORS and rate-limit check on the GitHub dependency-graph API first.

## Files

| File | Change |
|------|--------|
| `wwwroot/samples/demo-storefront.cdx.json` | New — curated demo document |
| `Components/UploadFile.razor` | "Try a sample" link, sample load, `?sample=1` handling, shared `Show(...)` helper |
| `Services/SbomState.cs` | `IsSample` flag |
| `Components/DynamicSbomViewer.razor` or `Pages/Home.razor` | "Sample" chip beside the file name |
| `wwwroot/css/app.css` | Link and chip styling |
| `README.md` | Link to `https://sbomviewer.com/?sample=1` |

## Tasks

1. Curate the sample file; load it through the normal upload path and check every tab by hand.
2. Sample link + load path + `Show(...)` refactor.
3. `IsSample` flag and chip.
4. `?sample=1` deep link.

## Verification

- Unit test: the sample file passes `SbomLoader.Load` — detection, validation and schema build. Read it from the repo path so a broken edit to the sample fails CI rather than production.
- Unit test: `PackageExtractor` returns a package with a purl for every component in the sample, so the scan has something to send.
- E2E (`FileUploadTests.cs` is the closest pattern): click "Try a sample" → dashboard renders → Sample chip is visible.
- E2E: after loading the sample, return to the landing page and confirm the Recent row is still absent — the sample must not be stored.
- E2E: navigate to `/?sample=1` → dashboard renders, and the URL no longer contains the parameter.
- Manually: scan the sample and confirm results appear with at least one KEV indicator.
- Manually: block the sample URL in dev tools and confirm a readable error, not a blank page.
