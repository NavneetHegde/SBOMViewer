using FluentAssertions;
using SBOMViewer.Blazor.Services;
using SBOMViewer.Blazor.Tests.TestData;

namespace SBOMViewer.Blazor.Tests.Services;

public class CycloneDXParserTests
{
    [Fact]
    public async Task NullInput_ReturnsNull()
    {
        var result = await CycloneDXParser.ParseCycloneDXBomJsonAsyncParseJson(null!);

        result.Should().BeNull();
    }

    [Fact]
    public async Task EmptyString_ReturnsNull()
    {
        var result = await CycloneDXParser.ParseCycloneDXBomJsonAsyncParseJson("");

        result.Should().BeNull();
    }

    [Fact]
    public async Task WhitespaceOnly_ReturnsNull()
    {
        var result = await CycloneDXParser.ParseCycloneDXBomJsonAsyncParseJson("   ");

        result.Should().BeNull();
    }

    [Fact]
    public async Task MalformedJson_ReturnsNull()
    {
        var result = await CycloneDXParser.ParseCycloneDXBomJsonAsyncParseJson("{not valid json}}");

        result.Should().BeNull();
    }

    [Fact]
    public async Task MissingBomFormat_ReturnsNull()
    {
        var result = await CycloneDXParser.ParseCycloneDXBomJsonAsyncParseJson(TestJson.CycloneDXMissingBomFormat);

        result.Should().BeNull();
    }

    [Fact]
    public async Task MissingMetadata_ReturnsNull()
    {
        var result = await CycloneDXParser.ParseCycloneDXBomJsonAsyncParseJson(TestJson.CycloneDXMissingMetadata);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidMinimal_ReturnsDocument()
    {
        var result = await CycloneDXParser.ParseCycloneDXBomJsonAsyncParseJson(TestJson.ValidCycloneDXMinimal);

        result.Should().NotBeNull();
        result!.BomFormat.Should().Be("CycloneDX");
        result.SpecVersion.Should().Be("1.6");
        result.Metadata.Should().NotBeNull();
        result.Metadata.Timestamp.Should().Be("2024-01-01T00:00:00Z");
    }

    [Fact]
    public async Task ValidWithComponents_ParsesComponents()
    {
        var result = await CycloneDXParser.ParseCycloneDXBomJsonAsyncParseJson(TestJson.ValidCycloneDXWithComponents);

        result.Should().NotBeNull();
        result!.Components.Should().HaveCount(2);
        result.Components[0].Name.Should().Be("ComponentA");
        result.Components[1].Name.Should().Be("ComponentB");
        result.Components[0].Licenses.Should().HaveCount(1);
        result.Components[0].Licenses[0].License.Id.Should().Be("MIT");
        result.Dependencies.Should().HaveCount(1);
        result.Dependencies[0].Ref.Should().Be("comp-1");
        result.Dependencies[0].DependsOn.Should().Contain("comp-2");
    }

    [Fact]
    public async Task JsonWithTrailingComma_Succeeds()
    {
        var json = """
            {
                "bomFormat": "CycloneDX",
                "specVersion": "1.6",
                "version": 1,
                "metadata": { "timestamp": "2024-01-01T00:00:00Z", },
            }
            """;

        var result = await CycloneDXParser.ParseCycloneDXBomJsonAsyncParseJson(json);

        result.Should().NotBeNull();
        result!.BomFormat.Should().Be("CycloneDX");
    }

    [Fact]
    public async Task JsonWithComments_Succeeds()
    {
        var json = """
            {
                // This is a comment
                "bomFormat": "CycloneDX",
                "specVersion": "1.6",
                "version": 1,
                "metadata": { "timestamp": "2024-01-01T00:00:00Z" }
            }
            """;

        var result = await CycloneDXParser.ParseCycloneDXBomJsonAsyncParseJson(json);

        result.Should().NotBeNull();
        result!.BomFormat.Should().Be("CycloneDX");
    }

    // ─── CycloneDX 1.7 Tests ──────────────────────────────────

    [Fact]
    public async Task Valid17Minimal_ReturnsDocument()
    {
        var result = await CycloneDXParser.ParseCycloneDXBomJsonAsyncParseJson(TestJson.ValidCycloneDX17Minimal);

        result.Should().NotBeNull();
        result!.BomFormat.Should().Be("CycloneDX");
        result.SpecVersion.Should().Be("1.7");
        result.Metadata.Should().NotBeNull();
        result.Metadata!.Timestamp.Should().Be("2025-06-01T00:00:00Z");
    }

    [Fact]
    public async Task Valid17WithLifecycles_ParsesLifecycles()
    {
        var result = await CycloneDXParser.ParseCycloneDXBomJsonAsyncParseJson(TestJson.ValidCycloneDX17Minimal);

        result.Should().NotBeNull();
        result!.Metadata!.Lifecycles.Should().NotBeNull();
        result.Metadata.Lifecycles.Should().HaveCount(2);
        result.Metadata.Lifecycles![0].Phase.Should().Be("build");
        result.Metadata.Lifecycles[1].Phase.Should().Be("operations");
    }

    [Fact]
    public async Task Valid17WithComponentTags_ParsesTags()
    {
        var result = await CycloneDXParser.ParseCycloneDXBomJsonAsyncParseJson(TestJson.ValidCycloneDX17WithNewFeatures);

        result.Should().NotBeNull();
        result!.Components.Should().HaveCount(1);

        var comp = result.Components[0];
        comp.Tags.Should().NotBeNull();
        comp.Tags.Should().Contain("security");
        comp.Tags.Should().Contain("crypto");
        comp.OmniborId.Should().NotBeNull();
        comp.OmniborId.Should().Contain("gitoid:blob:sha256:abc123");
        comp.Swhid.Should().NotBeNull();
        comp.Swhid.Should().Contain("swh:1:cnt:def456");
    }

    [Fact]
    public async Task Valid17WithDefinitions_ParsesStandards()
    {
        var result = await CycloneDXParser.ParseCycloneDXBomJsonAsyncParseJson(TestJson.ValidCycloneDX17WithNewFeatures);

        result.Should().NotBeNull();
        result!.Definitions.Should().NotBeNull();
        result.Definitions!.Standards.Should().HaveCount(1);
        result.Definitions.Standards![0].Name.Should().Be("NIST SP 800-53");
        result.Definitions.Standards[0].Version.Should().Be("5.0");
    }

    [Fact]
    public async Task Valid17WithDeclarations_ParsesClaimsAndAssessors()
    {
        var result = await CycloneDXParser.ParseCycloneDXBomJsonAsyncParseJson(TestJson.ValidCycloneDX17WithNewFeatures);

        result.Should().NotBeNull();
        result!.Declarations.Should().NotBeNull();
        result.Declarations!.Assessors.Should().HaveCount(1);
        result.Declarations.Assessors![0].Organization!.Name.Should().Be("Security Corp");
        result.Declarations.Claims.Should().HaveCount(1);
        result.Declarations.Claims![0].Target.Should().Be("comp-1");
    }

    [Fact]
    public async Task Valid17WithFormulation_ParsesFormulas()
    {
        var result = await CycloneDXParser.ParseCycloneDXBomJsonAsyncParseJson(TestJson.ValidCycloneDX17WithNewFeatures);

        result.Should().NotBeNull();
        result!.Formulation.Should().NotBeNull();
        result.Formulation.Should().HaveCount(1);
        result.Formulation![0].BomRef.Should().Be("formula-1");
        result.Formulation[0].Components.Should().HaveCount(1);
        result.Formulation[0].Components![0].Name.Should().Be("BuildTool");
    }

    [Fact]
    public async Task Valid16Json_LeavesNew17PropertiesNull()
    {
        var result = await CycloneDXParser.ParseCycloneDXBomJsonAsyncParseJson(TestJson.ValidCycloneDXMinimal);

        result.Should().NotBeNull();
        result!.SpecVersion.Should().Be("1.6");
        result.Formulation.Should().BeNull();
        result.Declarations.Should().BeNull();
        result.Definitions.Should().BeNull();
        result.Metadata!.Lifecycles.Should().BeNull();
    }
}
