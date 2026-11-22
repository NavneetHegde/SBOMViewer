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

    // ── Subject (the label shown beside the file name in recents) ──────────────────────────────

    private static string? Subject(string json, SbomFormat format)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        return SbomLoader.ExtractSubject(doc.RootElement, format);
    }

    [Fact]
    public void ExtractSubject_CycloneDx_UsesMetadataComponentNameAndVersion()
    {
        const string json = """
            { "bomFormat": "CycloneDX", "specVersion": "1.6",
              "metadata": { "component": { "type": "application", "name": "storefront", "version": "2.4.1" } },
              "components": [] }
            """;

        Subject(json, SbomFormat.CycloneDX_1_6).Should().Be("storefront 2.4.1");
    }

    [Fact]
    public void ExtractSubject_CycloneDx_NameWithoutVersion()
    {
        const string json = """
            { "metadata": { "component": { "name": "storefront" } } }
            """;

        Subject(json, SbomFormat.CycloneDX_1_5).Should().Be("storefront");
    }

    [Fact]
    public void ExtractSubject_CycloneDx_NoMetadataComponent_IsNull()
    {
        Subject(TestData.TestJson.ValidCycloneDXWithComponents, SbomFormat.CycloneDX_1_6).Should().BeNull();
    }

    [Theory]
    [InlineData(SbomFormat.SPDX_2_2)]
    [InlineData(SbomFormat.SPDX_2_3)]
    public void ExtractSubject_Spdx2_UsesDocumentName(SbomFormat format)
    {
        Subject(TestData.TestJson.ValidSpdxMinimal, format).Should().Be("Test SBOM");
    }

    [Fact]
    public void ExtractSubject_Spdx3_UsesSpdxDocumentElementName()
    {
        Subject(TestData.TestJson.ValidSpdx30WithPackages, SbomFormat.SPDX_3_0)
            .Should().Be("Test SBOM with packages");
    }

    [Fact]
    public void ExtractSubject_Spdx3_NoSpdxDocument_IsNull()
    {
        const string json = """
            { "@graph": [ { "type": "CreationInfo", "specVersion": "3.0.1" } ] }
            """;

        Subject(json, SbomFormat.SPDX_3_0).Should().BeNull();
    }

    [Fact]
    public void ExtractSubject_BlankName_IsNull()
    {
        Subject("""{ "name": "   " }""", SbomFormat.SPDX_2_3).Should().BeNull();
    }

    [Fact]
    public void ExtractSubject_LongName_IsTruncated()
    {
        var longName = "https://example.org/spdxdocs/" + new string('x', 100);
        var subject = Subject($$"""{ "name": "{{longName}}" }""", SbomFormat.SPDX_2_3);

        subject.Should().NotBeNull();
        subject!.Length.Should().Be(SbomLoader.MaxSubjectLength);
        subject.Should().EndWith("…");
    }

    [Fact]
    public void ExtractSubject_NoFormat_IsNull()
    {
        Subject(TestData.TestJson.ValidSpdxMinimal, (SbomFormat)999).Should().BeNull();
    }
}
