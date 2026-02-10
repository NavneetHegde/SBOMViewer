using FluentAssertions;
using SBOMViewer.Blazor.Services;
using SBOMViewer.Blazor.Tests.TestData;

namespace SBOMViewer.Blazor.Tests.Services;

public class SpdxParserTests
{
    [Fact]
    public async Task NullInput_ReturnsNull()
    {
        var result = await SpdxParser.ParseSpdxJsonAsync(null!);

        result.Should().BeNull();
    }

    [Fact]
    public async Task EmptyString_ReturnsNull()
    {
        var result = await SpdxParser.ParseSpdxJsonAsync("");

        result.Should().BeNull();
    }

    [Fact]
    public async Task WhitespaceOnly_ReturnsNull()
    {
        var result = await SpdxParser.ParseSpdxJsonAsync("   ");

        result.Should().BeNull();
    }

    [Fact]
    public async Task MalformedJson_ReturnsNull()
    {
        var result = await SpdxParser.ParseSpdxJsonAsync("{not valid json}}");

        result.Should().BeNull();
    }

    [Fact]
    public async Task MissingName_ReturnsNull()
    {
        var result = await SpdxParser.ParseSpdxJsonAsync(TestJson.SpdxMissingName);

        result.Should().BeNull();
    }

    [Fact]
    public async Task NullCreationInfo_ReturnsNull()
    {
        var result = await SpdxParser.ParseSpdxJsonAsync(TestJson.SpdxNullCreationInfo);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidMinimal_ReturnsDocument()
    {
        var result = await SpdxParser.ParseSpdxJsonAsync(TestJson.ValidSpdxMinimal);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Test SBOM");
        result.SpdxVersion.Should().Be("SPDX-2.2");
        result.CreationInfo.Should().NotBeNull();
        result.CreationInfo.Created.Should().Be("2024-01-01T00:00:00Z");
    }

    [Fact]
    public async Task ValidWithPackages_ParsesPackages()
    {
        var result = await SpdxParser.ParseSpdxJsonAsync(TestJson.ValidSpdxWithPackages);

        result.Should().NotBeNull();
        result!.Packages.Should().HaveCount(2);
        result.Packages[0].Name.Should().Be("PackageA");
        result.Packages[1].Name.Should().Be("PackageB");
        result.Relationships.Should().HaveCount(1);
        result.Relationships[0].RelationshipType.Should().Be("DESCRIBES");
    }

    [Fact]
    public async Task JsonWithTrailingComma_Succeeds()
    {
        var json = """
            {
                "spdxVersion": "SPDX-2.2",
                "name": "Trailing Comma Test",
                "creationInfo": { "created": "2024-01-01T00:00:00Z", "creators": ["Tool: test",], },
            }
            """;

        var result = await SpdxParser.ParseSpdxJsonAsync(json);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Trailing Comma Test");
    }

    [Fact]
    public async Task JsonWithComments_Succeeds()
    {
        var json = """
            {
                // This is a comment
                "spdxVersion": "SPDX-2.2",
                "name": "Comment Test",
                "creationInfo": { "created": "2024-01-01T00:00:00Z", "creators": ["Tool: test"] }
            }
            """;

        var result = await SpdxParser.ParseSpdxJsonAsync(json);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Comment Test");
    }
}
