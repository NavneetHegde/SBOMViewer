# SBOM Quality Scorecard — Is This SBOM Good Enough to Hand Over?

> **November 2026 release — feature 2 of 4.** Previous: [01 · Try a Sample](01-try-a-sample.md). Next: [03 · Recents Identity and Caps](03-recents-identity-and-caps.md). Epic: [README](README.md).

## Context

The app answers "what is in this SBOM?" It does not answer the question people increasingly have to answer before sending one to a customer, an auditor or a regulator: **"is this SBOM complete enough to be accepted?"**

That question has become concrete. CISA has replaced the 2021 NTIA minimum elements with a stricter set (hashes, licenses, generation tool, full transitive coverage), and Germany's BSI TR-03183-2 is widely treated as the practical bar for the EU Cyber Resilience Act. A generated SBOM routinely fails these — generators leave supplier, hash and license fields empty, and nothing tells the author until a recipient rejects the file.

Other browser-based viewers (SBOM Lens, SBOM Total, sbomlyze) already score against NTIA/CISA/BSI. This is now expected of an SBOM viewer, and its absence is the most visible gap against them.

### What already exists

- `SbomFormatDetector.Validate` checks a handful of required fields so the file can be *opened*. That is a gate, not an assessment, and should stay as it is.
- `ComponentRowExtractor` reads name, version, type, license and purl per component, for all three document shapes. It does not read supplier, hashes, or non-purl identifiers.
- The **Compliance tab is about license risk only** (`RenderComplianceTab`, `DynamicSbomViewer.razor:696`). "Compliance" there means "copyleft exposure", not "regulatory completeness". The two should not be merged into one tab — they answer different questions for different people.
- `DynamicSbomViewer.razor` is already 1,172 lines. This feature gets its own component.

## Prerequisite — confirm the rule sets against primary sources

The market research behind this plan came mostly from vendor blogs. Before writing a single rule, read the primary documents and record the exact element list in this file:

| Profile | Source to confirm | Confidence today |
|---|---|---|
| NTIA 2021 | NTIA "Minimum Elements for an SBOM" (July 2021) | High — stable, seven data fields |
| CISA 2026 | The final CISA minimum-elements document (reported published July 2026; the August 2025 PDF on cisa.gov is the *draft*) | Medium — element list below is from the draft plus secondary reporting |
| BSI TR-03183-2 | BSI TR-03183 Part 2, current version | Low — not yet read; **phase 2** |

A scorecard that is wrong about the standard is worse than no scorecard. Each rule in code should carry the section reference it implements.

### Findings (confirmed 2026-11-21)

**CISA 2026** — read from the final PDF, *2026 Minimum Elements for a Software Bill of Materials (SBOM)*, v2.1, 29 July 2026 (`cisa.gov/sites/default/files/2026-07/2026_cisa_sbom_minimum_elements_508c.pdf`). It differs from the draft-based table this plan started with:

- **The document has no numbered sections.** References in code use the category and element name, e.g. `CISA 2026 · Component Data: Component Producer`.
- **Data fields — SBOM Metadata (9):** SBOM Author, **SBOM Author Signature**, **SBOM Data Format Name**, **SBOM Data Format Version**, SBOM Generation Context, SBOM Timestamp, SBOM Tool Name, SBOM Tool Version, **SBOM Version**. The bold ones were not in the draft-based table.
- **Data fields — Component Data (8):** Component Producer (renamed from Supplier Name), Component Dependency Relationship, Component Hash Value, Component Hash Algorithm, Component Identifiers, Component License, Component Name, Component Version.
- **Coverage is a *Practice and Process*, not a data field** ("include … all components … including transitive dependencies. There is no minimum depth"). It stays as a heuristic check, referenced as a practice.
- **"Explicitly Identifying Unknown Information"** is now its own practice: when a value is unknown the author should say so. Tool version, component version, hash, license and producer each say "indicate that the information is unknown". This confirms the three-state design.
- SBOM Author is "the entity operating the tool … not the tool itself", and distinct from Component Producer. So CycloneDX `metadata.supplier` / `metadata.manufacturer` (which describe the *component*) do **not** satisfy SBOM Author.

**NTIA 2021** — unchanged: seven data fields (Supplier Name, Component Name, Version of the Component, Other Unique Identifiers, Dependency Relationship, Author of SBOM Data, Timestamp).

**BSI TR-03183-2** — not read; still phase 2.

## Requirements

- Assess the open document against a selectable profile and show, per check, pass / partial / fail with the count of components affected.
- List the components that fail each check, so the result is actionable rather than a number.
- Work across all six supported formats through one rule set.
- Run entirely locally with no network calls — unlike the scan, this feature can truthfully say nothing leaves the browser.
- Never present the result as a compliance determination.

## Approach

### Naming

Call it **Quality** in the sidebar and "readiness check" in copy. Avoid "compliant" and "certified" anywhere in the UI: the app checks whether fields are populated, not whether they are *true*, and no tool can attest conformance to the CRA. The tab carries a disclaimer in the same style as the existing `.compliance-disclaimer`.

### Three states, not two

The standards distinguish "unknown, and says so" from "absent". SPDX writes `NOASSERTION`; the CISA text explicitly prefers a declared unknown over invented data. So each fact is one of:

- **Present** — a real value.
- **DeclaredUnknown** — `NOASSERTION`, `NONE`, or an empty string where the format requires the field.
- **Missing** — the field is not there.

Reported separately. `ComponentRowExtractor` currently collapses `NOASSERTION` to `""` (`:65`, `:103`), which is right for the license table but loses this distinction, so the scorecard needs its own extraction.

### Facts extraction

New `Services/QualityFactsExtractor.cs`, format-switched like `ComponentRowExtractor.Extract`. It produces a format-neutral `QualityFacts` so the rules never touch JSON:

```csharp
public enum FactState { Present, DeclaredUnknown, Missing, NotExpressible }

public record ComponentFacts(
    string Name,
    FactState Version, FactState Supplier, FactState Identifier,
    FactState Hash, FactState License);

public record QualityFacts(
    FactState Author, FactState Timestamp,
    FactState ToolName, FactState ToolVersion,
    FactState GenerationContext,
    FactState DependencyRelationships,
    int ComponentsWithNoRelationship,
    IReadOnlyList<ComponentFacts> Components);
```

Do **not** add these fields to `ComponentRow`. That record feeds the diff and the components table; widening it for a different feature couples three things that have no reason to change together.

Field map (verify each against the specs while implementing):

| Fact | CycloneDX 1.5–1.7 | SPDX 2.2 / 2.3 | SPDX 3.0.1 (`@graph`) |
|---|---|---|---|
| Author | `metadata.authors`, `metadata.manufacturer`/`manufacture`, `metadata.supplier` | `creationInfo.creators` entries prefixed `Person:` / `Organization:` | `CreationInfo.createdBy` |
| Timestamp | `metadata.timestamp` | `creationInfo.created` | `CreationInfo.created` |
| Tool name / version | `metadata.tools` — **two shapes**: legacy array, and `tools.components[]` (1.5+) | `creationInfo.creators` entries prefixed `Tool:` (version is embedded as `name-version`) | `CreationInfo.createdUsing` → `Tool` element |
| Generation context | `metadata.lifecycles[].phase` | no dedicated field → `NotExpressible` | `software_sbomType` on the `software_Sbom` element |
| Component supplier | `components[].supplier.name` | `packages[].supplier` | `suppliedBy` |
| Component version | `components[].version` | `packages[].versionInfo` | `software_packageVersion` |
| Identifier | `purl`, `cpe`, `swid` | `externalRefs` with `purl` / `cpe22Type` / `cpe23Type` | `software_packageUrl`, `externalIdentifier` |
| Hash | `components[].hashes[]` | `packages[].checksums[]` | `verifiedUsing` |
| License | `components[].licenses[]` | `licenseConcluded` / `licenseDeclared` | concluded / declared license expressions |
| Dependencies | `dependencies[]` | `relationships[]` (`DEPENDS_ON`, `CONTAINS`, …) | `Relationship` elements |

`NotExpressible` matters: SPDX 2.x cannot state a generation context, and penalising a document for something its format cannot say would make every SPDX 2.x file fail. Those checks render as "not expressible in this format", excluded from the pass count.

### Rules as data

New `Services/SbomQualityService.cs` — static and pure, `QualityFacts` in, `QualityReport` out:

```csharp
public enum CheckStatus { Pass, Partial, Fail, NotExpressible }

public record QualityCheck(
    string Id, string Title, string Reference,   // e.g. "CISA 2026 §3.2"
    CheckStatus Status,
    int Affected, int Total,                     // component-level checks
    IReadOnlyList<string> AffectedComponents,    // capped, see below
    string FixHint);

public record QualityReport(string Profile, IReadOnlyList<QualityCheck> Checks)
{
    public int Passed => Checks.Count(c => c.Status == CheckStatus.Pass);
    public int Applicable => Checks.Count(c => c.Status != CheckStatus.NotExpressible);
}
```

A profile is a list of rule definitions — which fact, document-level or per-component, and the reference text. Adding BSI later is then a new list, not new logic.

Checks as implemented (confirmed per the prerequisite — 7 NTIA, 16 CISA):

| Check | NTIA 2021 | CISA 2026 | Not expressible in |
|---|:-:|:-:|---|
| SBOM author | ✓ | ✓ | |
| SBOM author signature (embedded only) | | ✓ | SPDX 2.x, SPDX 3.0.1 |
| Data format name and version | | ✓ | |
| Generation context | | ✓ | SPDX 2.x |
| Timestamp | ✓ | ✓ | |
| Tool name | | ✓ | |
| Tool version | | ✓ | |
| SBOM version | | ✓ | SPDX 3.0.1 |
| Component supplier / producer | ✓ | ✓ | |
| Dependency relationships present | ✓ | ✓ | |
| Component hash (value + algorithm) | | ✓ | |
| Unique identifier (purl / CPE / SWID / OmniBOR / SWHID) | ✓ | ✓ | |
| Component license | | ✓ | |
| Component name | ✓ | ✓ | |
| Component version | ✓ | ✓ | |
| Coverage — every component appears in a relationship (practice, heuristic) | | ✓ | |

Implementation decisions worth knowing:

- **Format name and version are one check, hash value and algorithm are one check.** Every supported format requires the algorithm alongside the value, so splitting them would only double-count. Data format always passes for a document the app opened.
- **Signature** checks only for an embedded CycloneDX `signature`, and does not verify it. A detached signature also satisfies the element; the fix hint says the check cannot see one.
- **SBOM version:** CycloneDX `version` or `serialNumber`; SPDX 2.x `documentNamespace`, which SPDX 2.3 §6.5 requires to change with every version of the document; not expressible in SPDX 3.0.1.
- **Tool version in SPDX:** neither SPDX 2.x nor the SPDX 3.0.1 `Tool` class has a version field. Both accept the `name-version` convention (`Tool: syft-1.18.0`); a tool string with no trailing version counts as missing.
- **`NONE` license is Present.** In SPDX it asserts "no license", which is a definite answer. `NOASSERTION` and a literal `unknown` count as DeclaredUnknown. An empty string counts as **Missing**, not DeclaredUnknown, because it declares nothing.
- **Unnamed components are kept**, unlike `ComponentRowExtractor`, so "Component name" can fail.
- **CycloneDX component producer** accepts `supplier`, `manufacturer`, `publisher`, `author` and `authors`. **SPDX 2.x** accepts `supplier` or `originator`.
- **Hash algorithm strength** (MD5 / SHA-1 not approved by NIST) is not checked yet — follow-up.

**Status thresholds** for per-component checks: Pass at 100%, Fail at 0%, Partial in between — with the percentage shown. Do not invent a pass mark like "90% is fine"; the standards do not define one, and the honest output is the coverage figure itself.

**Coverage is a heuristic and must say so.** The app cannot know whether transitive dependencies were omitted; it can only see that relationships exist and whether any listed component is unreferenced by all of them. Label it "relationships present for N of M components", not "transitive coverage verified".

### No single weighted score

Competitors show "7.4 / 10". Skip it. A weighted score needs weights nobody can justify, and it invites "what do I need for an 8?" instead of "what is missing?". Headline with **"9 of 13 checks pass"** per profile and let the coverage bars carry the detail.

### Fix hints

One short line per check explaining how the field usually gets populated — for example, hashes: "Generate the SBOM from build output or a lockfile with integrity data, rather than from a source scan." This is what turns a failed check into a next step, and it is where this can be better than the tools that only score. Keep hints generic; do not recommend specific commercial products.

### UI

New `Components/QualityTab.razor`, rendered from a new `quality` entry in the viewer's sidebar (`DynamicSbomViewer.razor:35-63`, between Vulnerabilities and Compliance).

*As built:* the tab is presentational and takes the `QualityReport` plus an `OnProfileChanged` callback. `DynamicSbomViewer` extracts `QualityFacts` once per document (keyed on the `JsonDocument` reference) and re-evaluates only when the profile changes, because the sidebar count, the Overview card and the print report all need the same result.

- **Profile switch** — `.filter-btn` toggles: NTIA 2021 / CISA 2026. Default to CISA 2026.
- **Headline** — "9 of 13 checks pass" plus a `.stat-card` row: document checks passed, average component coverage, components with any gap.
- **Checklist** — one row per check: status icon, title, reference, coverage bar (reuse `.stat-bar`), "412 of 430". Rows are `<details>`; expanding shows the fix hint and the affected components.
- **Affected components** — cap the rendered list at 200 with "and N more", and offer search. A 5,000-component SBOM with no hashes must not render 5,000 rows.

Match the surrounding hand-rolled markup (`.toolbar`, `.filter-btn`, `.stat-card`) — the viewer does not use Fluent UI for these despite the project convention.

**Overview tab** — one extra stat card ("Quality · 9/13 checks") that switches to the Quality tab on click, so the feature is discoverable without opening the sidebar entry.

**Print report** — add a short Quality section to `.print-report` (`DynamicSbomViewer.razor:146`): profile, headline, and the failed checks with counts. This is the artefact someone attaches to a vendor review.

### Release hardening (added before first release)

- **The check never runs on the upload path.** At or under 2,000 components it runs after the dashboard paints; above that it waits for a "Run readiness check" click. Measured in the published build: 5,000 components check in about 870 ms, and a profile switch takes about 470 ms. A hard size cap was rejected: the largest SBOMs are the ones whose authors most need the check, and uploads are already capped at 20 MB.
- **Ships as Beta**, with a "Report a wrong result" link on every check. It opens a pre-filled GitHub issue naming the check and its counts, never the file or component names. The real cure for wrong results is a fixture corpus from real generators (syft, cdxgen, trivy, Microsoft sbom-tool), with each file's expected result pinned. That is the gate for removing the Beta label.
  - **These files are local-only** (listed in `.git/info/exclude`, about 7.6 MB). Their tests use `[LocalSampleFact]`, which skips a test when its file is absent, so CI stays green. Each behaviour they uncovered also has a small inline test in `QualityFactsExtractorTests`.
  - **Corpus so far (2026-11-21):** `samples/examplemaven-0.0.1-enriched.spdx.json` (spdx-maven-plugin + Parlay, SPDX 2.3) and its SPDX 3.0.1 conversion. Results match a hand reading and are pinned in `SbomQualityServiceTests`. The pair also checks that component results are identical across the two formats. That check found one false negative: in 3.0.1 the package hash moves to the file linked by `hasDistributionArtifact`, which the extractor now follows. A syft file in syft's native JSON (`syft_1.57.2_darwin_amd64.sbom`) is not SPDX or CycloneDX, so the viewer cannot open it; regenerate with `-o spdx-json` / `-o cyclonedx-json`. `samples/sample-1.7.cdx.json` is hand-written (illustrative hash and VEX entries), so it counts as an edge-case fixture, not generator output. It found one scope decision: CycloneDX `cryptographic-asset` components are excluded from the component checks. `samples/cdxgen-12.9.3_express-5.3.0.cdx.json` (cdxgen 12.9.3, CycloneDX 1.7, 368 npm components) matches a hand count exactly with no fixes: CISA 14/16, NTIA 6/7. Only signature and component supplier fail; cdxgen records no supplier, publisher or author for npm packages. `cdxgen-12.9.3_express-5.3.0_signed.cdx.json` (same project, signed: JSF RS512 with the public key embedded, plus a signature on every component) passes the signature check, 15/16. That run also recorded component authors for 307 of 368 packages, so supplier is Partial. The check confirms a signature is present but does not verify it. Verifying in the browser would be possible (WebCrypto plus JCS canonicalisation), but a key embedded in the file proves nothing about who signed it. That needs a trusted key supplied separately, such as `sbom_sign_public.pem`, which makes it a separate feature. The express 5.3.0 scans also match hand counts on every check. All are pinned in `SbomQualityServiceTests`:

    | File | CISA 2026 | Not passing |
    |---|---|---|
    | `trivy-0.76.2_express-5.3.0.cdx.json` | 8/16 | author, signature, generation context, supplier, hash; identifier, license and version 64/65 (trivy's `package-lock.json` component) |
    | `syft-1.57.2_express-5.3.0.cdx.json` | 9/16 | author, signature, generation context, supplier, hash, license 65/90, coverage 65/90 (GitHub Actions have no edges) |
    | `syft-1.57.2_express-5.3.0.spdx.json` | 9/14 | supplier (66 NOASSERTION), hash, license (26 NOASSERTION); identifier and version 90/91 (the scanned-directory package) |
    | `sbom-tool-4.1.13_express-5.3.0.spdx.json` | 11/14 | supplier 309/370, hash, license (all NOASSERTION) |

    The syft pair found one cross-format inconsistency: CycloneDX `file` components (syft's ten workflow YAMLs) were counted, while the same files in SPDX `files` were not. `file` components are now excluded, like `cryptographic-asset`. **The corpus now covers all four generators named in the plan (cdxgen, syft, trivy, sbom-tool) plus spdx-maven-plugin.**

### Not in this plan

- **BSI TR-03183-2 profile** — phase 2, after reading the document.
- **Quality in the diff** — "the new release lost hashes on 40 components" on `/compare`. Valuable, and nearly free once `SbomQualityService` exists; do it as a follow-up.
- **Machine-readable export** of the report — belongs with the existing [data-export plan](../data-export-plan.md).
- **Schema validation** against the official JSON schemas. Different feature, needs the schema files shipped, and conflicts with the "no external schema files" convention.
- **Checking values are correct** — that a hash matches, a supplier is real, a license is accurate. Out of scope for any offline tool; the disclaimer says so.

## Files

| File | Change |
|------|--------|
| `Models/QualityReport.cs` | New — `FactState`, `ComponentFacts`, `QualityFacts`, `CheckStatus`, `QualityCheck`, `QualityReport` |
| `Services/QualityFactsExtractor.cs` | New — per-format fact extraction |
| `Services/SbomQualityService.cs` | New — profiles and rule evaluation |
| `Components/QualityTab.razor` | New — the tab |
| `Components/DynamicSbomViewer.razor` | Sidebar entry, tab switch, Overview stat card, print-report section |
| `wwwroot/css/app.css` | Checklist row and status-icon styling |
| `tests/.../Services/QualityFactsExtractorTests.cs` | New |
| `tests/.../Services/SbomQualityServiceTests.cs` | New |
| `CLAUDE.md` | Document the new services, models and tab |

## Tasks

0. **Read the primary sources** and fix the check table above. Blocks everything else.
1. `QualityFactsExtractor` for CycloneDX, with tests.
2. `QualityFactsExtractor` for SPDX 2.x and SPDX 3.0.1, with tests.
3. `SbomQualityService` with the NTIA 2021 and CISA 2026 profiles, with tests.
4. `QualityTab.razor` and the sidebar entry.
5. Overview stat card and print-report section.

Tasks 1 and 2 are independent. Task 3 can be developed against hand-built `QualityFacts` in parallel with them.

## Verification

- **Extractor tests per format**, using `TestData/TestJson.cs`: a fully populated document yields `Present` everywhere; removing one field flips exactly that fact.
- **`NOASSERTION` is `DeclaredUnknown`, not `Missing` and not `Present`** — test it for SPDX supplier and license specifically.
- **Both CycloneDX `metadata.tools` shapes** are recognised: the legacy array and the 1.5+ object with `components`.
- **SPDX 2.x generation context is `NotExpressible`** and is excluded from `Applicable`, so an otherwise-perfect SPDX 2.3 document can reach a full pass count.
- Rule tests on hand-built `QualityFacts`: 0% → Fail, 100% → Pass, anything between → Partial, with correct `Affected` / `Total`.
- A document with zero components does not divide by zero and does not report component checks as passing.
- The `samples/*-minimal.json` fixtures fail the checks you would expect them to; the `*-full.json` fixtures pass most. If a "full" sample fails something surprising, decide whether the sample or the rule is wrong before moving on.
- E2E: upload a sample → open Quality → headline is visible → switch profile → the check count changes.
- Manually: load a large real-world SBOM (thousands of components) and confirm the tab opens without a noticeable pause and the affected list is capped.
- Manually: open the network tab, switch to Quality, confirm **zero requests**.

### Added after review: JSON download (2026-11-21)

The Quality tab has a **Download JSON** button that saves the selected profile's report, built and saved in the browser. It holds every check, its status and counts, the element reference, the fix hint and the full affected-component list, plus a summary and the disclaimer. Format: `QualityReportExporter` (`schemaVersion` "1"). CSV and a combined export across both profiles are not included; they belong with the data-export plan.
