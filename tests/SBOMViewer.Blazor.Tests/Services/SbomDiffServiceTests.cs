using System.Text.Json;
using FluentAssertions;
using SBOMViewer.Blazor.Models;
using SBOMViewer.Blazor.Services;

namespace SBOMViewer.Blazor.Tests.Services;

public class SbomDiffServiceTests
{
    private static ComponentRow Row(
        string name, string version, string? purl = null, string license = "MIT",
        LicenseRisk risk = LicenseRisk.Permissive)
        => new(name, version, "library", license, purl, risk);

    // ─── purl identity ──────────────────────────────────────

    [Theory]
    [InlineData("pkg:npm/lodash@4.17.20",              "pkg:npm/lodash")]
    [InlineData("pkg:npm/%40angular/core@12.0.0",      "pkg:npm/%40angular/core")]
    [InlineData("pkg:nuget/Newtonsoft.Json@13.0.3",    "pkg:nuget/newtonsoft.json")]
    [InlineData("pkg:golang/x/text@v0.3.0?arch=amd64", "pkg:golang/x/text")]
    [InlineData("pkg:generic/thing@1.0#sub/path",      "pkg:generic/thing")]
    [InlineData("pkg:npm/lodash",                      "pkg:npm/lodash")]
    public void PurlIdentity_StripsVersionQualifiersAndSubpath(string purl, string expected)
        => SbomDiffService.PurlIdentity(purl).Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PurlIdentity_ReturnsNullForMissingPurl(string? purl)
        => SbomDiffService.PurlIdentity(purl).Should().BeNull();

    // ─── core behaviour ─────────────────────────────────────

    [Fact]
    public void Compare_IdenticalLists_ReportsNoChanges()
    {
        var rows = new List<ComponentRow>
        {
            Row("lodash",  "4.17.20", "pkg:npm/lodash@4.17.20"),
            Row("express", "4.18.2",  "pkg:npm/express@4.18.2")
        };

        var diff = SbomDiffService.Compare(rows, [.. rows]);

        diff.IsIdentical.Should().BeTrue();
        diff.TotalChanges.Should().Be(0);
        diff.UnchangedCount.Should().Be(2);
    }

    [Fact]
    public void Compare_VersionBump_IsVersionChange_NotAddPlusRemove()
    {
        // The purl carries the version, so a naive join on the raw purl would report
        // this as one addition and one removal instead of a single version change.
        var baseline = new List<ComponentRow> { Row("lodash", "4.17.20", "pkg:npm/lodash@4.17.20") };
        var current  = new List<ComponentRow> { Row("lodash", "4.17.21", "pkg:npm/lodash@4.17.21") };

        var diff = SbomDiffService.Compare(baseline, current);

        diff.Added.Should().BeEmpty();
        diff.Removed.Should().BeEmpty();
        diff.VersionChanged.Should().ContainSingle();
        diff.VersionChanged[0].Baseline!.Version.Should().Be("4.17.20");
        diff.VersionChanged[0].Current!.Version.Should().Be("4.17.21");
    }

    [Fact]
    public void Compare_ScopedNpmPackageVersionBump_IsVersionChange()
    {
        var baseline = new List<ComponentRow> { Row("@angular/core", "12.0.0", "pkg:npm/%40angular/core@12.0.0") };
        var current  = new List<ComponentRow> { Row("@angular/core", "13.0.0", "pkg:npm/%40angular/core@13.0.0") };

        var diff = SbomDiffService.Compare(baseline, current);

        diff.VersionChanged.Should().ContainSingle();
        diff.Added.Should().BeEmpty();
        diff.Removed.Should().BeEmpty();
    }

    [Fact]
    public void Compare_DetectsAdditionsAndRemovals()
    {
        var baseline = new List<ComponentRow> { Row("kept", "1.0", "pkg:npm/kept@1.0"), Row("gone", "2.0", "pkg:npm/gone@2.0") };
        var current  = new List<ComponentRow> { Row("kept", "1.0", "pkg:npm/kept@1.0"), Row("fresh", "3.0", "pkg:npm/fresh@3.0") };

        var diff = SbomDiffService.Compare(baseline, current);

        diff.Added.Should().ContainSingle().Which.Name.Should().Be("fresh");
        diff.Removed.Should().ContainSingle().Which.Name.Should().Be("gone");
        diff.UnchangedCount.Should().Be(1);
    }

    [Fact]
    public void Compare_LicenseChange_IsReportedWithBothSides()
    {
        var baseline = new List<ComponentRow> { Row("thing", "1.0", "pkg:npm/thing@1.0", "MIT",     LicenseRisk.Permissive) };
        var current  = new List<ComponentRow> { Row("thing", "1.0", "pkg:npm/thing@1.0", "GPL-3.0", LicenseRisk.StrongCopyleft) };

        var diff = SbomDiffService.Compare(baseline, current);

        diff.LicenseChanged.Should().ContainSingle();
        diff.LicenseChanged[0].Baseline!.Risk.Should().Be(LicenseRisk.Permissive);
        diff.LicenseChanged[0].Current!.Risk.Should().Be(LicenseRisk.StrongCopyleft);
        diff.VersionChanged.Should().BeEmpty();
    }

    [Fact]
    public void Compare_VersionAndLicenseBothChanged_AppearsInBothBuckets()
    {
        var baseline = new List<ComponentRow> { Row("thing", "1.0", "pkg:npm/thing@1.0", "MIT",     LicenseRisk.Permissive) };
        var current  = new List<ComponentRow> { Row("thing", "2.0", "pkg:npm/thing@2.0", "GPL-3.0", LicenseRisk.StrongCopyleft) };

        var diff = SbomDiffService.Compare(baseline, current);

        diff.VersionChanged.Should().ContainSingle();
        diff.LicenseChanged.Should().ContainSingle();
        diff.UnchangedCount.Should().Be(0);
    }

    // ─── edge cases ─────────────────────────────────────────

    [Fact]
    public void Compare_MatchesByNameWhenPurlMissingOnOneSide()
    {
        // Comparing across formats: an SPDX document without externalRefs has no purl,
        // so the name fallback is what keeps it from reading as a wholesale replacement.
        var baseline = new List<ComponentRow> { Row("lodash", "4.17.20", purl: null) };
        var current  = new List<ComponentRow> { Row("lodash", "4.17.21", "pkg:npm/lodash@4.17.21") };

        var diff = SbomDiffService.Compare(baseline, current);

        diff.VersionChanged.Should().ContainSingle();
        diff.Added.Should().BeEmpty();
        diff.Removed.Should().BeEmpty();
    }

    [Fact]
    public void Compare_NameMatchIsCaseInsensitive()
    {
        var diff = SbomDiffService.Compare(
            [Row("Lodash", "1.0", purl: null)],
            [Row("lodash", "1.0", purl: null)]);

        diff.IsIdentical.Should().BeTrue();
        diff.UnchangedCount.Should().Be(1);
    }

    [Fact]
    public void Compare_DuplicateIdentities_PairsOccurrencesAndReportsSurplus()
    {
        // The same package at two versions is legal in a lock-file-derived SBOM.
        var baseline = new List<ComponentRow>
        {
            Row("dup", "1.0", "pkg:npm/dup@1.0"),
            Row("dup", "2.0", "pkg:npm/dup@2.0")
        };
        var current = new List<ComponentRow> { Row("dup", "1.0", "pkg:npm/dup@1.0") };

        var diff = SbomDiffService.Compare(baseline, current);

        diff.Removed.Should().ContainSingle();
        diff.BaselineCount.Should().Be(2);
        diff.CurrentCount.Should().Be(1);
    }

    [Fact]
    public void Compare_EmptyBaseline_ReportsEverythingAsAdded()
    {
        var diff = SbomDiffService.Compare([], [Row("a", "1.0"), Row("b", "2.0")]);

        diff.Added.Should().HaveCount(2);
        diff.Removed.Should().BeEmpty();
        diff.UnchangedCount.Should().Be(0);
    }

    [Fact]
    public void Compare_EmptyCurrent_ReportsEverythingAsRemoved()
    {
        var diff = SbomDiffService.Compare([Row("a", "1.0")], []);

        diff.Removed.Should().ContainSingle();
        diff.Added.Should().BeEmpty();
    }

    [Fact]
    public void Compare_BothEmpty_IsIdentical()
        => SbomDiffService.Compare([], []).IsIdentical.Should().BeTrue();

    [Fact]
    public void Compare_ResultsAreSortedByName()
    {
        var diff = SbomDiffService.Compare([], [Row("zeta", "1.0"), Row("alpha", "1.0"), Row("mid", "1.0")]);

        diff.Added.Select(c => c.Name).Should().ContainInOrder("alpha", "mid", "zeta");
    }

    // ─── end-to-end over real documents ─────────────────────

    [Fact]
    public void Compare_SameDocumentAgainstItself_IsIdentical()
    {
        using var doc = JsonDocument.Parse(TestData.TestJson.ValidCycloneDXWithComponents);

        var diff = SbomDiffService.Compare(
            doc.RootElement, SbomFormat.CycloneDX_1_6,
            doc.RootElement, SbomFormat.CycloneDX_1_6);

        diff.IsIdentical.Should().BeTrue();
    }

    [Fact]
    public void Compare_AcrossFormats_MatchesComponentsByName()
    {
        var cyclone = """
            {
              "bomFormat": "CycloneDX", "specVersion": "1.6",
              "components": [
                { "name": "shared", "version": "2.0", "type": "library", "licenses": [ { "license": { "id": "MIT" } } ] },
                { "name": "only-in-cyclone", "version": "1.0", "type": "library" }
              ]
            }
            """;
        var spdx = """
            {
              "spdxVersion": "SPDX-2.3",
              "packages": [
                { "name": "shared", "versionInfo": "1.0", "licenseConcluded": "MIT" },
                { "name": "only-in-spdx", "versionInfo": "1.0", "licenseConcluded": "MIT" }
              ]
            }
            """;

        using var baseline = JsonDocument.Parse(spdx);
        using var current  = JsonDocument.Parse(cyclone);

        var diff = SbomDiffService.Compare(
            baseline.RootElement, SbomFormat.SPDX_2_3,
            current.RootElement,  SbomFormat.CycloneDX_1_6);

        diff.VersionChanged.Should().ContainSingle().Which.Name.Should().Be("shared");
        diff.Added.Should().ContainSingle().Which.Name.Should().Be("only-in-cyclone");
        diff.Removed.Should().ContainSingle().Which.Name.Should().Be("only-in-spdx");
    }
}
