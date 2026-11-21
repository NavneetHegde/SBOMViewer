# Scan History — "What's New Since Your Last Scan"

> **November 2026 release — feature 4 of 4.** Previous: [03 · Recents Identity and Caps](03-recents-identity-and-caps.md). Epic: [README](README.md).

## Context

An SBOM does not change after release, but the vulnerabilities known against it do. A document that scanned clean in September can have a KEV-listed CVE in November. Today the app has no memory of a previous scan, so a returning user re-scans, sees "47 vulnerabilities", and has to work out for themselves whether that is the same 47 as last time.

That is also why there is little reason to come back. The recents store brings the *file* back; nothing brings back the *question*. Remembering the last scan and showing the difference gives the app a recurring job: **"re-check this release — has anything new come out?"** — with no login, no backend and no notifications service.

### Depends on feature 3

This plan assumes [03 · Recents Identity and Caps](03-recents-identity-and-caps.md) has shipped. It relies on three things from it:

- an entry is identified by **content hash**, so history survives a re-drop or a rename and is never overwritten by a different file with the same name;
- up to **20 entries** are remembered, the newest 5 with content, so history is not limited to two files;
- `sbomRecentRemove(id)` and the two-stage eviction exist, and this plan extends both to cascade to scan snapshots.

Because a regenerated SBOM is a *new* entry under those rules, history never mixes newly disclosed CVEs with newly added components. That second question is what `/compare` is for.

### What already exists

- **Scan results** live only in `ChatState.VulnResults` and are wiped by `ClearVulnerabilities()` on every load.
- **Nothing links the open document to its recents entry.** `sbomRecentSave` resolves `true`, not the entry id, and `SbomState` holds no entry id. `OpenRecent` knows `item.Id` but discards it.

## Requirements

- After a scan, persist a compact snapshot of the result against the file it belongs to.
- On a later scan of the same file, show what is new: newly reported vulnerabilities and vulnerabilities newly added to CISA KEV.
- On the landing page, show when each remembered file was last scanned and offer a one-click re-check.
- Scanning stays **user-initiated**. Nothing is sent to OSV.dev until the user clicks.
- All of it best-effort, like the rest of the recents store: a storage failure means "no history", never a failed scan.

## Approach

### Why not scan automatically on return

The obvious version of this feature scans recents in the background when the page loads and shows a badge. Do not build that. It would send package names to OSV.dev without the user asking, on a tool whose whole pitch is that it does not do things with your data behind your back — and the project convention is explicit that scanning is user-initiated. A visible "Last scanned 23 days ago · Re-check" button gets nearly all of the value and none of the trust cost.

### Link the open document to its entry

- `sbomRecentSave` resolves the **entry id** instead of `true`; `RecentSbomStore.SaveAsync` returns `long?` (null on failure).
- `SbomLoadResult` gains `long? RecentId`, populated by `LoadAsync`. The `Load(string, string)` overload leaves it null.
- `SbomState` gains `long? RecentId`, set by the upload path and by `OpenRecent`, cleared by `Clear()`.

Documents that arrive via the viewer ↔ compare hand-off or the sample link have no `RecentId`, so they get no history. That is the correct outcome for the sample, and an acceptable limitation for the hand-off — carrying the id through `OpenInViewer()` is a possible follow-up.

### Storage

Bump `DB_VERSION` to 2 and add a third object store, `scans`, keyed by entry id. `onupgradeneeded` already guards each `createObjectStore` with a `contains` check, so adding one more is safe for existing users.

```js
{ id: <entry id>,
  scannedAt: <ms>,
  packageCount: 212,
  truncated: false,       // either hard cap was hit
  kevEnriched: true,      // KEV enrichment succeeded for this scan
  findings: [ { p: "log4j-core", v: "2.14.1", id: "GHSA-jfh8-c2jp-5v3q", kev: true }, … ] }
```

Store only what the delta needs. Not summaries, not CVSS, not fix versions — those are re-fetched by the next scan anyway, and a 200-finding snapshot stays a few KB.

New globals, matching the existing naming: `sbomScanSave(id, snapshot)` and `sbomScanGet(id)`. `sbomRecentList` joins `scannedAt` and the finding count onto each entry so the landing page needs no second round-trip.

**Every place that deletes an entry must delete its snapshot** — eviction past 20, `sbomRecentRemove` and `sbomRecentClear`. An orphaned snapshot is package data the user was told had been cleared.

Evicting an entry's *content* (past the newest 5) must **not** delete its snapshot. That is the point of the two caps: the history outlives the file.

### Delta

New `Services/ScanDeltaService.cs` — static and pure:

```csharp
public record ScanSnapshot(
    DateTimeOffset ScannedAt, int PackageCount, bool Truncated, bool KevEnriched,
    IReadOnlyList<SnapshotFinding> Findings);

public record SnapshotFinding(string Package, string Version, string VulnId, bool Kev);

public record ScanDelta(
    DateTimeOffset PreviousScanAt,
    IReadOnlySet<(string Package, string Version, string VulnId)> NewFindings,
    IReadOnlySet<string> NewlyKev,       // vuln ids
    int ResolvedCount,                   // in previous, absent now (withdrawn advisories)
    bool Reliable);
```

A finding's identity is `(package, version, vulnId)`. Three things can make a naive comparison lie, and each needs handling:

- **Hard caps.** The scan stops at 500 packages and 200 detail fetches (`VulnerabilityService.cs:15-16`). If either scan was truncated, a finding can appear "new" simply because it fell inside the cap this time. Set `Reliable = false` when either snapshot has `truncated`, and say so in the banner rather than suppressing the delta.
- **Failed KEV enrichment.** Enrichment is deliberately non-fatal. If the previous scan's KEV fetch failed, every KEV entry would look newly listed today. Only compute `NewlyKev` when **both** snapshots have `kevEnriched`. This needs `VulnerabilityService` to report whether KEV enrichment succeeded — today it only emits a warning string, so add a flag to the scan result or a callback alongside `onWarning`.
- **Alias churn.** OSV sometimes re-issues an advisory under a different primary id. Accept this as a rare false "new" for now; if it proves noisy, match on the CVE alias when one exists.

### Scan flow

In `ScanVulnerabilities` (`DynamicSbomViewer.razor:420`), after results arrive and only when `SbomState.RecentId` is set:

1. Read the previous snapshot.
2. Compute the delta and store it in `ChatState.ScanDelta` (cleared by `ClearVulnerabilities()`).
3. Save the new snapshot.

Read before write, in that order, and wrap the whole block in try/catch — it runs after the flagship feature has succeeded and must not be able to turn that into an error.

Do not overwrite a snapshot with a failed scan (`ScanError` set). A network blip must not erase the history.

### UI

**Vulnerabilities tab** (`RenderVulnsTab`):

- A banner above the list when a delta exists: **"3 new since your last scan on 12 Oct · 1 newly added to CISA KEV"**. If nothing changed: "No new vulnerabilities since 12 Oct." — that reassurance is itself the answer the user came for. When `Reliable` is false, append "The scan was capped, so this comparison may be incomplete."
- A **NEW** pill on new rows, and a "New only" `.filter-btn` beside the existing KEV-only and has-fix toggles. It must not collide with the KEV indicator, which should remain the most urgent mark on a row.
- No banner on a first scan.

**Landing page** (`UploadFile.razor`, the Recent row built in feature 3):

- Extend the second line of each row: `CycloneDX 1.6 · 1.2 MB · 3 days ago · scanned 23 days ago, 47 found`.
- **Re-check** on entries that have both content and a snapshot. It opens the file and starts the scan in one action — the click is the user's consent to scan. Implement as a one-shot `SbomState.ScanOnOpen` flag that `DynamicSbomViewer` consumes after first render.
- Content-less entries keep their "scanned … ago, N found" line and change their prompt from "Drop this file again to reopen" to "Drop this file again to re-check".
- Update `.recent-note`: "File names, recent files and their last scan results are kept in this browser only — never uploaded anywhere."

`RecentSbom` gains `long? ScannedAt` and `int? FindingCount`, and can reuse the existing `SavedAgo` formatting for the "scanned … ago" label.

### Not in this plan

- **Background or scheduled re-scans, browser notifications, email.** All need either silent network calls or a backend.
- **New CVEs between two different documents** — that is phase 2 of the [diff plan](../sbom-diff-plan.md).
- **A timeline of more than one previous scan.** One snapshot answers "what is new"; a history chart is a different feature.
- **Grouping successive releases of the same product** (storefront 2.4.0 → 2.4.1) into one tracked item. The `subject` label from feature 3 makes this possible later, but it is a different model from "one file, one history".

## Files

| File | Change |
|------|--------|
| `wwwroot/js/sbom-recent.js` | `DB_VERSION` 2; `scans` store; save returns id; `sbomScanSave` / `sbomScanGet`; entry deletes cascade to snapshots; list joins scan metadata |
| `Services/RecentSbomStore.cs` | `SaveAsync` returns `long?`; `SaveScanAsync`, `GetScanAsync` — best-effort |
| `Services/SbomLoader.cs` | `SbomLoadResult.RecentId` |
| `Services/SbomState.cs` | `RecentId`, `ScanOnOpen` |
| `Services/ScanDeltaService.cs` | New — snapshot projection and delta |
| `Models/ScanSnapshot.cs` | New — `ScanSnapshot`, `SnapshotFinding`, `ScanDelta` |
| `Models/RecentSbom.cs` | `ScannedAt`, `FindingCount`, label |
| `Services/VulnerabilityService.cs` | Report whether KEV enrichment succeeded and whether a cap was hit |
| `Services/ChatState.cs` | `ScanDelta`, cleared with the other vuln state |
| `Components/DynamicSbomViewer.razor` | Snapshot read/write after scan, banner, NEW pill, "New only" filter, `ScanOnOpen` |
| `Components/UploadFile.razor` | Scan metadata on rows, Re-check, set `RecentId` |
| `wwwroot/css/app.css` | Banner and NEW pill |
| `PRIVACY.md`, `CLAUDE.md` | Scan results are now stored locally |

## Tasks

1. Plumb the entry id: JS save returns id → `RecentSbomStore` → `SbomLoadResult` → `SbomState`.
2. `scans` store, cascade deletes, list join.
3. `ScanDeltaService` and models, with tests. Independent of 1–2.
4. `VulnerabilityService` reports KEV-enriched and truncated.
5. Scan flow: read previous, compute, save.
6. Vulnerabilities tab UI.
7. Landing page UI and Re-check.

## Verification

- **`ScanDeltaService` unit tests**: identical snapshots → empty delta; one added finding → one new; a finding present before and absent now → `ResolvedCount` 1; the same vuln id on two packages is two findings.
- **KEV delta is suppressed** when either snapshot has `kevEnriched = false` — the test that prevents a failed enrichment last month from flagging everything as newly exploited today.
- **`Reliable` is false** when either snapshot is truncated.
- Extend `SbomLoaderTests`: `LoadAsync` surfaces the id; `Load(string, string)` leaves it null.
- **E2E — history** (`RecentSbomTests.cs` already drives IndexedDB): upload → scan → reload page → row shows "scanned" → Re-check → banner reads "No new vulnerabilities". Do not assert specific counts; OSV data moves.
- E2E: drop the same file again, and the same content renamed → snapshot still present. Drop a different file with the same name → it has no history, and the first file's history is untouched.
- E2E: upload and scan a file, then upload 5 more → the first entry has lost its content but still shows "scanned … ago". Re-drop it and scan → the delta banner appears.
- E2E: per-row × and Clear both empty the matching rows in `scans`. Check IndexedDB directly, not through the UI.
- **Upgrade path**: load the feature-3 build, upload a file, then load this build in the same profile → the database upgrades to version 2, the entry still opens, and no error is thrown.
- Manually force a delta: scan, edit the stored snapshot in dev tools to remove a finding and unset a `kev` flag, re-scan → banner shows 1 new and 1 newly KEV, and the pill is on the right row.
- With IndexedDB disabled (Firefox private window): scan completes normally, with no banner and no error.
- A failed scan (block `api.osv.dev`) leaves the previous snapshot untouched.
