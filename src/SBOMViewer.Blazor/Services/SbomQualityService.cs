using SBOMViewer.Blazor.Models;

namespace SBOMViewer.Blazor.Services;

/// <summary>
/// Evaluates <see cref="QualityFacts"/> against a profile. Static and pure: facts in, report out.
///
/// A profile is a list of <see cref="Rule"/>s — which fact, document- or component-level, and the element
/// it implements. Adding a profile (BSI TR-03183-2 is planned) is a new list, not new logic.
///
/// This checks that fields are <em>populated</em>, never that they are <em>correct</em>, and the result is
/// not a compliance determination.
/// </summary>
public static class SbomQualityService
{
    /// <summary>
    /// One check definition. Exactly one of <see cref="Doc"/> / <see cref="Comp"/> is set. <see cref="Comp"/>
    /// receives the document facts too, because coverage depends on whether any relationships exist at all.
    /// </summary>
    private sealed record Rule(
        string Id,
        string Title,
        string Reference,
        string FixHint,
        Func<QualityFacts, FactState>? Doc = null,
        Func<QualityFacts, ComponentFacts, FactState>? Comp = null,
        string? Note = null);

    public static IReadOnlyList<QualityProfile> Profiles { get; } = [QualityProfile.Cisa2026, QualityProfile.Ntia2021];

    public static QualityReport Evaluate(QualityFacts facts, QualityProfile profile)
    {
        var rules = profile switch
        {
            QualityProfile.Ntia2021 => Ntia2021,
            QualityProfile.Cisa2026 => Cisa2026,
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, null)
        };
        // Counted per component rather than by joining the affected-label lists, because labels need not
        // be unique (the same package can appear twice).
        var compRules = rules.Where(r => r.Comp is not null).ToList();
        var withGap = facts.Components.Count(c => compRules.Any(r =>
            r.Comp!(facts, c) is FactState.Missing or FactState.DeclaredUnknown));

        return new QualityReport(profile, rules.Select(r => Run(r, facts)).ToList())
        {
            ComponentCount = facts.Components.Count,
            ComponentsWithGap = withGap
        };
    }

    private static QualityCheck Run(Rule rule, QualityFacts facts)
    {
        if (rule.Doc is not null)
        {
            var state = rule.Doc(facts);
            var status = state switch
            {
                FactState.Present => CheckStatus.Pass,
                FactState.DeclaredUnknown => CheckStatus.Partial,
                FactState.NotExpressible => CheckStatus.NotExpressible,
                _ => CheckStatus.Fail
            };
            var applies = state == FactState.NotExpressible ? 0 : 1;
            return new QualityCheck(rule.Id, rule.Title, rule.Reference, CheckScope.Document, status,
                Affected: state is FactState.Missing or FactState.DeclaredUnknown ? 1 : 0,
                Total: applies,
                DeclaredUnknown: state == FactState.DeclaredUnknown ? 1 : 0,
                AffectedComponents: [], rule.FixHint, rule.Note);
        }

        var total = 0;
        var declared = 0;
        var affected = new List<string>();
        foreach (var c in facts.Components)
        {
            var s = rule.Comp!(facts, c);
            if (s == FactState.NotExpressible) continue;
            total++;
            if (s == FactState.Present) continue;
            affected.Add(c.Label);
            if (s == FactState.DeclaredUnknown) declared++;
        }

        // Pass only at 100% and fail at 0%: the standards define no pass mark, so the honest output in
        // between is the coverage figure itself. No components at all is a failure, not a vacuous pass.
        var compStatus =
            facts.Components.Count > 0 && total == 0 ? CheckStatus.NotExpressible
            : total == 0 || affected.Count == total ? CheckStatus.Fail
            : affected.Count == 0 ? CheckStatus.Pass
            : CheckStatus.Partial;

        return new QualityCheck(rule.Id, rule.Title, rule.Reference, CheckScope.Component, compStatus,
            affected.Count, total, declared, affected, rule.FixHint, rule.Note);
    }

    // ── Shared fix hints ────────────────────────────────────────────────────────

    private const string HintAuthor =
        "Name the person or organisation that generated the SBOM — CycloneDX metadata.authors, SPDX an " +
        "\"Organization:\" or \"Person:\" creator. The generating tool alone does not count.";
    private const string HintTimestamp =
        "Generators set this automatically; a missing timestamp usually means the file was hand-edited or merged. " +
        "CycloneDX metadata.timestamp, SPDX creationInfo.created.";
    private const string HintSupplier =
        "Package registry metadata usually carries the publisher; many generators need an option or a " +
        "post-processing step to copy it in. If it is genuinely unknown, say so (SPDX NOASSERTION) rather than leaving it out.";
    private const string HintName = "Every component needs a name. An unnamed entry is usually a generator bug or a hand edit.";
    private const string HintVersion =
        "If the producer publishes no version, record that it is unknown rather than omitting the field. " +
        "Generating from a lockfile pins exact versions.";
    private const string HintIdentifier =
        "Add a purl (or CPE). Generating from a lockfile or package manifest normally yields a purl for every package.";
    private const string HintDependencies =
        "Generate from a lockfile or a resolved build so the dependency graph is captured — CycloneDX " +
        "dependencies, SPDX DEPENDS_ON / CONTAINS relationships.";

    // ── NTIA 2021 ───────────────────────────────────────────────────────────────
    // "The Minimum Elements For a Software Bill of Materials (SBOM)", NTIA, July 12 2021 — the seven
    // fields of the Data Fields section.

    private const string Ntia = "NTIA 2021 · Data Fields";

    private static readonly Rule[] Ntia2021 =
    [
        new("author",       "Author of SBOM data",            $"{Ntia}: Author of SBOM Data", HintAuthor,     Doc: f => f.Author),
        new("timestamp",    "Timestamp",                      $"{Ntia}: Timestamp",           HintTimestamp,  Doc: f => f.Timestamp),
        new("supplier",     "Supplier name",                  $"{Ntia}: Supplier Name",       HintSupplier,   Comp: (_, c) => c.Supplier),
        new("name",         "Component name",                 $"{Ntia}: Component Name",      HintName,       Comp: (_, c) => c.Name),
        new("version",      "Version of the component",       $"{Ntia}: Version of the Component", HintVersion, Comp: (_, c) => c.Version),
        new("identifier",   "Other unique identifiers",       $"{Ntia}: Other Unique Identifiers", HintIdentifier, Comp: (_, c) => c.Identifier),
        new("dependencies", "Dependency relationships present", $"{Ntia}: Dependency Relationship", HintDependencies, Doc: f => f.DependencyRelationships),
    ];

    // ── CISA 2026 ───────────────────────────────────────────────────────────────
    // "2026 Minimum Elements for a Software Bill of Materials (SBOM)", CISA et al., v2.1, July 29 2026.
    // The document has no numbered sections; references use its category and element names.

    private const string CisaMeta = "CISA 2026 · SBOM Metadata";
    private const string CisaComp = "CISA 2026 · Component Data";

    private static readonly Rule[] Cisa2026 =
    [
        new("author", "SBOM author", $"{CisaMeta}: SBOM Author", HintAuthor, Doc: f => f.Author),
        new("signature", "SBOM author signature", $"{CisaMeta}: SBOM Author Signature",
            "Sign the SBOM. CycloneDX can embed a signature; a detached signature delivered alongside the file " +
            "also meets this element, but this check cannot see one.",
            Doc: f => f.AuthorSignature,
            Note: "Checks only for an embedded signature, and does not verify it."),
        new("format", "Data format name and version", $"{CisaMeta}: SBOM Data Format Name / Version",
            "Use a current, non-deprecated SPDX or CycloneDX release.",
            Doc: f => f.DataFormat),
        new("generation-context", "Generation context", $"{CisaMeta}: SBOM Generation Context",
            "Record the lifecycle phase the SBOM was produced in (source, build, post-build…) — CycloneDX " +
            "metadata.lifecycles, SPDX 3 software_sbomType.",
            Doc: f => f.GenerationContext),
        new("timestamp", "Timestamp", $"{CisaMeta}: SBOM Timestamp", HintTimestamp, Doc: f => f.Timestamp),
        new("tool-name", "Tool name", $"{CisaMeta}: SBOM Tool Name",
            "Record the generating tool — CycloneDX metadata.tools, SPDX a \"Tool:\" creator.",
            Doc: f => f.ToolName),
        new("tool-version", "Tool version", $"{CisaMeta}: SBOM Tool Version",
            "Record the tool's version with its name (SPDX convention: \"Tool: name-1.2.3\"; CycloneDX tools[].version).",
            Doc: f => f.ToolVersion),
        new("sbom-version", "SBOM version", $"{CisaMeta}: SBOM Version",
            "Give the document a version and bump it on every change — CycloneDX version and serialNumber.",
            Doc: f => f.SbomVersion),

        new("supplier", "Component producer", $"{CisaComp}: Component Producer", HintSupplier, Comp: (_, c) => c.Supplier),
        new("dependencies", "Dependency relationships present", $"{CisaComp}: Component Dependency Relationship",
            HintDependencies, Doc: f => f.DependencyRelationships),
        new("hash", "Component hash", $"{CisaComp}: Component Hash Value / Algorithm",
            "Generate the SBOM from build output or a lockfile with integrity data, rather than from a source scan.",
            Comp: (_, c) => c.Hash),
        new("identifier", "Component identifiers", $"{CisaComp}: Component Identifiers", HintIdentifier, Comp: (_, c) => c.Identifier),
        new("license", "Component license", $"{CisaComp}: Component License",
            "Enable license detection in the generator, or take licenses from registry metadata. When a license " +
            "cannot be determined, declare it unknown (NOASSERTION).",
            Comp: (_, c) => c.License),
        new("name", "Component name", $"{CisaComp}: Component Name", HintName, Comp: (_, c) => c.Name),
        new("version", "Component version", $"{CisaComp}: Component Version", HintVersion, Comp: (_, c) => c.Version),

        new("coverage", "Components in the dependency graph", "CISA 2026 · Practices and Processes: Coverage",
            "Every component should appear in the dependency graph, leaves included — in CycloneDX a dependencies " +
            "entry with an empty dependsOn says \"no dependencies\".",
            Comp: (f, c) => f.DependencyRelationships == FactState.Present && c.InRelationship ? FactState.Present : FactState.Missing,
            Note: "Heuristic: shows which components appear in a relationship. It cannot tell whether transitive " +
                  "dependencies were left out of the SBOM altogether."),
    ];
}
