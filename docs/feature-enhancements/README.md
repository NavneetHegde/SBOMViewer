# Feature Enhancements

Candidate features that deepen the value of the existing application for its current users. Each is scoped independently and can be implemented on its own.

The app today does one thing well: upload a single SBOM, browse it via a dynamic tabbed dashboard (overview / components / vulnerabilities / compliance), and scan against OSV.dev. These proposals address three gaps found in the current code:

| # | Feature | Gap it closes | Plan | Status |
|---|---------|---------------|------|--------|
| 1 | SBOM Diff | No way to compare two SBOMs — the question users have at every release | [sbom-diff-plan.md](sbom-diff-plan.md) | **Implemented** (phase 1) |
| 2 | Data Exports | Nothing the app *derives* — license risk, scan results, the diff — can leave the browser in machine-readable form | [data-export-plan.md](data-export-plan.md) · [tasks](data-export-tasks.md) | Not started |
| 3 | Vulnerability Triage | Severity says what *could* be bad; nothing says what is actually being exploited | [vulnerability-triage-plan.md](vulnerability-triage-plan.md) | **Implemented** |
| 4 | User Feedback | No login, so no idea who uses the app or whether any of it lands | [user-feedback-plan.md](user-feedback-plan.md) | Not started |
| 5 | Try a Sample | A visitor with no SBOM on disk never sees the dashboard | [01-try-a-sample.md](November26/01-try-a-sample.md) | Not started |
| 6 | Quality Scorecard | Nothing says whether an SBOM is complete enough to hand over (NTIA / CISA minimum elements) | [02-quality-scorecard.md](November26/02-quality-scorecard.md) | Not started |
| 7 | Recents Identity and Caps | Two files with the same name overwrite each other; only 2 files are remembered | [03-recents-identity-and-caps.md](November26/03-recents-identity-and-caps.md) | Not started |
| 8 | Scan History | No memory of the last scan, so no way to see what is new — and no reason to return | [04-scan-history.md](November26/04-scan-history.md) | Not started |

## Next release

Features 5–8 make up the **[November 2026 release](November26/README.md)**. That folder holds the four plans, numbered in the order they are to be implemented.

Feature 1's phase 2 — surfacing **newly introduced CVEs** between the two documents — is not yet built. See the plan for the API-cap consideration it raises.

## Runner-up (not planned in detail)

**Dependency-relationship graph.** CycloneDX `dependencies` and SPDX `relationships` currently fall through to the generic `DynamicSection` renderer as flat JSON — the weakest part of the viewer. A tree or graph view of the dependency chain would make transitive-dependency risk visible.

## Common verification

```bash
dotnet build SBOMViewer.slnx
dotnet test
dotnet run --project src/SBOMViewer.Blazor      # https://localhost:5157
```

Exercise against the fixtures in `samples/` — they cover CycloneDX 1.5/1.6/1.7 and SPDX 2.2/2.3/3.0.1, so every format-aware extraction path gets hit. Add an E2E test in `tests/SBOMViewer.E2E.Tests/` for whichever feature ships; `FileUploadTests.cs` is the closest existing pattern for driving the upload flow.
