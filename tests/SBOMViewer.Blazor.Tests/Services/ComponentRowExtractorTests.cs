using System.Text.Json;
using FluentAssertions;
using SBOMViewer.Blazor.Models;
using SBOMViewer.Blazor.Services;

namespace SBOMViewer.Blazor.Tests.Services;

public class ComponentRowExtractorTests
{
    // ─── CycloneDX ──────────────────────────────────────────

    [Fact]
    public void Extract_CycloneDX_LicenseId_UsesIdAndClassifies()
    {
        var json = """
            {
                "bomFormat": "CycloneDX",
                "specVersion": "1.6",
                "components": [
                    { "name": "Newtonsoft.Json", "version": "13.0.3", "type": "library",
                      "purl": "pkg:nuget/Newtonsoft.Json@13.0.3",
                      "licenses": [ { "license": { "id": "MIT" } } ] }
                ]
            }
            """;
        using var doc = JsonDocument.Parse(json);

        var result = ComponentRowExtractor.Extract(doc.RootElement, SbomFormat.CycloneDX_1_6);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Newtonsoft.Json");
        result[0].License.Should().Be("MIT");
        result[0].Risk.Should().Be(LicenseRisk.Permissive);
    }

    [Fact]
    public void Extract_CycloneDX_LicenseNameFallback_UsedWhenNoId()
    {
        var json = """
            {
                "bomFormat": "CycloneDX",
                "specVersion": "1.7",
                "components": [
                    { "name": "Npgsql", "version": "8.0.0", "type": "library",
                      "licenses": [ { "license": { "name": "PostgreSQL License" } } ] }
                ]
            }
            """;
        using var doc = JsonDocument.Parse(json);

        var result = ComponentRowExtractor.Extract(doc.RootElement, SbomFormat.CycloneDX_1_7);

        result.Should().HaveCount(1);
        result[0].License.Should().Be("PostgreSQL License");
    }

    [Fact]
    public void Extract_CycloneDX_LicenseExpressionFallback_UsedWhenNoLicenseObject()
    {
        var json = """
            {
                "bomFormat": "CycloneDX",
                "specVersion": "1.6",
                "components": [
                    { "name": "dual-licensed-lib", "version": "1.0.0", "type": "library",
                      "licenses": [ { "expression": "MIT OR Apache-2.0" } ] }
                ]
            }
            """;
        using var doc = JsonDocument.Parse(json);

        var result = ComponentRowExtractor.Extract(doc.RootElement, SbomFormat.CycloneDX_1_6);

        result.Should().HaveCount(1);
        result[0].License.Should().Be("MIT OR Apache-2.0");
        result[0].Risk.Should().Be(LicenseRisk.Unknown);
    }

    [Fact]
    public void Extract_CycloneDX_NoLicenses_EmptyLicenseAndUnknownRisk()
    {
        var json = """
            {
                "bomFormat": "CycloneDX",
                "specVersion": "1.6",
                "components": [
                    { "name": "no-license-lib", "version": "1.0.0", "type": "library" }
                ]
            }
            """;
        using var doc = JsonDocument.Parse(json);

        var result = ComponentRowExtractor.Extract(doc.RootElement, SbomFormat.CycloneDX_1_6);

        result.Should().HaveCount(1);
        result[0].License.Should().BeEmpty();
        result[0].Risk.Should().Be(LicenseRisk.Unknown);
    }

    [Fact]
    public void Extract_CycloneDX_StrongCopyleftLicense_ClassifiesAsStrongCopyleft()
    {
        var json = """
            {
                "bomFormat": "CycloneDX",
                "specVersion": "1.6",
                "components": [
                    { "name": "gpl-lib", "version": "1.0.0", "type": "library",
                      "licenses": [ { "license": { "id": "GPL-3.0-only" } } ] }
                ]
            }
            """;
        using var doc = JsonDocument.Parse(json);

        var result = ComponentRowExtractor.Extract(doc.RootElement, SbomFormat.CycloneDX_1_6);

        result[0].Risk.Should().Be(LicenseRisk.StrongCopyleft);
    }

    [Fact]
    public void Extract_CycloneDX_NoComponents_ReturnsEmpty()
    {
        var json = """
            {
                "bomFormat": "CycloneDX",
                "specVersion": "1.6",
                "metadata": {}
            }
            """;
        using var doc = JsonDocument.Parse(json);

        var result = ComponentRowExtractor.Extract(doc.RootElement, SbomFormat.CycloneDX_1_6);

        result.Should().BeEmpty();
    }

    // ─── SPDX ──────────────────────────────────────────

    [Fact]
    public void Extract_Spdx_LicenseConcluded_UsedAndClassified()
    {
        var json = """
            {
                "spdxVersion": "SPDX-2.2",
                "packages": [
                    {
                        "name": "Microsoft.EntityFrameworkCore",
                        "versionInfo": "8.0.0",
                        "licenseConcluded": "MIT",
                        "externalRefs": [
                            { "referenceCategory": "PACKAGE-MANAGER", "referenceLocator": "pkg:nuget/Microsoft.EntityFrameworkCore@8.0.0" }
                        ]
                    }
                ]
            }
            """;
        using var doc = JsonDocument.Parse(json);

        var result = ComponentRowExtractor.Extract(doc.RootElement, SbomFormat.SPDX_2_2);

        result.Should().HaveCount(1);
        result[0].License.Should().Be("MIT");
        result[0].Risk.Should().Be(LicenseRisk.Permissive);
        result[0].Purl.Should().Be("pkg:nuget/Microsoft.EntityFrameworkCore@8.0.0");
    }

    [Theory]
    [InlineData("NOASSERTION")]
    [InlineData("NONE")]
    public void Extract_Spdx_NoAssertionOrNone_TreatedAsEmpty(string licenseConcluded)
    {
        var json = $$"""
            {
                "spdxVersion": "SPDX-2.2",
                "packages": [
                    { "name": "PackageA", "versionInfo": "1.0.0", "licenseConcluded": "{{licenseConcluded}}" }
                ]
            }
            """;
        using var doc = JsonDocument.Parse(json);

        var result = ComponentRowExtractor.Extract(doc.RootElement, SbomFormat.SPDX_2_2);

        result.Should().HaveCount(1);
        result[0].License.Should().BeEmpty();
        result[0].Risk.Should().Be(LicenseRisk.Unknown);
    }

    [Fact]
    public void Extract_Spdx_NoPackages_ReturnsEmpty()
    {
        var json = """
            {
                "spdxVersion": "SPDX-2.2",
                "name": "Test SBOM"
            }
            """;
        using var doc = JsonDocument.Parse(json);

        var result = ComponentRowExtractor.Extract(doc.RootElement, SbomFormat.SPDX_2_2);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Extract_NullFormat_ReturnsEmpty()
    {
        using var doc = JsonDocument.Parse("""{ "components": [] }""");

        var result = ComponentRowExtractor.Extract(doc.RootElement, null);

        result.Should().BeEmpty();
    }

    // ─── CycloneDX 1.5 ──────────────────────────────────────

    [Fact]
    public void Extract_CycloneDX15_LicenseId_UsesIdAndClassifies()
    {
        using var doc = JsonDocument.Parse(TestData.TestJson.ValidCycloneDX15WithComponents);

        var result = ComponentRowExtractor.Extract(doc.RootElement, SbomFormat.CycloneDX_1_5);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("ComponentA");
        result[0].License.Should().Be("MIT");
        result[0].Risk.Should().Be(LicenseRisk.Permissive);
    }

    // ─── SPDX 2.3 ──────────────────────────────────────────

    [Fact]
    public void Extract_Spdx23_LicenseConcluded_UsedAndClassified()
    {
        using var doc = JsonDocument.Parse(TestData.TestJson.ValidSpdx23WithPackages);

        var result = ComponentRowExtractor.Extract(doc.RootElement, SbomFormat.SPDX_2_3);

        result.Should().HaveCount(1);
        result[0].License.Should().Be("MIT");
        result[0].Risk.Should().Be(LicenseRisk.Permissive);
        result[0].Purl.Should().Be("pkg:npm/PackageA@1.0.0");
    }

    [Fact]
    public void Extract_Spdx23_NewSupplyChainAndSecurityFields_DontBreakExtraction()
    {
        using var doc = JsonDocument.Parse(TestData.TestJson.ValidSpdx23WithSecurityAndSupplyChainFields);

        var result = ComponentRowExtractor.Extract(doc.RootElement, SbomFormat.SPDX_2_3);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("PackageA");
        result[0].License.Should().Be("MIT");
        result[0].Risk.Should().Be(LicenseRisk.Permissive);
        result[0].Purl.Should().Be("pkg:npm/PackageA@1.0.0");
    }

    // ─── SPDX 3.0.1 ──────────────────────────────────────────

    [Fact]
    public void Extract_Spdx30_ConcludedLicenseExpression_UsedAndClassified()
    {
        using var doc = JsonDocument.Parse(TestData.TestJson.ValidSpdx30WithPackages);

        var result = ComponentRowExtractor.Extract(doc.RootElement, SbomFormat.SPDX_3_0);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("PackageA");
        result[0].License.Should().Be("MIT");
        result[0].Risk.Should().Be(LicenseRisk.Permissive);
        result[0].Purl.Should().Be("pkg:npm/PackageA@1.0.0");
    }

    [Fact]
    public void Extract_Spdx30_SecurityAndBuildProfileElements_DontBreakExtraction()
    {
        using var doc = JsonDocument.Parse(TestData.TestJson.ValidSpdx30WithSecurityAndBuildProfile);

        var result = ComponentRowExtractor.Extract(doc.RootElement, SbomFormat.SPDX_3_0);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("PackageA");
        result[0].License.Should().Be("MIT");
        result[0].Risk.Should().Be(LicenseRisk.Permissive);
        result[0].Purl.Should().Be("pkg:npm/PackageA@1.0.0");
    }

    [Fact]
    public void Extract_Spdx30_IgnoresNonPackageElements()
    {
        using var doc = JsonDocument.Parse(TestData.TestJson.ValidSpdx30Minimal);

        var result = ComponentRowExtractor.Extract(doc.RootElement, SbomFormat.SPDX_3_0);

        result.Should().BeEmpty();
    }
}
