using FluentAssertions;
using SBOMViewer.Blazor.Models.CycloneDX;
using SBOMViewer.Blazor.Models.Spdx;
using SBOMViewer.Blazor.Services;

namespace SBOMViewer.Blazor.Tests.Services;

public class SbomStateTests
{
    [Fact]
    public void CycloneDXData_Set_TriggersOnChange()
    {
        var state = new SbomState();
        var fired = false;
        state.OnChange += () => fired = true;

        state.CycloneDXData = new CycloneDXDocument();

        fired.Should().BeTrue();
    }

    [Fact]
    public void CycloneDXData_SetNull_TriggersOnChange()
    {
        var state = new SbomState();
        state.CycloneDXData = new CycloneDXDocument();

        var fired = false;
        state.OnChange += () => fired = true;

        state.CycloneDXData = null;

        fired.Should().BeTrue();
    }

    [Fact]
    public void SpdxData_Set_TriggersOnChange()
    {
        var state = new SbomState();
        var fired = false;
        state.OnChange += () => fired = true;

        state.SpdxData = new SpdxDocument();

        fired.Should().BeTrue();
    }

    [Fact]
    public void SpdxData_SetNull_TriggersOnChange()
    {
        var state = new SbomState();
        state.SpdxData = new SpdxDocument();

        var fired = false;
        state.OnChange += () => fired = true;

        state.SpdxData = null;

        fired.Should().BeTrue();
    }

    [Fact]
    public void CycloneDXFileName_Set_DoesNotTriggerOnChange()
    {
        var state = new SbomState();
        var fired = false;
        state.OnChange += () => fired = true;

        state.CycloneDXFileName = "test.json";

        fired.Should().BeFalse();
    }

    [Fact]
    public void SpdxFileName_Set_DoesNotTriggerOnChange()
    {
        var state = new SbomState();
        var fired = false;
        state.OnChange += () => fired = true;

        state.SpdxFileName = "test.json";

        fired.Should().BeFalse();
    }

    [Fact]
    public void OnChange_NoSubscribers_DoesNotThrow()
    {
        var state = new SbomState();

        var act = () => state.CycloneDXData = new CycloneDXDocument();

        act.Should().NotThrow();
    }

    [Fact]
    public void CycloneDXData_Set_ValuePersisted()
    {
        var state = new SbomState();
        var doc = new CycloneDXDocument { BomFormat = "CycloneDX" };

        state.CycloneDXData = doc;

        state.CycloneDXData.Should().BeSameAs(doc);
    }

    [Fact]
    public void SpdxData_Set_ValuePersisted()
    {
        var state = new SbomState();
        var doc = new SpdxDocument { Name = "Test" };

        state.SpdxData = doc;

        state.SpdxData.Should().BeSameAs(doc);
    }

    [Fact]
    public void MultipleSubscribers_BothNotified()
    {
        var state = new SbomState();
        var count = 0;
        state.OnChange += () => count++;
        state.OnChange += () => count++;

        state.SpdxData = new SpdxDocument();

        count.Should().Be(2);
    }
}
