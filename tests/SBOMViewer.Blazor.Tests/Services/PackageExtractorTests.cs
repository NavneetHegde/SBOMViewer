using System.Text.Json;
using FluentAssertions;
using SBOMViewer.Blazor.Models;
using SBOMViewer.Blazor.Services;

namespace SBOMViewer.Blazor.Tests.Services;

public class PackageExtractorTests
{
    // ─── CycloneDX ──────────────────────────────────────────

    [Fact]
    public void ExtractPackages_CycloneDX_ExtractsComponents()
    {
        var json = """
            {
                "bomFormat": "CycloneDX",
                "specVersion": "1.6",
                "components": [
                    { "name": "lodash", "version": "4.17.21", "purl": "pkg:npm/lodash@4.17.21" },
                    { "name": "express", "version": "4.18.2", "purl": "pkg:npm/express@4.18.2" }
                ]
            }
            """;
        using var doc = JsonDocument.Parse(json);

        var result = PackageExtractor.ExtractPackages(doc.RootElement, SbomFormat.CycloneDX_1_6);

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("lodash");
        result[0].Version.Should().Be("4.17.21");
        result[0].Ecosystem.Should().Be("npm");
        result[0].Purl.Should().Be("pkg:npm/lodash@4.17.21");
    }

    [Fact]
    public void ExtractPackages_CycloneDX17_ExtractsComponents()
    {
        var json = """
            {
                "bomFormat": "CycloneDX",
                "specVersion": "1.7",
                "components": [
                    { "name": "Newtonsoft.Json", "version": "13.0.3", "purl": "pkg:nuget/Newtonsoft.Json@13.0.3" }
                ]
            }
            """;
        using var doc = JsonDocument.Parse(json);

        var result = PackageExtractor.ExtractPackages(doc.RootElement, SbomFormat.CycloneDX_1_7);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Newtonsoft.Json");
        result[0].Ecosystem.Should().Be("NuGet");
    }

    [Fact]
    public void ExtractPackages_CycloneDX_NoPurl_EcosystemIsNull()
    {
        var json = """
            {
                "bomFormat": "CycloneDX",
                "specVersion": "1.6",
                "components": [
                    { "name": "MyLib", "version": "1.0.0" }
                ]
            }
            """;
        using var doc = JsonDocument.Parse(json);

        var result = PackageExtractor.ExtractPackages(doc.RootElement, SbomFormat.CycloneDX_1_6);

        result.Should().HaveCount(1);
        result[0].Ecosystem.Should().BeNull();
        result[0].Purl.Should().BeNull();
    }

    [Fact]
    public void ExtractPackages_CycloneDX_MissingVersion_Skipped()
    {
        var json = """
            {
                "bomFormat": "CycloneDX",
                "specVersion": "1.6",
                "components": [
                    { "name": "NoVersion" },
                    { "name": "HasVersion", "version": "1.0.0" }
                ]
            }
            """;
        using var doc = JsonDocument.Parse(json);

        var result = PackageExtractor.ExtractPackages(doc.RootElement, SbomFormat.CycloneDX_1_6);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("HasVersion");
    }

    [Fact]
    public void ExtractPackages_CycloneDX_NoComponents_ReturnsEmpty()
    {
        var json = """
            {
                "bomFormat": "CycloneDX",
                "specVersion": "1.6",
                "metadata": {}
            }
            """;
        using var doc = JsonDocument.Parse(json);

        var result = PackageExtractor.ExtractPackages(doc.RootElement, SbomFormat.CycloneDX_1_6);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtractPackages_CycloneDX_VariousEcosystems()
    {
        var json = """
            {
                "bomFormat": "CycloneDX",
                "specVersion": "1.6",
                "components": [
                    { "name": "requests", "version": "2.31.0", "purl": "pkg:pypi/requests@2.31.0" },
                    { "name": "serde", "version": "1.0.0", "purl": "pkg:cargo/serde@1.0.0" },
                    { "name": "rails", "version": "7.0.0", "purl": "pkg:gem/rails@7.0.0" },
                    { "name": "gin", "version": "1.9.1", "purl": "pkg:golang/gin@1.9.1" }
                ]
            }
            """;
        using var doc = JsonDocument.Parse(json);

        var result = PackageExtractor.ExtractPackages(doc.RootElement, SbomFormat.CycloneDX_1_6);

        result.Should().HaveCount(4);
        result[0].Ecosystem.Should().Be("PyPI");
        result[1].Ecosystem.Should().Be("crates.io");
        result[2].Ecosystem.Should().Be("RubyGems");
        result[3].Ecosystem.Should().Be("Go");
    }

    // ─── SPDX ──────────────────────────────────────────

    [Fact]
    public void ExtractPackages_Spdx_ExtractsPackages()
    {
        var json = """
            {
                "spdxVersion": "SPDX-2.2",
                "packages": [
                    {
                        "name": "PackageA",
                        "versionInfo": "1.0.0",
                        "externalRefs": [
                            { "referenceType": "purl", "referenceLocator": "pkg:npm/PackageA@1.0.0" }
                        ]
                    },
                    {
                        "name": "PackageB",
                        "versionInfo": "2.0.0"
                    }
                ]
            }
            """;
        using var doc = JsonDocument.Parse(json);

        var result = PackageExtractor.ExtractPackages(doc.RootElement, SbomFormat.SPDX_2_2);

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("PackageA");
        result[0].Ecosystem.Should().Be("npm");
        result[0].Purl.Should().Be("pkg:npm/PackageA@1.0.0");
        result[1].Name.Should().Be("PackageB");
        result[1].Ecosystem.Should().BeNull();
        result[1].Purl.Should().BeNull();
    }

    [Fact]
    public void ExtractPackages_Spdx_MissingVersionInfo_Skipped()
    {
        var json = """
            {
                "spdxVersion": "SPDX-2.2",
                "packages": [
                    { "name": "NoVersion" },
                    { "name": "HasVersion", "versionInfo": "1.0.0" }
                ]
            }
            """;
        using var doc = JsonDocument.Parse(json);

        var result = PackageExtractor.ExtractPackages(doc.RootElement, SbomFormat.SPDX_2_2);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("HasVersion");
    }

    [Fact]
    public void ExtractPackages_Spdx_NoPackages_ReturnsEmpty()
    {
        var json = """
            {
                "spdxVersion": "SPDX-2.2",
                "name": "Test SBOM"
            }
            """;
        using var doc = JsonDocument.Parse(json);

        var result = PackageExtractor.ExtractPackages(doc.RootElement, SbomFormat.SPDX_2_2);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtractPackages_Spdx_ExternalRefsNonPurl_Ignored()
    {
        var json = """
            {
                "spdxVersion": "SPDX-2.2",
                "packages": [
                    {
                        "name": "PackageA",
                        "versionInfo": "1.0.0",
                        "externalRefs": [
                            { "referenceType": "cpe23Type", "referenceLocator": "cpe:2.3:a:vendor:product:1.0.0" }
                        ]
                    }
                ]
            }
            """;
        using var doc = JsonDocument.Parse(json);

        var result = PackageExtractor.ExtractPackages(doc.RootElement, SbomFormat.SPDX_2_2);

        result.Should().HaveCount(1);
        result[0].Purl.Should().BeNull();
        result[0].Ecosystem.Should().BeNull();
    }

    // ─── Existing Test Data ──────────────────────────────────

    [Fact]
    public void ExtractPackages_ExistingCycloneDXTestData_ExtractsComponents()
    {
        using var doc = JsonDocument.Parse(TestData.TestJson.ValidCycloneDXWithComponents);

        var result = PackageExtractor.ExtractPackages(doc.RootElement, SbomFormat.CycloneDX_1_6);

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("ComponentA");
        result[0].Version.Should().Be("1.0.0");
        result[1].Name.Should().Be("ComponentB");
        result[1].Version.Should().Be("2.0.0");
    }

    [Fact]
    public void ExtractPackages_ExistingSpdxTestData_ExtractsPackages()
    {
        using var doc = JsonDocument.Parse(TestData.TestJson.ValidSpdxWithPackages);

        var result = PackageExtractor.ExtractPackages(doc.RootElement, SbomFormat.SPDX_2_2);

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("PackageA");
        result[0].Version.Should().Be("1.0.0");
        result[1].Name.Should().Be("PackageB");
        result[1].Version.Should().Be("2.0.0");
    }

    [Fact]
    public void ExtractPackages_ExistingCycloneDXMinimal_ReturnsEmpty()
    {
        using var doc = JsonDocument.Parse(TestData.TestJson.ValidCycloneDXMinimal);

        var result = PackageExtractor.ExtractPackages(doc.RootElement, SbomFormat.CycloneDX_1_6);

        result.Should().BeEmpty();
    }
}
