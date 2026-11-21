namespace SBOMViewer.Blazor.Models;

/// <summary>
/// What a document says about one field. The standards treat "unknown, and says so" differently from
/// "absent" — CISA 2026 asks authors to explicitly identify unknown information — so the two are kept
/// apart rather than collapsed the way <see cref="ComponentRow"/> does for the license table.
/// </summary>
public enum FactState
{
    /// <summary>A real value.</summary>
    Present,
    /// <summary>The document explicitly says the value is unknown (SPDX <c>NOASSERTION</c>, a literal "unknown").</summary>
    DeclaredUnknown,
    /// <summary>The field is not there, or is empty.</summary>
    Missing,
    /// <summary>The format has no field for this, so the document cannot be faulted for omitting it.</summary>
    NotExpressible
}

/// <summary>Format-neutral facts about one component, for <c>SbomQualityService</c>.</summary>
public record ComponentFacts
{
    /// <summary>Display label — "name@version", or a positional placeholder when the name is missing.</summary>
    public required string Label { get; init; }
    public FactState Name { get; init; } = FactState.Missing;
    public FactState Version { get; init; } = FactState.Missing;
    public FactState Supplier { get; init; } = FactState.Missing;
    public FactState Identifier { get; init; } = FactState.Missing;
    public FactState Hash { get; init; } = FactState.Missing;
    public FactState License { get; init; } = FactState.Missing;
    /// <summary>The component appears on either side of at least one dependency relationship.</summary>
    public bool InRelationship { get; init; }
}

/// <summary>
/// Format-neutral facts about a whole document. Produced by <c>QualityFactsExtractor</c>; the rules never
/// touch JSON. Every fact defaults to <see cref="FactState.Missing"/> so tests can build one by hand and set
/// only what they care about.
/// </summary>
public record QualityFacts
{
    public FactState DataFormat { get; init; } = FactState.Missing;
    public FactState Author { get; init; } = FactState.Missing;
    public FactState AuthorSignature { get; init; } = FactState.Missing;
    public FactState Timestamp { get; init; } = FactState.Missing;
    public FactState ToolName { get; init; } = FactState.Missing;
    public FactState ToolVersion { get; init; } = FactState.Missing;
    public FactState SbomVersion { get; init; } = FactState.Missing;
    public FactState GenerationContext { get; init; } = FactState.Missing;
    public FactState DependencyRelationships { get; init; } = FactState.Missing;
    public IReadOnlyList<ComponentFacts> Components { get; init; } = [];
}

public enum QualityProfile { Ntia2021, Cisa2026 }

public enum CheckStatus { Pass, Partial, Fail, NotExpressible }

public enum CheckScope { Document, Component }

/// <summary>
/// One readiness check. For document checks <see cref="Total"/> is 1. For component checks it is the
/// number of components the check applies to, <see cref="Affected"/> those without a real value, and
/// <see cref="DeclaredUnknown"/> the subset of <see cref="Affected"/> that at least say so.
/// <see cref="AffectedComponents"/> is uncapped — the UI caps what it renders.
/// </summary>
public record QualityCheck(
    string Id,
    string Title,
    string Reference,
    CheckScope Scope,
    CheckStatus Status,
    int Affected,
    int Total,
    int DeclaredUnknown,
    IReadOnlyList<string> AffectedComponents,
    string FixHint,
    string? Note = null)
{
    public int Satisfied => Total - Affected;
    public double CoveragePct => Total == 0 ? 0 : Math.Round((double)Satisfied / Total * 100, 1);
}

public record QualityReport(QualityProfile Profile, IReadOnlyList<QualityCheck> Checks)
{
    public int Passed => Checks.Count(c => c.Status == CheckStatus.Pass);
    public int Applicable => Checks.Count(c => c.Status != CheckStatus.NotExpressible);

    public int ComponentCount { get; init; }

    /// <summary>Components failing at least one applicable component-level check in this profile.</summary>
    public int ComponentsWithGap { get; init; }

    /// <summary>Mean coverage across applicable component checks, or null when there are none.</summary>
    public double? AverageComponentCoverage
    {
        get
        {
            var comp = Checks.Where(c => c.Scope == CheckScope.Component && c.Status != CheckStatus.NotExpressible && c.Total > 0).ToList();
            return comp.Count == 0 ? null : Math.Round(comp.Average(c => c.CoveragePct), 1);
        }
    }

    public string ProfileLabel => Profile switch
    {
        QualityProfile.Ntia2021 => "NTIA 2021",
        QualityProfile.Cisa2026 => "CISA 2026",
        _ => Profile.ToString()
    };
}
