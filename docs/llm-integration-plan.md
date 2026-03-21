# Plan: LLM Integration — SBOMViewer

## Context

Building on the vulnerability-integration release, this plan adds a browser-local LLM chat panel (via WebLLM + WebGPU) for conversational SBOM analysis. Users can ask questions about their uploaded SBOM and vulnerability scan results. All processing remains fully client-side — no backend required.

**Release:** `llm-integration` (Phases 3–5)
**Depends on:** `vulnerability-integration` (completed)

---

## New File Structure

```
src/SBOMViewer.Blazor/
├── Services/
│   └── WebLlmService.cs                # C# wrapper around WebLLM JS interop
├── Components/
│   ├── ChatPanel.razor                  # Collapsible right-side chat panel
│   └── ChatPanel.razor.css              # Scoped styles
└── wwwroot/js/
    └── webllm-interop.js               # JS module: WebLLM init, chat, progress callbacks
```

---

## Phase 3: Chat Panel UI

### 3.1 ChatPanel Component (`Components/ChatPanel.razor`)

- 400px fixed-width right panel with border-left separator
- Flex column: header (title + close button) → messages area (scrollable) → input area
- Messages styled as chat bubbles (user right-aligned, assistant left-aligned)
- Uses `FluentTextField` + send button for input
- **Model opt-in flow** (before any AI chat is available):
  1. Shows an info card explaining: "AI Chat is powered by WebLLM (Apache 2.0 license). The model (~924MB) will be downloaded to your browser and runs entirely on your device. No data leaves your browser."
  2. Links to WebLLM license and project page
  3. User must click "Download AI Model" to opt in — no automatic download
  4. `FluentProgress` bar shows download progress after opt-in
  5. Model is cached in browser IndexedDB — subsequent visits skip download
- WebGPU not supported → shows informational banner explaining browser requirements, chat input is disabled

### 3.2 Layout Integration (`MainLayout.razor`)

Modify `FluentBodyContent` to use flex row layout:
```razor
<FluentBodyContent Style="flex:1; overflow:hidden; display:flex; flex-direction:row;">
    <div style="flex:1; overflow:auto;">
        <!-- existing content -->
    </div>
    @if (ChatState.IsChatPanelOpen)
    {
        <ChatPanel />
    }
</FluentBodyContent>
```

Add "AI Chat" toggle button (chat bubble icon) to toolbar.

**Files to modify:** `MainLayout.razor`

---

## Phase 4: WebLLM Integration (Browser-Local LLM)

### 4.1 JS Interop Module (`wwwroot/js/webllm-interop.js`)

ES module that:
- Dynamically imports WebLLM from CDN (`@mlc-ai/web-llm`)
- Exports: `isWebGpuSupported()`, `initWebLlm(modelId, dotnetHelper)`, `chatCompletion(messagesJson)`, `dispose()`
- Reports model download progress back to C# via `[JSInvokable]` callback on DotNetObjectReference

### 4.2 WebLlmService (`Services/WebLlmService.cs`)

- Wraps `IJSRuntime` calls to the JS module
- Model: `SmolLM2-1.7B-Instruct-q4f16_1-MLC` (~924MB, persisted in IndexedDB by WebLLM)
- Best quality-to-size ratio for browser deployment — outperforms Llama 3.2 1B and Qwen 2.5-1.5B on instruction-following
- Implements `IAsyncDisposable` for cleanup
- Key methods: `IsWebGpuSupported()`, `InitializeAsync()`, `GetCompletionAsync(messages)`

### 4.3 SBOM Context for Chat

Before first user message, inject a system prompt with:
- SBOM format, filename, component count
- Top 20 component names+versions (condensed to fit small model's ~4K context)
- Vulnerability scan summary if available (counts + top 5 vulnerable packages)
- NOT the full JSON (too large for small model context)

### 4.4 index.html Change

Add before `</body>`:
```html
<script type="module" src="js/webllm-interop.js"></script>
```

### 4.5 DI Registration (`Program.cs`)

```csharp
builder.Services.AddScoped<WebLlmService>();
```

---

## Phase 5: Testing & Polish

### Unit Tests
- WebLlmService — mock IJSRuntime, test initialization flow, error handling

### E2E Tests (`tests/SBOMViewer.E2E.Tests/`)
- Chat panel toggle: upload SBOM → click chat button → verify panel appears
- WebGPU fallback: verify graceful degradation message in non-WebGPU environment

---

## Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| WebLLM (not Transformers.js) | OpenAI-compatible API, WebGPU acceleration, chat-optimized |
| SmolLM2 1.7B model | Best quality-to-size ratio (~924MB), outperforms 1B models on instruction-following |
| Side panel (not drawer/modal) | Users can view SBOM and chat simultaneously |
| User-initiated model load | ~924MB download must be opt-in with licensing disclosure and progress indication |
| Licensing transparency | Show WebLLM license (Apache 2.0) and data privacy info before user opts in to download |

---

## Verification

1. `dotnet build SBOMViewer.slnx` — compiles with no errors
2. `dotnet test` — all unit tests pass
3. Click "AI Chat" → verify panel opens → click "Load AI Model" (requires WebGPU browser) → send a question about the SBOM → verify response
4. E2E tests pass against local build

---

## Execution Plan (Step-by-Step)

### Step 1: ChatPanel UI Component
- [ ] Create `src/SBOMViewer.Blazor/Components/ChatPanel.razor`
  - 400px right panel, chat bubbles, model opt-in card with licensing info
  - WebGPU unsupported banner
- [ ] Create `src/SBOMViewer.Blazor/Components/ChatPanel.razor.css`

### Step 2: Integrate Chat Panel into Layout
- [ ] Modify `MainLayout.razor` — add chat toggle button, flex row layout with conditional ChatPanel
- [ ] `dotnet build` + manual test

### Step 3: WebLLM JS Interop
- [ ] Create `src/SBOMViewer.Blazor/wwwroot/js/webllm-interop.js`
  - Functions: isWebGpuSupported, initWebLlm, chatCompletion, dispose
- [ ] Modify `index.html` — add script tag

### Step 4: WebLlmService C# Wrapper
- [ ] Create `src/SBOMViewer.Blazor/Services/WebLlmService.cs`
  - IJSRuntime wrapper, model = SmolLM2-1.7B-Instruct-q4f16_1-MLC
  - Methods: IsWebGpuSupported, InitializeAsync, GetCompletionAsync, DisposeAsync
- [ ] Register in `Program.cs`

### Step 5: Wire Chat Completion Flow
- [ ] Implement send/receive in ChatPanel.razor
- [ ] Build SBOM context system prompt
- [ ] `dotnet build`

### Step 6: Testing & Polish
- [ ] Run all unit tests: `dotnet test`
- [ ] Add E2E tests for chat panel toggle, WebGPU fallback
- [ ] Manual end-to-end test with sample SBOM files
