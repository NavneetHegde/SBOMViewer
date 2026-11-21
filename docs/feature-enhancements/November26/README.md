# November 2026 Release

Release epic. Four features, numbered in the order they are to be implemented.

## Goal

Move the app from "open an SBOM and look at it" to something a person has a reason to start using and a reason to return to:

- **New visitors** can see the product without having an SBOM on disk.
- **Current users** can tell whether an SBOM is complete enough to hand over.
- **Returning users** are told what has changed since they last looked.

## Features

| # | Feature | Plan | Audience | Size | Status |
|---|---------|------|----------|------|--------|
| 1 | **Try a Sample** — one-click demo SBOM and a `/?sample=1` deep link | [01-try-a-sample.md](01-try-a-sample.md) | New | Small | Not started |
| 2 | **Quality Scorecard** — readiness check against NTIA 2021 and CISA 2026 minimum elements | [02-quality-scorecard.md](02-quality-scorecard.md) | Current | Large | Not started |
| 3 | **Recents Identity and Caps** — identify files by content hash; remember 20 names, 5 files | [03-recents-identity-and-caps.md](03-recents-identity-and-caps.md) | Returning | Medium | Not started |
| 4 | **Scan History** — "what's new since your last scan", with one-click re-check | [04-scan-history.md](04-scan-history.md) | Returning | Medium | Not started |

## Order and dependencies

```
1 Try a Sample ─────────┐
                        ├─ independent of each other
2 Quality Scorecard ────┘

3 Recents Identity and Caps ──► 4 Scan History
```

- **3 before 4 is the only hard dependency.** Scan history is stored against the entries that feature 3 redefines.
- **1 and 2 are independent** of each other and of 3–4. They are numbered first because 1 is quick and gives every later feature a rich document to demo and test with, and 2 touches neither storage nor the network.
- **3 changes a feature already in production** (how recents are identified and evicted). Let it reach production and be observed before 4 stores more data on top of it.

## Before starting

- **Feature 2, task 0** — read the primary NTIA, CISA and BSI documents and confirm the check list. The list in the plan comes from the CISA draft and secondary reporting, and every other task in that feature depends on it.
- **Feature 1** — curate the demo file from real packages at real versions. An invented purl returns nothing from OSV.dev and makes the scan look broken.

## Releasing

Each feature is releasable on its own through the existing flow: feature branch → PR into a `release/*` branch (CI: build, unit tests, E2E) → staging deploy and automatic PR to `main` → production deploy and GitHub release.

Per feature, before merging to `main`:

- [ ] Unit and E2E tests listed in the plan's Verification section pass
- [ ] Manual checks in the plan's Verification section done on staging
- [ ] `CLAUDE.md` updated for new services, models and components
- [ ] `PRIVACY.md` updated if what is stored or sent has changed (features 3 and 4)
- [ ] Status updated in the table above and in [../README.md](../README.md)

## Out of scope for this release

Considered and deferred — see the individual plans for the reasoning:

- VEX import and authoring
- Dependency graph / path view
- End-of-life flags
- Fetching an SBOM from a GitHub repository URL
- CycloneDX XML input; crypto and ML BOM views
- BSI TR-03183-2 profile for the scorecard
- Background re-scans or notifications
