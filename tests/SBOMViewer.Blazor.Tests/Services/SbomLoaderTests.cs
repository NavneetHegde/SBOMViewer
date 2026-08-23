using FluentAssertions;
using SBOMViewer.Blazor.Models;
using SBOMViewer.Blazor.Services;

namespace SBOMViewer.Blazor.Tests.Services;

/// <summary>
/// Covers the shared detect → validate → build-schema pipeline used by both the single-document
/// upload screen and the comparison screen.
/// </summary>
public class SbomLoaderTests
{
    private static SbomLoader Loader() => new(new SchemaService());

    [Fact]
    public void Load_ValidCycloneDx_Succeeds()
    {
        var result = Loader().Load(TestData.TestJson.ValidCycloneDXWithComponents, "sbom.json");

        result.Success.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Document.Should().NotBeNull();
        result.Schema.Should().NotBeNull();
        result.Format.Should().Be(SbomFormat.CycloneDX_1_6);
        result.FileName.Should().Be("sbom.json");
    }

    [Fact]
    public void Load_ValidSpdx_Succeeds()
    {
        var result = Loader().Load(TestData.TestJson.ValidSpdxWithPackages, "sbom.json");

        result.Success.Should().BeTrue();
        result.Format.Should().Be(SbomFormat.SPDX_2_2);
    }

    [Fact]
    public void Load_MalformedJson_ReportsUnrecognizedFormat()
    {
        // SbomFormatDetector.DetectWithDetails parses inside a try/catch and returns "no format"
        // on JsonException, so malformed input is reported as an unrecognized format rather than
        // as a parse failure. FileUploadTests asserts this same wording end to end.
        var result = Loader().Load("{ not json", "bad.json");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Unrecognized SBOM format");
        result.Document.Should().BeNull();
    }

    [Fact]
    public void Load_UnrecognizedFormat_ReturnsFormatError()
    {
        var result = Loader().Load("""{ "hello": "world" }""", "other.json");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Unrecognized SBOM format");
    }

    [Fact]
    public void Load_FailingValidation_ReturnsValidationErrorAndNoDocument()
    {
        var result = Loader().Load(TestData.TestJson.CycloneDXMissingMetadata, "sbom.json");

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
        // The parsed document must be disposed rather than leaked when validation rejects it.
        result.Document.Should().BeNull();
    }

    [Fact]
    public void Failed_ProducesUnsuccessfulResult()
    {
        var result = SbomLoadResult.Failed("nope");

        result.Success.Should().BeFalse();
        result.Error.Should().Be("nope");
        result.Document.Should().BeNull();
    }
}
