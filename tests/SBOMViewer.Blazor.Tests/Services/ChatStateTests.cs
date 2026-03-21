using FluentAssertions;
using SBOMViewer.Blazor.Models;
using SBOMViewer.Blazor.Services;

namespace SBOMViewer.Blazor.Tests.Services;

public class ChatStateTests
{
    [Fact]
    public void AddMessage_AddsToList_And_TriggersOnChange()
    {
        var state = new ChatState();
        var fired = false;
        state.OnChange += () => fired = true;

        state.AddMessage(new ChatMessage("user", "Hello", DateTime.UtcNow));

        state.Messages.Should().HaveCount(1);
        state.Messages[0].Content.Should().Be("Hello");
        fired.Should().BeTrue();
    }

    [Fact]
    public void ClearChat_ResetsMessages_And_LlmState()
    {
        var state = new ChatState();
        state.AddMessage(new ChatMessage("user", "Hello", DateTime.UtcNow));
        state.IsLlmLoaded = true;
        state.IsLlmLoading = true;
        state.LlmLoadProgress = 50;
        state.LlmError = "some error";

        state.ClearChat();

        state.Messages.Should().BeEmpty();
        state.IsLlmLoaded.Should().BeFalse();
        state.IsLlmLoading.Should().BeFalse();
        state.LlmLoadProgress.Should().Be(0);
        state.LlmError.Should().BeNull();
    }

    [Fact]
    public void ClearVulnerabilities_ResetsVulnState()
    {
        var state = new ChatState();
        state.VulnResults = [new VulnerabilityResult("pkg", "1.0", [])];
        state.IsScanning = true;
        state.ScanProgress = 5;
        state.ScanTotal = 10;
        state.ScanError = "error";

        state.ClearVulnerabilities();

        state.VulnResults.Should().BeNull();
        state.IsScanning.Should().BeFalse();
        state.ScanProgress.Should().Be(0);
        state.ScanTotal.Should().Be(0);
        state.ScanError.Should().BeNull();
    }

    [Fact]
    public void Clear_ResetsEverything()
    {
        var state = new ChatState();
        state.AddMessage(new ChatMessage("user", "Hello", DateTime.UtcNow));
        state.IsLlmLoaded = true;
        state.IsChatPanelOpen = true;
        state.VulnResults = [new VulnerabilityResult("pkg", "1.0", [])];
        state.IsScanning = true;

        state.Clear();

        state.Messages.Should().BeEmpty();
        state.IsLlmLoaded.Should().BeFalse();
        state.IsChatPanelOpen.Should().BeFalse();
        state.VulnResults.Should().BeNull();
        state.IsScanning.Should().BeFalse();
    }

    [Fact]
    public void Clear_TriggersOnChange()
    {
        var state = new ChatState();
        var fired = false;
        state.OnChange += () => fired = true;

        state.Clear();

        fired.Should().BeTrue();
    }

    [Fact]
    public void ClearChat_TriggersOnChange()
    {
        var state = new ChatState();
        var fired = false;
        state.OnChange += () => fired = true;

        state.ClearChat();

        fired.Should().BeTrue();
    }

    [Fact]
    public void ClearVulnerabilities_TriggersOnChange()
    {
        var state = new ChatState();
        var fired = false;
        state.OnChange += () => fired = true;

        state.ClearVulnerabilities();

        fired.Should().BeTrue();
    }

    [Fact]
    public void OnChange_NoSubscribers_DoesNotThrow()
    {
        var state = new ChatState();

        var act = () => state.AddMessage(new ChatMessage("user", "test", DateTime.UtcNow));

        act.Should().NotThrow();
    }

    [Fact]
    public void MultipleSubscribers_AllNotified()
    {
        var state = new ChatState();
        var count = 0;
        state.OnChange += () => count++;
        state.OnChange += () => count++;

        state.AddMessage(new ChatMessage("user", "test", DateTime.UtcNow));

        count.Should().Be(2);
    }
}
