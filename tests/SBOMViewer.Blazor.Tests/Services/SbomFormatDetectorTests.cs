using System.Text.Json;
using FluentAssertions;
using SBOMViewer.Blazor.Models;
using SBOMViewer.Blazor.Services;
using SBOMViewer.Blazor.Tests.TestData;

namespace SBOMViewer.Blazor.Tests.Services;

public class SbomFormatDetectorTests
{
    [Fact]
    public void NullInput_ReturnsNull()
    {
        var result = SbomFormatDetector.Detect(null!);

        result.Should().BeNull();
    }

    [Fact]
    public void EmptyString_ReturnsNull()
    {
        var result = SbomFormatDetector.Detect("");

        result.Should().BeNull();
    }

    [Fact]
    public void MalformedJson_ReturnsNull()
    {
        var result = SbomFormatDetector.Detect("{not valid json}}");

        result.Should().BeNull();
    }

    [Fact]
    public void UnrecognizedFormat_ReturnsNull()
    {
        var json = """{ "format": "unknown", "version": "1.0" }""";

        var result = SbomFormatDetector.Detect(json);

        result.Should().BeNull();
    }

    [Fact]
    public void CycloneDX16_DetectsCycloneDX16()
    {
        var result = SbomFormatDetector.Detect(TestJson.ValidCycloneDXMinimal);

        result.Should().Be(SbomFormat.CycloneDX_1_6);
    }

    [Fact]
    public void CycloneDX17_DetectsCycloneDX17()
    {
        var result = SbomFormatDetector.Detect(TestJson.ValidCycloneDX17Minimal);

        result.Should().Be(SbomFormat.CycloneDX_1_7);
    }

    [Fact]
    public void Spdx22_DetectsSpdx22()
    {
        var result = SbomFormatDetector.Detect(TestJson.ValidSpdxMinimal);

        result.Should().Be(SbomFormat.SPDX_2_2);
    }

    [Fact]
    public void CycloneDX15_DetectsCycloneDX15()
    {
        var result = SbomFormatDetector.Detect(TestJson.ValidCycloneDX15Minimal);

        result.Should().Be(SbomFormat.CycloneDX_1_5);
    }

    [Fact]
    public void Spdx23_DetectsSpdx23()
    {
        var result = SbomFormatDetector.Detect(TestJson.ValidSpdx23Minimal);

        result.Should().Be(SbomFormat.SPDX_2_3);
    }

    [Fact]
    public void Spdx30_DetectsSpdx30()
    {
        var result = SbomFormatDetector.Detect(TestJson.ValidSpdx30Minimal);

        result.Should().Be(SbomFormat.SPDX_3_0);
    }

    [Fact]
    public void Spdx30_UnsupportedSpecVersion_ReturnsUnsupportedVersion()
    {
        var json = """
            {
                "@context": "https://spdx.org/rdf/3.0.1/spdx-context.jsonld",
                "@graph": [
                    { "type": "CreationInfo", "specVersion": "3.1.0" }
                ]
            }
            """;

        var result = SbomFormatDetector.DetectWithDetails(json);

        result.Format.Should().BeNull();
        result.IsUnsupportedVersion.Should().BeTrue();
        result.DetectedVersion.Should().Be("SPDX 3.1.0");
    }

    [Fact]
    public void CycloneDXWithoutSpecVersion_DefaultsToCycloneDX16()
    {
        var json = """
            {
                "bomFormat": "CycloneDX",
                "version": 1,
                "metadata": { "timestamp": "2024-01-01T00:00:00Z" }
            }
            """;

        var result = SbomFormatDetector.Detect(json);

        result.Should().Be(SbomFormat.CycloneDX_1_6);
    }

    [Fact]
    public void CycloneDXCaseInsensitive_DetectsFormat()
    {
        var json = """
            {
                "bomFormat": "cyclonedx",
                "specVersion": "1.7",
                "version": 1,
                "metadata": { "timestamp": "2024-01-01T00:00:00Z" }
            }
            """;

        var result = SbomFormatDetector.Detect(json);

        result.Should().Be(SbomFormat.CycloneDX_1_7);
    }

    // ─── Unsupported version tests ─────────────────────────────

    [Fact]
    public void CycloneDX14_ReturnsUnsupportedVersion()
    {
        var json = """
            {
                "bomFormat": "CycloneDX",
                "specVersion": "1.4",
                "version": 1,
                "metadata": { "timestamp": "2024-01-01T00:00:00Z" }
            }
            """;

        var result = SbomFormatDetector.DetectWithDetails(json);

        result.Format.Should().BeNull();
        result.IsUnsupportedVersion.Should().BeTrue();
        result.DetectedVersion.Should().Be("CycloneDX 1.4");
    }

    [Fact]
    public void Spdx24_ReturnsUnsupportedVersion()
    {
        var json = """
            {
                "spdxVersion": "SPDX-2.4",
                "dataLicense": "CC0-1.0",
                "SPDXID": "SPDXRef-DOCUMENT",
                "name": "test",
                "documentNamespace": "https://example.org/test"
            }
            """;

        var result = SbomFormatDetector.DetectWithDetails(json);

        result.Format.Should().BeNull();
        result.IsUnsupportedVersion.Should().BeTrue();
        result.DetectedVersion.Should().Be("SPDX-2.4");
    }

    [Fact]
    public void UnrecognizedFormat_IsNotUnsupportedVersion()
    {
        var json = """{ "format": "unknown", "version": "1.0" }""";

        var result = SbomFormatDetector.DetectWithDetails(json);

        result.Format.Should().BeNull();
        result.IsUnsupportedVersion.Should().BeFalse();
        result.DetectedVersion.Should().BeNull();
    }

    [Fact]
    public void SupportedVersions_ContainsExpectedFormats()
    {
        SbomFormatDetector.SupportedVersions.Should().Contain("CycloneDX 1.5");
        SbomFormatDetector.SupportedVersions.Should().Contain("CycloneDX 1.6");
        SbomFormatDetector.SupportedVersions.Should().Contain("CycloneDX 1.7");
        SbomFormatDetector.SupportedVersions.Should().Contain("SPDX 2.2");
        SbomFormatDetector.SupportedVersions.Should().Contain("SPDX 2.3");
        SbomFormatDetector.SupportedVersions.Should().Contain("SPDX 3.0.1");
        SbomFormatDetector.SupportedVersions.Should().HaveCount(6);
    }

    // ─── Lightweight validation tests ─────────────────────────

    [Fact]
    public void Validate_ValidCycloneDX_ReturnsNull()
    {
        using var doc = JsonDocument.Parse(TestJson.ValidCycloneDXWithComponents);
        var result = SbomFormatDetector.Validate(doc.RootElement, SbomFormat.CycloneDX_1_6);

        result.Should().BeNull();
    }

    [Fact]
    public void Validate_CycloneDXMissingComponents_ReportsError()
    {
        using var doc = JsonDocument.Parse(TestJson.ValidCycloneDXMinimal);
        var result = SbomFormatDetector.Validate(doc.RootElement, SbomFormat.CycloneDX_1_6);

        result.Should().NotBeNull();
        result.Should().Contain("components");
    }

    [Fact]
    public void Validate_CycloneDXMissingMetadata_ReportsError()
    {
        using var doc = JsonDocument.Parse(TestJson.CycloneDXMissingMetadata);
        var result = SbomFormatDetector.Validate(doc.RootElement, SbomFormat.CycloneDX_1_7);

        result.Should().NotBeNull();
        result.Should().Contain("metadata");
        result.Should().Contain("components");
    }

    [Fact]
    public void Validate_CycloneDXMissingBomFormat_ReportsError()
    {
        using var doc = JsonDocument.Parse(TestJson.CycloneDXMissingBomFormat);
        var result = SbomFormatDetector.Validate(doc.RootElement, SbomFormat.CycloneDX_1_6);

        result.Should().NotBeNull();
        result.Should().Contain("bomFormat");
    }

    [Fact]
    public void Validate_ValidSpdx_ReturnsNull()
    {
        using var doc = JsonDocument.Parse(TestJson.ValidSpdxMinimal);
        var result = SbomFormatDetector.Validate(doc.RootElement, SbomFormat.SPDX_2_2);

        result.Should().BeNull();
    }

    [Fact]
    public void Validate_SpdxMissingName_ReportsError()
    {
        using var doc = JsonDocument.Parse(TestJson.SpdxMissingName);
        var result = SbomFormatDetector.Validate(doc.RootElement, SbomFormat.SPDX_2_2);

        result.Should().NotBeNull();
        result.Should().Contain("name");
    }

    [Fact]
    public void Validate_SpdxNullCreationInfo_ReportsError()
    {
        using var doc = JsonDocument.Parse(TestJson.SpdxNullCreationInfo);
        var result = SbomFormatDetector.Validate(doc.RootElement, SbomFormat.SPDX_2_2);

        result.Should().NotBeNull();
        result.Should().Contain("creationInfo");
    }

    [Fact]
    public void Validate_SpdxMissingMultipleFields_ReportsAll()
    {
        var json = """{ "spdxVersion": "SPDX-2.2" }""";
        using var doc = JsonDocument.Parse(json);
        var result = SbomFormatDetector.Validate(doc.RootElement, SbomFormat.SPDX_2_2);

        result.Should().NotBeNull();
        result.Should().Contain("name");
        result.Should().Contain("SPDXID");
        result.Should().Contain("dataLicense");
        result.Should().Contain("documentNamespace");
        result.Should().Contain("creationInfo");
    }

    [Fact]
    public void Validate_ValidCycloneDX15_ReturnsNull()
    {
        using var doc = JsonDocument.Parse(TestJson.ValidCycloneDX15WithComponents);
        var result = SbomFormatDetector.Validate(doc.RootElement, SbomFormat.CycloneDX_1_5);

        result.Should().BeNull();
    }

    [Fact]
    public void Validate_ValidSpdx23_ReturnsNull()
    {
        using var doc = JsonDocument.Parse(TestJson.ValidSpdx23WithPackages);
        var result = SbomFormatDetector.Validate(doc.RootElement, SbomFormat.SPDX_2_3);

        result.Should().BeNull();
    }

    [Fact]
    public void Validate_ValidSpdx30_ReturnsNull()
    {
        using var doc = JsonDocument.Parse(TestJson.ValidSpdx30WithPackages);
        var result = SbomFormatDetector.Validate(doc.RootElement, SbomFormat.SPDX_3_0);

        result.Should().BeNull();
    }

    [Fact]
    public void Validate_Spdx30MissingGraph_ReportsError()
    {
        var json = """{ "@context": "https://spdx.org/rdf/3.0.1/spdx-context.jsonld" }""";
        using var doc = JsonDocument.Parse(json);
        var result = SbomFormatDetector.Validate(doc.RootElement, SbomFormat.SPDX_3_0);

        result.Should().NotBeNull();
        result.Should().Contain("@graph");
    }

    [Fact]
    public void Validate_Spdx30MissingCreationInfo_ReportsError()
    {
        var json = """
            {
                "@graph": [
                    { "type": "SpdxDocument", "spdxId": "https://example.org/doc1" }
                ]
            }
            """;
        using var doc = JsonDocument.Parse(json);
        var result = SbomFormatDetector.Validate(doc.RootElement, SbomFormat.SPDX_3_0);

        result.Should().NotBeNull();
        result.Should().Contain("CreationInfo");
    }
}
