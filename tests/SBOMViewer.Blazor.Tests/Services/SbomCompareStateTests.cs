using System.Text.Json;
using FluentAssertions;
using SBOMViewer.Blazor.Models;
using SBOMViewer.Blazor.Services;

namespace SBOMViewer.Blazor.Tests.Services;

public class SbomCompareStateTests
{
    private static SbomLoadResult Load(string json, SbomFormat format, string fileName)
    {
        var doc = JsonDocument.Parse(json);
        return new SbomLoadResult(doc, new SchemaService().BuildFromJson(doc.RootElement), format, fileName, null);
    }

    private static SbomLoadResult CycloneDx(string fileName = "sbom.json")
        => Load(TestData.TestJson.ValidCycloneDXWithComponents, SbomFormat.CycloneDX_1_6, fileName);

    [Fact]
    public void Diff_IsNullUntilBothSlotsFilled()
    {
        var state = new SbomCompareState();

        state.Diff.Should().BeNull();

        state.SetBaseline(CycloneDx("old.json"));
        state.Diff.Should().BeNull();
        state.IsReady.Should().BeFalse();

        state.SetCurrent(CycloneDx("new.json"));
        state.Diff.Should().NotBeNull();
        state.IsReady.Should().BeTrue();
    }

    [Fact]
    public void SetBaseline_RecordsFileNameFormatAndComponentCount()
    {
        var state = new SbomCompareState();

        state.SetBaseline(CycloneDx("old.json"));

        state.BaselineFileName.Should().Be("old.json");
        state.BaselineFormat.Should().Be(SbomFormat.CycloneDX_1_6);
        state.BaselineComponentCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SetBaseline_IgnoresFailedLoad()
    {
        var state = new SbomCompareState();

        state.SetBaseline(SbomLoadResult.Failed("boom"));

        state.HasBaseline.Should().BeFalse();
        state.BaselineFileName.Should().BeNull();
    }

    [Fact]
    public void OnChange_FiresWhenSlotsChange()
    {
        var state = new SbomCompareState();
        var fired = 0;
        state.OnChange += () => fired++;

        state.SetBaseline(CycloneDx());
        state.SetCurrent(CycloneDx());
        state.ClearBaseline();
        state.Clear();

        fired.Should().Be(4);
    }

    [Fact]
    public void ClearBaseline_DropsDiffButKeepsCurrent()
    {
        var state = new SbomCompareState();
        state.SetBaseline(CycloneDx("old.json"));
        state.SetCurrent(CycloneDx("new.json"));

        state.ClearBaseline();

        state.Diff.Should().BeNull();
        state.HasBaseline.Should().BeFalse();
        state.HasCurrent.Should().BeTrue();
        state.CurrentFileName.Should().Be("new.json");
    }

    [Fact]
    public void Swap_ExchangesTheTwoSlots()
    {
        var state = new SbomCompareState();
        state.SetBaseline(CycloneDx("old.json"));
        state.SetCurrent(CycloneDx("new.json"));

        state.Swap();

        state.BaselineFileName.Should().Be("new.json");
        state.CurrentFileName.Should().Be("old.json");
    }

    [Fact]
    public void Swap_MirrorsTheDiff()
    {
        var state = new SbomCompareState();
        // Minimal has no components, full has several — so the diff is entirely one-directional.
        state.SetBaseline(Load(TestData.TestJson.ValidCycloneDXMinimal, SbomFormat.CycloneDX_1_6, "old.json"));
        state.SetCurrent(CycloneDx("new.json"));

        var addedBefore   = state.Diff!.Added.Count;
        var removedBefore = state.Diff!.Removed.Count;
        addedBefore.Should().BeGreaterThan(0);

        state.Swap();

        state.Diff!.Added.Count.Should().Be(removedBefore);
        state.Diff!.Removed.Count.Should().Be(addedBefore);
    }

    [Fact]
    public void Swap_WithOnlyOneSlotFilled_MovesItToTheOtherSide()
    {
        var state = new SbomCompareState();
        state.SetBaseline(CycloneDx("only.json"));

        state.Swap();

        state.HasBaseline.Should().BeFalse();
        state.CurrentFileName.Should().Be("only.json");
        state.Diff.Should().BeNull();
    }

    [Fact]
    public void Swap_WithNothingLoaded_IsANoOpAndDoesNotNotify()
    {
        var state = new SbomCompareState();
        var fired = 0;
        state.OnChange += () => fired++;

        state.Swap();

        fired.Should().Be(0);
        state.Diff.Should().BeNull();
    }

    [Fact]
    public void Swap_Twice_RestoresTheOriginalOrder()
    {
        var state = new SbomCompareState();
        state.SetBaseline(CycloneDx("old.json"));
        state.SetCurrent(CycloneDx("new.json"));

        state.Swap();
        state.Swap();

        state.BaselineFileName.Should().Be("old.json");
        state.CurrentFileName.Should().Be("new.json");
    }

    [Fact]
    public void Clear_ResetsEverything()
    {
        var state = new SbomCompareState();
        state.SetBaseline(CycloneDx());
        state.SetCurrent(CycloneDx());

        state.Clear();

        state.HasBaseline.Should().BeFalse();
        state.HasCurrent.Should().BeFalse();
        state.Diff.Should().BeNull();
        state.BaselineComponentCount.Should().Be(0);
    }

    [Fact]
    public void ReplacingASlot_RecomputesTheDiff()
    {
        var state = new SbomCompareState();
        state.SetBaseline(CycloneDx("old.json"));
        state.SetCurrent(CycloneDx("new.json"));
        state.Diff!.IsIdentical.Should().BeTrue();

        // Replace current with a document that has no components at all.
        state.SetCurrent(Load(TestData.TestJson.ValidCycloneDXMinimal, SbomFormat.CycloneDX_1_6, "empty.json"));

        state.CurrentFileName.Should().Be("empty.json");
        state.Diff!.Removed.Should().NotBeEmpty();
    }
}
