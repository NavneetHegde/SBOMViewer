using System.Text.Json;
using FluentAssertions;
using SBOMViewer.Blazor.Models;
using SBOMViewer.Blazor.Services;

namespace SBOMViewer.Blazor.Tests.Services;

/// <summary>
/// Guards the bundled demo document behind "Try a sample". It is read from the repo path the app
/// deploys it from, so a broken edit to the sample fails CI rather than production.
///
/// Nothing here asserts vulnerability counts — new CVEs are published against old versions all
/// the time, so what a scan of this file returns drifts.
/// </summary>
public class SampleSbomTests
{
    private static readonly string SamplePath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "../../../../../src/SBOMViewer.Blazor/wwwroot/samples/demo-storefront.cdx.json"));

    private static SbomLoadResult LoadSample() =>
        new SbomLoader(new SchemaService()).Load(File.ReadAllText(SamplePath), "demo-storefront.cdx.json");

    [Fact]
    public void Sample_PassesTheLoadPipeline()
    {
        var result = LoadSample();

        result.Error.Should().BeNull();
        result.Success.Should().BeTrue();
        result.Format.Should().Be(SbomFormat.CycloneDX_1_6);
        result.Schema.Should().NotBeNull();
    }

    [Fact]
    public void Sample_StaysSmallEnoughToFetchOnALandingPageClick()
    {
        new FileInfo(SamplePath).Length.Should().BeLessThan(50 * 1024);
    }

    [Fact]
    public void Sample_EveryComponentYieldsAScannablePackage()
    {
        using var doc = LoadSample().Document!;
        var componentCount = doc.RootElement.GetProperty("components").GetArrayLength();

        var packages = PackageExtractor.ExtractPackages(doc.RootElement, SbomFormat.CycloneDX_1_6);

        packages.Should().HaveCount(componentCount);
        packages.Should().OnlyContain(p => !string.IsNullOrEmpty(p.Purl) && !string.IsNullOrEmpty(p.Ecosystem));
        packages.Select(p => p.Ecosystem).Distinct().Should().BeEquivalentTo("Maven", "npm", "PyPI", "NuGet");
    }

    [Fact]
    public void Sample_Log4ShellComponentIsQueriedUnderItsOsvName()
    {
        using var doc = LoadSample().Document!;

        var packages = PackageExtractor.ExtractPackages(doc.RootElement, SbomFormat.CycloneDX_1_6);

        // OSV indexes Maven packages as group:artifact. The KEV-listed Log4Shell finding is the
        // centrepiece of the demo scan, and it only appears if this exact name and version is sent.
        packages.Should().Contain(p =>
            p.Name == "org.apache.logging.log4j:log4j-core" && p.Version == "2.14.1" && p.Ecosystem == "Maven");
    }

    [Fact]
    public void Sample_ExercisesTheComplianceTab()
    {
        using var doc = LoadSample().Document!;

        var rows = ComponentRowExtractor.Extract(doc.RootElement, SbomFormat.CycloneDX_1_6);

        rows.Should().Contain(r => r.Risk == LicenseRisk.StrongCopyleft);
        rows.Should().Contain(r => r.Risk == LicenseRisk.WeakCopyleft);
        rows.Should().Contain(r => r.Risk == LicenseRisk.Permissive);
        rows.Should().Contain(r => string.IsNullOrEmpty(r.License), "one component deliberately has no license");
    }

    [Fact]
    public void Sample_HasDependenciesThatResolveToComponents()
    {
        using var doc = LoadSample().Document!;
        var root = doc.RootElement;

        var refs = root.GetProperty("components").EnumerateArray()
            .Select(c => c.GetProperty("bom-ref").GetString())
            .Append(root.GetProperty("metadata").GetProperty("component").GetProperty("bom-ref").GetString())
            .ToHashSet();

        var dependencies = root.GetProperty("dependencies").EnumerateArray().ToList();
        dependencies.Should().NotBeEmpty();

        foreach (var dep in dependencies)
        {
            refs.Should().Contain(dep.GetProperty("ref").GetString());
            foreach (var target in dep.GetProperty("dependsOn").EnumerateArray())
                refs.Should().Contain(target.GetString());
        }
    }

    [Fact]
    public void Sample_MetadataIsMostlyButNotEntirelyComplete()
    {
        // The quality scorecard should show "mostly passes, here is what to fix" on the demo —
        // neither 100% nor 0%.
        using var doc = LoadSample().Document!;
        var components = doc.RootElement.GetProperty("components").EnumerateArray().ToList();

        var withHashes = components.Count(c => c.TryGetProperty("hashes", out _));
        var withSupplier = components.Count(c => c.TryGetProperty("supplier", out _));

        withHashes.Should().BeGreaterThan(components.Count / 2).And.BeLessThan(components.Count);
        withSupplier.Should().BeGreaterThan(components.Count / 2).And.BeLessThan(components.Count);
    }
}
