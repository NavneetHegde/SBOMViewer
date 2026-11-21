namespace SBOMViewer.Blazor.Tests.TestData;

/// <summary>
/// Real-generator SBOMs (cdxgen, syft, trivy, sbom-tool…) are large and are kept out of the repository;
/// they live only in a developer's local <c>samples/</c> folder (listed in <c>.git/info/exclude</c>).
/// A test pinned to one runs when the file is present and is reported as skipped otherwise, so CI stays
/// green without them. The behaviour each file uncovered is also covered by a small inline test.
/// </summary>
public static class LocalSamples
{
    public static readonly string Dir =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../samples"));

    public static string? SkipReason(string[] files)
    {
        var missing = files.Where(f => !File.Exists(Path.Combine(Dir, f))).ToArray();
        return missing.Length == 0 ? null : $"Local-only sample not present: {string.Join(", ", missing)}";
    }
}

public sealed class LocalSampleFactAttribute : FactAttribute
{
    public LocalSampleFactAttribute(params string[] files) => Skip = LocalSamples.SkipReason(files);
}

public sealed class LocalSampleTheoryAttribute : TheoryAttribute
{
    public LocalSampleTheoryAttribute(params string[] files) => Skip = LocalSamples.SkipReason(files);
}
