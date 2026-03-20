using System.Text.Json;
using FluentAssertions;
using SBOMViewer.Blazor.Models;
using SBOMViewer.Blazor.Services;
using SBOMViewer.Blazor.Tests.TestData;

namespace SBOMViewer.Blazor.Tests.Services;

public class SchemaServiceTests
{
    private readonly SchemaService _service = new();

    // ─── Basic structure ─────────────────────────────────────

    [Fact]
    public void BuildFromJson_RootIsObject()
    {
        using var doc = JsonDocument.Parse(TestJson.ValidCycloneDXMinimal);
        var node = _service.BuildFromJson(doc.RootElement);

        node.NodeType.Should().Be(SchemaNodeType.Object);
        node.PropertyName.Should().Be("root");
    }

    [Fact]
    public void BuildFromJson_DetectsScalarProperties()
    {
        using var doc = JsonDocument.Parse(TestJson.ValidCycloneDXMinimal);
        var node = _service.BuildFromJson(doc.RootElement);

        node.Properties.Should().ContainKey("bomFormat");
        node.Properties["bomFormat"].NodeType.Should().Be(SchemaNodeType.String);

        node.Properties.Should().ContainKey("version");
        node.Properties["version"].NodeType.Should().Be(SchemaNodeType.Number);
    }

    [Fact]
    public void BuildFromJson_DetectsObjectProperties()
    {
        using var doc = JsonDocument.Parse(TestJson.ValidCycloneDXMinimal);
        var node = _service.BuildFromJson(doc.RootElement);

        node.Properties.Should().ContainKey("metadata");
        node.Properties["metadata"].NodeType.Should().Be(SchemaNodeType.Object);
    }

    [Fact]
    public void BuildFromJson_DetectsArrayProperties()
    {
        using var doc = JsonDocument.Parse(TestJson.ValidCycloneDXWithComponents);
        var node = _service.BuildFromJson(doc.RootElement);

        node.Properties.Should().ContainKey("components");
        node.Properties["components"].NodeType.Should().Be(SchemaNodeType.Array);
    }

    [Fact]
    public void BuildFromJson_ArrayItemSchema_HasProperties()
    {
        using var doc = JsonDocument.Parse(TestJson.ValidCycloneDXWithComponents);
        var node = _service.BuildFromJson(doc.RootElement);

        var components = node.Properties["components"];
        components.ItemSchema.Should().NotBeNull();
        components.ItemSchema!.NodeType.Should().Be(SchemaNodeType.Object);
        components.ItemSchema.Properties.Should().ContainKey("name");
    }

    // ─── Property ordering ───────────────────────────────────

    [Fact]
    public void BuildFromJson_PreservesPropertyOrder()
    {
        using var doc = JsonDocument.Parse(TestJson.ValidCycloneDXMinimal);
        var node = _service.BuildFromJson(doc.RootElement);

        node.PropertyOrder.Should().NotBeEmpty();
        node.PropertyOrder[0].Should().Be("bomFormat");
        node.PropertyOrder[1].Should().Be("specVersion");
    }

    // ─── Render hints ────────────────────────────────────────

    [Fact]
    public void BuildFromJson_ScalarProperties_GetKeyValueHint()
    {
        using var doc = JsonDocument.Parse(TestJson.ValidCycloneDXMinimal);
        var node = _service.BuildFromJson(doc.RootElement);

        node.Properties["bomFormat"].Hint.Should().Be(RenderHint.KeyValueGroup);
        node.Properties["specVersion"].Hint.Should().Be(RenderHint.KeyValueGroup);
    }

    [Fact]
    public void BuildFromJson_Components_GetSearchableListHint()
    {
        using var doc = JsonDocument.Parse(TestJson.ValidCycloneDXWithComponents);
        var node = _service.BuildFromJson(doc.RootElement);

        node.Properties["components"].Hint.Should().Be(RenderHint.SearchableList);
    }

    [Fact]
    public void BuildFromJson_Dependencies_GetAccordionHint()
    {
        using var doc = JsonDocument.Parse(TestJson.ValidCycloneDXWithComponents);
        var node = _service.BuildFromJson(doc.RootElement);

        node.Properties["dependencies"].Hint.Should().Be(RenderHint.AccordionSection);
    }

    [Fact]
    public void BuildFromJson_Packages_GetSearchableListHint()
    {
        using var doc = JsonDocument.Parse(TestJson.ValidSpdxWithPackages);
        var node = _service.BuildFromJson(doc.RootElement);

        node.Properties["packages"].Hint.Should().Be(RenderHint.SearchableList);
    }

    [Fact]
    public void BuildFromJson_Relationships_GetAccordionHint()
    {
        using var doc = JsonDocument.Parse(TestJson.ValidSpdxWithPackages);
        var node = _service.BuildFromJson(doc.RootElement);

        node.Properties["relationships"].Hint.Should().Be(RenderHint.AccordionSection);
    }

    // ─── Title humanization ──────────────────────────────────

    [Fact]
    public void BuildFromJson_CamelCaseProperty_GetsHumanizedTitle()
    {
        using var doc = JsonDocument.Parse(TestJson.ValidCycloneDXMinimal);
        var node = _service.BuildFromJson(doc.RootElement);

        node.Properties["bomFormat"].Title.Should().Be("Bom Format");
        node.Properties["specVersion"].Title.Should().Be("Spec Version");
    }

    // ─── SPDX structure ──────────────────────────────────────

    [Fact]
    public void BuildFromJson_Spdx_DetectsAllTopLevelProperties()
    {
        using var doc = JsonDocument.Parse(TestJson.ValidSpdxMinimal);
        var node = _service.BuildFromJson(doc.RootElement);

        node.Properties.Should().ContainKey("spdxVersion");
        node.Properties.Should().ContainKey("name");
        node.Properties.Should().ContainKey("creationInfo");
        node.Properties["creationInfo"].NodeType.Should().Be(SchemaNodeType.Object);
    }

    // ─── CycloneDX 1.7 features ─────────────────────────────

    [Fact]
    public void BuildFromJson_CycloneDX17_DetectsNewSections()
    {
        using var doc = JsonDocument.Parse(TestJson.ValidCycloneDX17WithNewFeatures);
        var node = _service.BuildFromJson(doc.RootElement);

        node.Properties.Should().ContainKey("definitions");
        node.Properties.Should().ContainKey("declarations");
        node.Properties.Should().ContainKey("formulation");
        node.Properties["formulation"].NodeType.Should().Be(SchemaNodeType.Array);
    }

    // ─── Edge cases ──────────────────────────────────────────

    [Fact]
    public void BuildFromJson_EmptyObject_ReturnsEmptyNode()
    {
        using var doc = JsonDocument.Parse("{}");
        var node = _service.BuildFromJson(doc.RootElement);

        node.NodeType.Should().Be(SchemaNodeType.Object);
        node.Properties.Should().BeEmpty();
        node.PropertyOrder.Should().BeEmpty();
    }

    [Fact]
    public void BuildFromJson_BooleanProperty_DetectedCorrectly()
    {
        var json = """{ "filesAnalyzed": true }""";
        using var doc = JsonDocument.Parse(json);
        var node = _service.BuildFromJson(doc.RootElement);

        node.Properties["filesAnalyzed"].NodeType.Should().Be(SchemaNodeType.Boolean);
        node.Properties["filesAnalyzed"].Hint.Should().Be(RenderHint.KeyValueGroup);
    }

    [Fact]
    public void BuildFromJson_EmptyArray_DetectedAsArray()
    {
        var json = """{ "components": [] }""";
        using var doc = JsonDocument.Parse(json);
        var node = _service.BuildFromJson(doc.RootElement);

        node.Properties["components"].NodeType.Should().Be(SchemaNodeType.Array);
        node.Properties["components"].ItemSchema.Should().BeNull();
    }
}
