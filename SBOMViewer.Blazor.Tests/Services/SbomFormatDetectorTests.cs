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
    public void CycloneDX15_ReturnsUnsupportedVersion()
    {
        var json = """
            {
                "bomFormat": "CycloneDX",
                "specVersion": "1.5",
                "version": 1,
                "metadata": { "timestamp": "2024-01-01T00:00:00Z" }
            }
            """;

        var result = SbomFormatDetector.DetectWithDetails(json);

        result.Format.Should().BeNull();
        result.IsUnsupportedVersion.Should().BeTrue();
        result.DetectedVersion.Should().Be("CycloneDX 1.5");
    }

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
    public void Spdx23_ReturnsUnsupportedVersion()
    {
        var json = """
            {
                "spdxVersion": "SPDX-2.3",
                "dataLicense": "CC0-1.0",
                "SPDXID": "SPDXRef-DOCUMENT",
                "name": "test",
                "documentNamespace": "https://example.org/test"
            }
            """;

        var result = SbomFormatDetector.DetectWithDetails(json);

        result.Format.Should().BeNull();
        result.IsUnsupportedVersion.Should().BeTrue();
        result.DetectedVersion.Should().Be("SPDX-2.3");
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
        SbomFormatDetector.SupportedVersions.Should().Contain("CycloneDX 1.6");
        SbomFormatDetector.SupportedVersions.Should().Contain("CycloneDX 1.7");
        SbomFormatDetector.SupportedVersions.Should().Contain("SPDX 2.2");
        SbomFormatDetector.SupportedVersions.Should().HaveCount(3);
    }
}
