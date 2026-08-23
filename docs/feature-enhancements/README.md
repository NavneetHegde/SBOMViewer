# Feature Enhancements

Three candidate features that deepen the value of the existing application for its current users. Each is scoped independently and can be implemented on its own.

The app today does one thing well: upload a single SBOM, browse it via a dynamic tabbed dashboard (overview / components / vulnerabilities / compliance), and scan against OSV.dev. These proposals address three gaps found in the current code:

| # | Feature | Gap it closes | Plan | Status |
|---|---------|---------------|------|--------|
| 1 | SBOM Diff | No way to compare two SBOMs — the question users have at every release | [sbom-diff-plan.md](sbom-diff-plan.md) | **Implemented** (phase 1) |
| 2 | Data Exports | Only export path is browser print-to-PDF, though every tab holds clean structured data | [data-export-plan.md](data-export-plan.md) | Not started |
| 3 | Vulnerability Triage | Scan results are an undifferentiated list — no exploitability signal, no sort or filter | [vulnerability-triage-plan.md](vulnerability-triage-plan.md) | Not started |

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
