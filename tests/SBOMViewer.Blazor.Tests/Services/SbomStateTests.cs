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

    private static SbomLoadResult LoadResult(string fileName = "test.json") => new(
        JsonDocument.Parse("{}"),
        new SchemaNode { PropertyName = "root" },
        SbomFormat.CycloneDX_1_6,
        fileName,
        null);

    [Fact]
    public void Load_AssignsEveryFieldBeforeNotifying()
    {
        // Subscribers re-render on the notification, so everything — the sample flag included —
        // has to be in place by the time it fires.
        var state = new SbomState();
        var result = LoadResult("demo.json");
        (string? FileName, bool IsSample, bool HasSchema)? seen = null;
        var notifications = 0;
        state.OnChange += () =>
        {
            notifications++;
            seen = (state.FileName, state.IsSample, state.Schema is not null);
        };

        state.Load(result, isSample: true);

        notifications.Should().Be(1);
        seen.Should().Be(("demo.json", true, true));
        state.Document.Should().BeSameAs(result.Document);
        state.DetectedFormat.Should().Be(SbomFormat.CycloneDX_1_6);
    }

    [Fact]
    public void Load_ARealFileAfterTheSample_DropsTheSampleFlag()
    {
        var state = new SbomState();
        state.Load(LoadResult("demo.json"), isSample: true);

        state.Load(LoadResult("mine.json"));

        state.IsSample.Should().BeFalse();
        state.FileName.Should().Be("mine.json");
    }

    private static ChatState ChatWithScanResults()
    {
        var chat = new ChatState();
        chat.VulnResults = [new VulnerabilityResult("old-package", "1.0.0", [])];
        return chat;
    }

    [Fact]
    public void Load_ClearsThePreviousDocumentsScanResults()
    {
        // No caller has to remember to do this: results that describe the old document must not
        // be shown against the new one.
        var chat = ChatWithScanResults();
        var state = new SbomState(chat);

        state.Load(LoadResult());

        chat.VulnResults.Should().BeNull();
    }

    [Fact]
    public void Load_AFailedResult_LeavesScanResultsAlone()
    {
        var chat = ChatWithScanResults();
        var state = new SbomState(chat);

        state.Load(SbomLoadResult.Failed("boom"));

        chat.VulnResults.Should().NotBeNull();
    }

    [Fact]
    public void Clear_ClearsScanResults()
    {
        var chat = ChatWithScanResults();
        var state = new SbomState(chat);
        state.Load(LoadResult());
        chat.VulnResults = [new VulnerabilityResult("old-package", "1.0.0", [])];

        state.Clear();

        chat.VulnResults.Should().BeNull();
    }

    [Fact]
    public void Load_IgnoresAFailedResult()
    {
        var state = new SbomState();
        state.Load(LoadResult("kept.json"), isSample: true);

        state.Load(SbomLoadResult.Failed("boom"));

        state.FileName.Should().Be("kept.json");
        state.IsSample.Should().BeTrue();
        state.Document.Should().NotBeNull();
    }

    [Fact]
    public void Clear_ResetsAllProperties()
    {
        var state = new SbomState();
        state.Load(LoadResult(), isSample: true);

        state.Clear();

        state.IsSample.Should().BeFalse();
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
