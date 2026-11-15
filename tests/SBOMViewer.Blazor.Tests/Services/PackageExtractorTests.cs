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
    public void ExtractPackages_CycloneDX_JoinsGroupIntoTheNameOsvIndexes()
    {
        // CycloneDX carries the Maven groupId and the npm scope in a separate "group" field. OSV
        // indexes those packages as group:artifact and @scope/name, so the bare name finds nothing.
        var json = """
            {
                "bomFormat": "CycloneDX",
                "specVersion": "1.6",
                "components": [
                    { "group": "org.apache.logging.log4j", "name": "log4j-core", "version": "2.14.1", "purl": "pkg:maven/org.apache.logging.log4j/log4j-core@2.14.1" },
                    { "group": "@babel", "name": "traverse", "version": "7.22.0", "purl": "pkg:npm/%40babel/traverse@7.22.0" },
                    { "group": "@babel", "name": "@babel/core", "version": "7.22.0", "purl": "pkg:npm/%40babel/core@7.22.0" },
                    { "group": "org.example", "name": "org.example:already-joined", "version": "1.0.0", "purl": "pkg:maven/org.example/already-joined@1.0.0" },
                    { "group": "Microsoft", "name": "Newtonsoft.Json", "version": "13.0.3", "purl": "pkg:nuget/Newtonsoft.Json@13.0.3" },
                    { "group": "org.example", "name": "no-purl", "version": "1.0.0" }
                ]
            }
            """;
        using var doc = JsonDocument.Parse(json);

        var result = PackageExtractor.ExtractPackages(doc.RootElement, SbomFormat.CycloneDX_1_6);

        result.Select(p => p.Name).Should().Equal(
            "org.apache.logging.log4j:log4j-core",
            "@babel/traverse",
            "@babel/core",
            "org.example:already-joined",
            "Newtonsoft.Json",   // other ecosystems have no group in their OSV name
            "no-purl");          // no purl, no known ecosystem — leave the name alone
    }

    [Fact]
    public void ExtractPackages_CycloneDX_DerivesTheOsvNameFromThePurlWhenThereIsNoUsableGroup()
    {
        var json = """
            {
                "bomFormat": "CycloneDX",
                "specVersion": "1.6",
                "components": [
                    { "name": "log4j-core", "version": "2.14.1", "purl": "pkg:maven/org.apache.logging.log4j/log4j-core@2.14.1?type=jar" },
                    { "group": "babel", "name": "core", "version": "7.22.0", "purl": "pkg:npm/%40babel/core@7.22.0" },
                    { "group": "babel", "name": "parser", "version": "7.22.0", "purl": "pkg:npm/parser@7.22.0" },
                    { "group": "github.com/gin-gonic", "name": "gin", "version": "v1.9.0", "purl": "pkg:golang/github.com/gin-gonic/gin@v1.9.0" },
                    { "group": "symfony", "name": "console", "version": "6.3.0", "purl": "pkg:composer/symfony/console@6.3.0" },
                    { "name": "lodash", "version": "4.17.21", "purl": "pkg:npm/lodash@4.17.21" }
                ]
            }
            """;
        using var doc = JsonDocument.Parse(json);

        var result = PackageExtractor.ExtractPackages(doc.RootElement, SbomFormat.CycloneDX_1_6);

        result.Select(p => p.Name).Should().Equal(
            "org.apache.logging.log4j:log4j-core",
            "@babel/core",
            "@babel/parser",                // purl has no namespace — fall back to the group, adding the "@"
            "github.com/gin-gonic/gin",
            "symfony/console",
            "lodash");
    }

    [Fact]
    public void ExtractPackages_Spdx_DerivesTheOsvNameFromThePurl()
    {
        // SPDX has no "group" field at all, so the purl is the only place the namespace lives.
        var spdx2 = """
            {
                "spdxVersion": "SPDX-2.3",
                "packages": [
                    {
                        "name": "log4j-core", "versionInfo": "2.14.1",
                        "externalRefs": [ { "referenceCategory": "PACKAGE-MANAGER", "referenceType": "purl", "referenceLocator": "pkg:maven/org.apache.logging.log4j/log4j-core@2.14.1" } ]
                    }
                ]
            }
            """;
        var spdx3 = """
            {
                "@graph": [
                    { "type": "software_Package", "name": "traverse", "software_packageVersion": "7.22.0", "software_packageUrl": "pkg:npm/%40babel/traverse@7.22.0" }
                ]
            }
            """;
        using var doc2 = JsonDocument.Parse(spdx2);
        using var doc3 = JsonDocument.Parse(spdx3);

        PackageExtractor.ExtractPackages(doc2.RootElement, SbomFormat.SPDX_2_3)
            .Single().Name.Should().Be("org.apache.logging.log4j:log4j-core");
        PackageExtractor.ExtractPackages(doc3.RootElement, SbomFormat.SPDX_3_0)
            .Single().Name.Should().Be("@babel/traverse");
    }

    [Fact]
    public void OsvName_MatchesBetweenScanPackagesAndComponentRows()
    {
        // The Components tab joins scan results onto its rows by this name. If the two extractors
        // ever disagree, every namespaced component silently shows "no vulnerabilities".
        using var doc = JsonDocument.Parse("""
            {
                "bomFormat": "CycloneDX",
                "specVersion": "1.6",
                "components": [
                    { "group": "org.apache.logging.log4j", "name": "log4j-core", "version": "2.14.1", "purl": "pkg:maven/org.apache.logging.log4j/log4j-core@2.14.1" },
                    { "group": "@babel", "name": "traverse", "version": "7.22.0", "purl": "pkg:npm/%40babel/traverse@7.22.0" },
                    { "name": "Newtonsoft.Json", "version": "13.0.3", "purl": "pkg:nuget/Newtonsoft.Json@13.0.3" },
                    { "name": "no-purl", "version": "1.0.0" }
                ]
            }
            """);

        var scanned = PackageExtractor.ExtractPackages(doc.RootElement, SbomFormat.CycloneDX_1_6).Select(p => p.Name);
        var rowKeys = ComponentRowExtractor.Extract(doc.RootElement, SbomFormat.CycloneDX_1_6)
            .Select(r => PackageExtractor.OsvName(r.Name, r.Purl));

        rowKeys.Should().Equal(scanned);
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

    // ─── CycloneDX 1.5 ──────────────────────────────────────

    [Fact]
    public void ExtractPackages_CycloneDX15_ExtractsComponents()
    {
        using var doc = JsonDocument.Parse(TestData.TestJson.ValidCycloneDX15WithComponents);

        var result = PackageExtractor.ExtractPackages(doc.RootElement, SbomFormat.CycloneDX_1_5);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("ComponentA");
        result[0].Version.Should().Be("1.0.0");
    }

    // ─── SPDX 2.3 ──────────────────────────────────────────

    [Fact]
    public void ExtractPackages_Spdx23_ExtractsPackages()
    {
        using var doc = JsonDocument.Parse(TestData.TestJson.ValidSpdx23WithPackages);

        var result = PackageExtractor.ExtractPackages(doc.RootElement, SbomFormat.SPDX_2_3);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("PackageA");
        result[0].Version.Should().Be("1.0.0");
        result[0].Ecosystem.Should().Be("npm");
    }

    [Fact]
    public void ExtractPackages_Spdx23_IgnoresSecurityRefs_StillFindsPurl()
    {
        using var doc = JsonDocument.Parse(TestData.TestJson.ValidSpdx23WithSecurityAndSupplyChainFields);

        var result = PackageExtractor.ExtractPackages(doc.RootElement, SbomFormat.SPDX_2_3);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("PackageA");
        result[0].Version.Should().Be("1.0.0");
        result[0].Purl.Should().Be("pkg:npm/PackageA@1.0.0");
        result[0].Ecosystem.Should().Be("npm");
    }

    // ─── SPDX 3.0.1 ──────────────────────────────────────────

    [Fact]
    public void ExtractPackages_Spdx30_ExtractsSoftwarePackages()
    {
        using var doc = JsonDocument.Parse(TestData.TestJson.ValidSpdx30WithPackages);

        var result = PackageExtractor.ExtractPackages(doc.RootElement, SbomFormat.SPDX_3_0);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("PackageA");
        result[0].Version.Should().Be("1.0.0");
        result[0].Ecosystem.Should().Be("npm");
        result[0].Purl.Should().Be("pkg:npm/PackageA@1.0.0");
    }

    [Fact]
    public void ExtractPackages_Spdx30_IgnoresSecurityAndBuildProfileElements_StillFindsPackage()
    {
        using var doc = JsonDocument.Parse(TestData.TestJson.ValidSpdx30WithSecurityAndBuildProfile);

        var result = PackageExtractor.ExtractPackages(doc.RootElement, SbomFormat.SPDX_3_0);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("PackageA");
        result[0].Version.Should().Be("1.0.0");
        result[0].Purl.Should().Be("pkg:npm/PackageA@1.0.0");
    }

    [Fact]
    public void ExtractPackages_Spdx30_IgnoresNonPackageElements()
    {
        using var doc = JsonDocument.Parse(TestData.TestJson.ValidSpdx30Minimal);

        var result = PackageExtractor.ExtractPackages(doc.RootElement, SbomFormat.SPDX_3_0);

        result.Should().BeEmpty();
    }
}
