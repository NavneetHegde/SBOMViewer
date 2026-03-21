using SBOMViewer.Blazor.Models;

namespace SBOMViewer.Blazor.Services;

public class ChatState
{
    public event Action? OnChange;

    // Chat
    public List<ChatMessage> Messages { get; } = [];
    public bool IsLlmLoaded { get; set; }
    public bool IsLlmLoading { get; set; }
    public double LlmLoadProgress { get; set; }
    public string? LlmError { get; set; }
    public bool IsChatPanelOpen { get; set; }

    // Vulnerabilities
    public List<VulnerabilityResult>? VulnResults { get; set; }
    public bool IsScanning { get; set; }
    public int ScanProgress { get; set; }
    public int ScanTotal { get; set; }
    public string? ScanError { get; set; }

    public void AddMessage(ChatMessage message)
    {
        Messages.Add(message);
        NotifyStateChanged();
    }

    public void ClearChat()
    {
        Messages.Clear();
        IsLlmLoaded = false;
        IsLlmLoading = false;
        LlmLoadProgress = 0;
        LlmError = null;
        NotifyStateChanged();
    }

    public void ClearVulnerabilities()
    {
        VulnResults = null;
        IsScanning = false;
        ScanProgress = 0;
        ScanTotal = 0;
        ScanError = null;
        NotifyStateChanged();
    }

    public void Clear()
    {
        Messages.Clear();
        IsLlmLoaded = false;
        IsLlmLoading = false;
        LlmLoadProgress = 0;
        LlmError = null;
        IsChatPanelOpen = false;
        VulnResults = null;
        IsScanning = false;
        ScanProgress = 0;
        ScanTotal = 0;
        ScanError = null;
        NotifyStateChanged();
    }

    public void NotifyStateChanged() => OnChange?.Invoke();
}
