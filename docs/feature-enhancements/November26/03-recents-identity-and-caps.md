# Recents — Content-Hash Identity and 5 / 20 Caps

> **November 2026 release — feature 3 of 4.** Previous: [02 · Quality Scorecard](02-quality-scorecard.md). Next: [04 · Scan History](04-scan-history.md). Epic: [README](README.md).

## Context

The recents store keeps the last 2 uploads in IndexedDB so they can be reopened without finding the file again. Two of its rules were fine for that shortcut and are wrong for anything more:

- **A file is identified by its name.** `sbomRecentSave` deletes any earlier entry with the same name (`wwwroot/js/sbom-recent.js:56-62`). SBOMs are very commonly all called `bom.json` or `sbom.json`, so a user with three projects sees them overwrite one another.
- **Only 2 files are remembered.** Anyone tracking more than two releases loses the oldest every time.

This feature fixes both. It is useful on its own — the overwrite is a bug users can hit today — and it is the foundation for [04 · Scan History](04-scan-history.md), which stores a scan result against each remembered file. History keyed to a name that can be overwritten, or limited to two files, would not be worth building.

It ships **before** feature 4, as its own release step, because it changes the behaviour of a feature already in production. Any problem with the new identity or eviction rules should surface before more data is stored on top of them.

### What already exists

- `wwwroot/js/sbom-recent.js` — flat `sbomRecent*` globals (`Save` / `List` / `Get` / `Clear`). Metadata (`entries`) and file content (`content`) are separate object stores so listing does not read file bodies. `MAX_ENTRIES = 2`. All of a save happens in one transaction.
- `Services/RecentSbomStore.cs` — thin interop wrapper, every call best-effort. `MaxEntries` mirrors the JS constant.
- `SbomLoader.LoadAsync` saves; the `Load(string, string)` overload deliberately does not.
- UI: the Recent row on the landing page (`UploadFile.razor:66-84`) and `Recent:` chips in an empty compare slot (`CompareSlot.razor`).
- `tests/SBOMViewer.E2E.Tests/RecentSbomTests.cs` asserts the 2-entry cap and same-name dedup. Both assertions change meaning here.

## Requirements

- Two different files with the same name are two entries.
- The same file dropped again — even renamed — is recognised as the same entry.
- Remember up to 20 files by name; keep the full content of the newest 5.
- Same-named entries are distinguishable in the list.
- A single entry can be forgotten without clearing everything.
- Existing users' stored files keep working after the upgrade.
- Everything stays best-effort: a storage failure never breaks an upload.

## Approach

### Identify a file by content hash, not by name

Each entry gets a `hash` field: the SHA-256 of the file content, hex-encoded, computed with `crypto.subtle.digest`. On save:

| Incoming file | Result |
|---|---|
| Hash matches an existing entry | Same document. Keep the entry and its id; update `name` and `savedAt`; restore the content if it had been evicted |
| No hash match | New entry — even if another entry has the same name |

A regenerated SBOM therefore counts as a new document, which is correct: it *is* a different document, and comparing the two is what `/compare` is for.

Two implementation constraints:

- **Hash before opening the transaction.** `crypto.subtle.digest` is asynchronous, and the existing rule — do every read and write inside one transaction, never await between steps — still holds. Compute the hash first, then open the transaction.
- **`crypto.subtle` exists only in a secure context.** Production (HTTPS) and `localhost` both qualify. If it is missing, fall back to `name + size` as the identity rather than failing the save.

### Telling same-named entries apart

Once two `bom.json` entries can coexist, the name alone no longer identifies them. Store a `subject` label taken from inside the document and show it beside the name — `bom.json · storefront 2.4.1`:

| Format | Subject |
|---|---|
| CycloneDX | `metadata.component.name` + `version` |
| SPDX 2.x | document `name` |
| SPDX 3.0.1 | `name` of the `SpdxDocument` element |

Extract it in `SbomLoader` (it already has the parsed document and the format) and pass it to `SaveAsync`. It is optional — plenty of SBOMs omit it — and the upload date remains the tiebreaker.

### Two caps: 5 files, 20 names

The single cap of 2 exists because each entry stores the whole file, up to 20 MB. Metadata is a few hundred bytes. Split them:

| Constant | Value | Governs |
|---|---|---|
| `MAX_CONTENT` | 5 | Entries whose **file content** is kept, newest first |
| `MAX_ENTRIES` | 20 | Entries whose **metadata** is kept |

Eviction on save, in one transaction:

1. Beyond the newest 5 — delete the row in the `content` store only, and set `hasContent: false` on the entry.
2. Beyond the newest 20 — delete the entry.

Worst case for content is 5 × 20 MB, roughly 100–200 MB as stored; typical SBOMs are far smaller, and a quota failure already degrades to "not saved". `RecentSbomStore` gains a second constant mirroring `MAX_CONTENT`.

An entry with `hasContent: false` cannot be reopened with a click. Its row says so; dropping that file again hash-matches the entry and restores the content.

**On its own, a content-less entry is only a remembered name.** It earns its place in feature 4, where it also carries the last scan result. Building the two-stage eviction here means feature 4 adds data to an existing structure rather than changing eviction a second time.

### Existing entries have no hash

Users upgrading from the current build have up to two entries without one. Backfill lazily at the start of `sbomRecentSave`, before the main transaction: read any entry lacking a hash, hash its stored content, write it back. At most two files, once. Without this, re-uploading a file stored before the upgrade creates a visible duplicate.

No `DB_VERSION` bump is needed here — entries gain fields (`hash`, `subject`, `hasContent`), and no object store is added. Treat a missing `hasContent` as `true` when reading old rows.

### Forgetting one entry

New global `sbomRecentRemove(id)`, deleting from `entries` and `content` in one transaction, wrapped by `RecentSbomStore.RemoveAsync`. With up to 20 names remembered, Clear-all alone is too blunt.

### UI

**Landing page** (`UploadFile.razor`, the Recent row):

- Each row: `bom.json · storefront 2.4.1` on the first line, `CycloneDX 1.6 · 1.2 MB · 3 days ago` on the second.
- Show the newest 5; a "Show older (n)" toggle reveals the rest. Twenty rows by default would push the two primary cards off the screen.
- Entries with `hasContent: false` are not clickable; they read "Drop this file again to reopen".
- A per-row **×** to forget one entry, next to the existing Clear-all.
- Update `.recent-note`: "File names and recent files are kept in this browser only — never uploaded anywhere."

**Compare page** (`CompareSlot.razor`): the `Recent:` chips list only entries with content, capped at the newest 3 so the slot does not fill with chips.

`RecentSbom` gains `Subject` and `HasContent`.

## Files

| File | Change |
|------|--------|
| `wwwroot/js/sbom-recent.js` | Content-hash identity with lazy backfill; `MAX_CONTENT` 5 / `MAX_ENTRIES` 20 two-stage eviction; `sbomRecentRemove`; entries gain `hash`, `subject`, `hasContent` |
| `Services/RecentSbomStore.cs` | `SaveAsync` takes `subject`; `RemoveAsync`; second cap constant — all best-effort |
| `Services/SbomLoader.cs` | Subject extraction, passed to `SaveAsync` |
| `Models/RecentSbom.cs` | `Subject`, `HasContent` |
| `Components/UploadFile.razor` | Subject on rows, "Show older", per-row remove, content-less rows, updated note |
| `Components/CompareSlot.razor` | Chips limited to entries with content, newest 3 |
| `wwwroot/css/app.css` | Recent-row additions |
| `tests/SBOMViewer.E2E.Tests/RecentSbomTests.cs` | Rewrite the cap and dedup tests for the new rules |
| `tests/.../Services/SbomLoaderTests.cs` | Subject extraction per format |
| `PRIVACY.md`, `CLAUDE.md` | Up to 20 file names are now remembered; recents section rewritten |

## Tasks

1. Content-hash identity in `sbomRecentSave`, with the `name + size` fallback.
2. Lazy backfill of existing entries.
3. Two-stage eviction and `hasContent`.
4. Subject extraction and storage.
5. `sbomRecentRemove` / `RemoveAsync`.
6. Landing-page and compare-slot UI.
7. Rewrite `RecentSbomTests.cs`.

## Verification

- **E2E — identity**:
  - two different files with the same name → two entries;
  - the same file twice → one entry;
  - the same content under a different name → one entry, showing the new name.
- **E2E — caps**: upload 6 distinct files → 6 rows, the oldest marked "drop again", and the `content` store holds exactly 5. Re-drop the oldest → it is clickable again. Upload 21 → 20 rows. Generate the files in the test rather than committing 21 fixtures.
- E2E: per-row × removes that entry from `entries` **and** `content`. Check IndexedDB directly, not through the UI.
- E2E: compare-slot chips never offer an entry without content.
- Unit: `SbomLoader` extracts the subject for CycloneDX, SPDX 2.x and SPDX 3.0.1, and returns null when the document has none.
- **Upgrade path**: load the current production build, upload two files, then load the new build in the same profile → both entries still open, gain a hash after the next save, and re-uploading one of them does not create a duplicate.
- With IndexedDB disabled (Firefox private window): upload works, no Recent row, no error.
- Over plain HTTP on a non-localhost host (no `crypto.subtle`): save still succeeds using the fallback identity.
