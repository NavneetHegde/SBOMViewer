using System.Text.Json;
using Microsoft.JSInterop;
using SBOMViewer.Blazor.Models;

namespace SBOMViewer.Blazor.Services;

public class WebLlmService : IAsyncDisposable
{
    public const string ModelId = "SmolLM2-1.7B-Instruct-q4f16_1-MLC";
    public const string ModelSize = "~924 MB";
    public const string ModelLicense = "Apache 2.0";

    private readonly IJSRuntime _jsRuntime;
    private readonly ChatState _chatState;
    private readonly SbomState _sbomState;
    private IJSObjectReference? _module;
    private DotNetObjectReference<WebLlmService>? _dotnetRef;
    private bool _initialized;
    private TaskCompletionSource<string>? _streamCompletion;

    public WebLlmService(IJSRuntime jsRuntime, ChatState chatState, SbomState sbomState)
    {
        _jsRuntime = jsRuntime;
        _chatState = chatState;
        _sbomState = sbomState;
    }

    private async Task<IJSObjectReference> GetModuleAsync()
    {
        _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./js/webllm-interop.js");
        return _module;
    }

    public async Task<bool> IsWebGpuSupportedAsync()
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<bool>("isWebGpuSupported");
    }

    public async Task<bool> IsModelCachedAsync()
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<bool>("isModelCached", ModelId);
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        _chatState.IsLlmLoading = true;
        _chatState.LlmLoadProgress = 0;
        _chatState.LlmError = null;
        _chatState.NotifyStateChanged();

        try
        {
            var module = await GetModuleAsync();
            _dotnetRef = DotNetObjectReference.Create(this);
            await module.InvokeVoidAsync("initWebLlm", ModelId, _dotnetRef);

            _initialized = true;
            _chatState.IsLlmLoaded = true;
            _chatState.IsLlmLoading = false;
            _chatState.LlmLoadProgress = 1;
            _chatState.NotifyStateChanged();
        }
        catch (Exception ex)
        {
            _chatState.IsLlmLoading = false;
            _chatState.LlmError = $"Failed to load AI model: {ex.Message}";
            _chatState.NotifyStateChanged();
        }
    }

    [JSInvokable]
    public void OnModelLoadProgress(double progress)
    {
        _chatState.LlmLoadProgress = progress;
        _chatState.NotifyStateChanged();
    }

    [JSInvokable]
    public void OnStreamChunk(string chunk)
    {
        _chatState.StreamingContent += chunk;
        _chatState.NotifyStateChanged();
    }

    [JSInvokable]
    public void OnStreamComplete()
    {
        _streamCompletion?.TrySetResult(_chatState.StreamingContent ?? string.Empty);
    }

    [JSInvokable]
    public void OnStreamCancelled()
    {
        _streamCompletion?.TrySetCanceled();
    }

    [JSInvokable]
    public async Task OnEscapePressed()
    {
        await CancelStreamingAsync();
    }

    public async Task CancelStreamingAsync()
    {
        if (_streamCompletion is null) return;
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("abortStreaming");
    }

    public async Task StartEscapeListenerAsync()
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("startEscapeListener", _dotnetRef);
    }

    public async Task StopEscapeListenerAsync()
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("stopEscapeListener");
    }

    public async Task<string> GetCompletionAsync(List<ChatMessage> messages)
    {
        if (!_initialized)
            throw new InvalidOperationException("WebLLM engine not initialized.");

        var module = await GetModuleAsync();

        var systemPrompt = BuildSystemPrompt();

        var apiMessages = new List<object>();

        if (systemPrompt is not null)
        {
            apiMessages.Add(new { role = "system", content = systemPrompt });
        }

        foreach (var msg in messages)
        {
            apiMessages.Add(new { role = msg.Role, content = msg.Content });
        }

        var messagesJson = JsonSerializer.Serialize(apiMessages);

        _chatState.StreamingContent = string.Empty;
        _streamCompletion = new TaskCompletionSource<string>();

        await module.InvokeVoidAsync("chatCompletionStreaming", messagesJson, _dotnetRef);

        return await _streamCompletion.Task;
    }

    private string? BuildSystemPrompt()
    {
        var sbom = _sbomState;
        if (sbom.Document is null || sbom.DetectedFormat is null)
            return null;

        var root = sbom.Document.RootElement;
        var format = sbom.DetectedFormat.Value;
        var parts = new List<string>
        {
            "You are an AI assistant analyzing a Software Bill of Materials (SBOM).",
            $"File: {sbom.FileName ?? "unknown"}",
            $"Format: {format}"
        };

        // Component summary
        var packages = PackageExtractor.ExtractPackages(root, format);
        parts.Add($"Total components: {packages.Count}");

        if (packages.Count > 0)
        {
            var top20 = packages.Take(20)
                .Select(p => $"  - {p.Name} {p.Version}");
            parts.Add("Top components:");
            parts.AddRange(top20);

            if (packages.Count > 20)
                parts.Add($"  ... and {packages.Count - 20} more");
        }

        // Vulnerability summary if available
        var vulns = _chatState.VulnResults;
        if (vulns is { Count: > 0 })
        {
            var totalVulns = vulns.Sum(v => v.Vulnerabilities.Count);
            var affectedPackages = vulns.Count;
            parts.Add($"\nVulnerability scan: {totalVulns} vulnerabilities across {affectedPackages} packages.");

            var top5 = vulns
                .OrderByDescending(v => v.Vulnerabilities.Count)
                .Take(5)
                .Select(v => $"  - {v.PackageName} {v.PackageVersion}: {v.Vulnerabilities.Count} vuln(s)");
            parts.Add("Most affected:");
            parts.AddRange(top5);
        }

        parts.Add("\nAnswer questions about this SBOM concisely. If you don't know, say so.");

        return string.Join("\n", parts);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("dispose");
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Circuit already disconnected during app shutdown
            }
        }
        _dotnetRef?.Dispose();
    }
}
