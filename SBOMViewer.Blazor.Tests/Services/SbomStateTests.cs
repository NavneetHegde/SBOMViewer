using System.Text.Json;
using FluentAssertions;
using SBOMViewer.Blazor.Models;
using SBOMViewer.Blazor.Services;

namespace SBOMViewer.Blazor.Tests.Services;

public class SbomStateTests
{
    [Fact]
    public void Document_Set_TriggersOnChange()
    {
        var state = new SbomState();
        var fired = false;
        state.OnChange += () => fired = true;

        state.Document = JsonDocument.Parse("{}");

        fired.Should().BeTrue();
    }

    [Fact]
    public void Document_SetNull_TriggersOnChange()
    {
        var state = new SbomState();
        state.Document = JsonDocument.Parse("{}");

        var fired = false;
        state.OnChange += () => fired = true;

        state.Document = null;

        fired.Should().BeTrue();
    }

    [Fact]
    public void Document_Set_ValuePersisted()
    {
        var state = new SbomState();
        var doc = JsonDocument.Parse("""{ "bomFormat": "CycloneDX" }""");

        state.Document = doc;

        state.Document.Should().BeSameAs(doc);
    }

    [Fact]
    public void Clear_ResetsAllProperties()
    {
        var state = new SbomState();
        state.Document = JsonDocument.Parse("{}");
        state.Schema = new SchemaNode { PropertyName = "root" };
        state.DetectedFormat = SbomFormat.CycloneDX_1_6;
        state.FileName = "test.json";

        state.Clear();

        state.Document.Should().BeNull();
        state.Schema.Should().BeNull();
        state.DetectedFormat.Should().BeNull();
        state.FileName.Should().BeNull();
    }

    [Fact]
    public void Clear_TriggersOnChange()
    {
        var state = new SbomState();
        var fired = false;
        state.OnChange += () => fired = true;

        state.Clear();

        fired.Should().BeTrue();
    }

    [Fact]
    public void OnChange_NoSubscribers_DoesNotThrow()
    {
        var state = new SbomState();

        var act = () => state.Document = JsonDocument.Parse("{}");

        act.Should().NotThrow();
    }

    [Fact]
    public void MultipleSubscribers_BothNotified()
    {
        var state = new SbomState();
        var count = 0;
        state.OnChange += () => count++;
        state.OnChange += () => count++;

        state.Document = JsonDocument.Parse("{}");

        count.Should().Be(2);
    }

    [Fact]
    public void FileName_Set_DoesNotTriggerOnChange()
    {
        var state = new SbomState();
        var fired = false;
        state.OnChange += () => fired = true;

        state.FileName = "test.json";

        fired.Should().BeFalse();
    }
}
