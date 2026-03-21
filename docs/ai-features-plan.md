# Plan: Vulnerability Integration — SBOMViewer

## Context

SBOMViewer currently parses and displays SBOM files but offers no security analysis. This plan adds automated CVE/vulnerability scanning via the free OSV.dev API. All processing remains fully client-side — no backend required.

**Release:** `vulnerability-integration` (Phases 1–2)

---

## File Structure

```
src/SBOMViewer.Blazor/
├── Models/
│   ├── PackageInfo.cs                   # Package name, version, ecosystem, purl
│   ├── VulnerabilityResult.cs           # CVE entries per package
│   └── ChatMessage.cs                   # Chat message (role, content, timestamp)
├── Services/
│   ├── PackageExtractor.cs              # Extracts packages from SBOM JSON (CycloneDX + SPDX)
│   ├── VulnerabilityService.cs          # OSV.dev API client with batch querying
│   └── ChatState.cs                     # Singleton state for vuln results + future chat
├── Components/
│   ├── VulnerabilitySummary.razor       # Summary with severity breakdown
│   └── VulnerabilityBadge.razor         # Inline severity badge
```

---

## Phase 1: Foundation Models & Package Extraction

### 1.1 New Models

**`Models/PackageInfo.cs`**
```csharp
public record PackageInfo(string Name, string Version, string? Ecosystem, string? Purl);
```

**`Models/VulnerabilityResult.cs`**
```csharp
public record VulnerabilityResult(string PackageName, string PackageVersion, List<VulnerabilityEntry> Vulnerabilities);
public record VulnerabilityEntry(string Id, string? Summary, string? Severity, double? CvssScore, string? FixedVersion);
```

**`Models/ChatMessage.cs`**
```csharp
public record ChatMessage(string Role, string Content, DateTime Timestamp);
```

### 1.2 PackageExtractor Service (`Services/PackageExtractor.cs`)

Static service that extracts `List<PackageInfo>` from a `JsonElement` root based on `SbomFormat`:

- **CycloneDX**: Iterate `components` array → extract `name`, `version`, `purl` → parse ecosystem from purl (`pkg:<ecosystem>/...`)
- **SPDX**: Iterate `packages` array → extract `name`, `versionInfo` → check `externalRefs` for purl with `referenceType: "purl"`

### 1.3 ChatState Service (`Services/ChatState.cs`)

Singleton following the same `OnChange` event pattern as `SbomState`:
- Vulnerability results, scan progress/status
- Chat messages list, LLM loading state/progress, panel open/closed toggle (prepared for future LLM integration)

### 1.4 DI Registration (`Program.cs`)

```csharp
builder.Services.AddScoped(sp => new HttpClient());
builder.Services.AddSingleton<ChatState>();
builder.Services.AddScoped<VulnerabilityService>();
```

---

## Phase 2: Vulnerability Scanning (OSV.dev)

### 2.1 VulnerabilityService (`Services/VulnerabilityService.cs`)

- Uses `HttpClient` to POST to `https://api.osv.dev/v1/querybatch`
- Batches queries in groups of 100 packages
- Request format: `{ "queries": [{ "package": { "name": "...", "ecosystem": "..." }, "version": "..." }] }`
- Parses response into `List<VulnerabilityResult>`
- Reports progress via `Action<int, int>` callback
- Handles errors gracefully (network failures → error message, not crash)

### 2.2 VulnerabilitySummary Component (`Components/VulnerabilitySummary.razor`)

- Rendered inside DynamicSbomViewer as a collapsible FluentAccordionItem
- Shows severity breakdown with colored `FluentBadge` (Critical=red, High=orange, Medium=yellow, Low=blue)
- Searchable/expandable list of affected packages with CVE details
- Each CVE links to `https://osv.dev/vulnerability/{id}`

### 2.3 Scan Button Integration

Scan button placed inside the Vulnerabilities accordion heading in `DynamicSbomViewer.razor`:
- Red ShieldError icon with "Vulnerabilities" label
- Accent-colored Scan button with loading spinner during scan
- Full-page overlay with progress bar while scanning
- User must click to initiate scan (not automatic)

---

## Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| OSV.dev (not NVD/Snyk) | Free, no auth, CORS-enabled, SBOM-native, batch API |
| User-initiated scan (not auto) | Large SBOMs could trigger hundreds of API calls |
| Accordion integration | Vulnerabilities section fits naturally alongside General Info and component sections |
| Full-page overlay during scan | Clear feedback that a batch API operation is in progress |

---

## Verification

1. `dotnet build SBOMViewer.slnx` — compiles with no errors
2. `dotnet test` — all unit tests pass (existing + new)
3. Run locally → upload a sample SBOM → click "Scan" → verify CVE results appear

---

## Execution Plan — COMPLETE

### Step 1: Create Data Models ✅
- [x] `Models/PackageInfo.cs`
- [x] `Models/VulnerabilityResult.cs`
- [x] `Models/ChatMessage.cs`

### Step 2: PackageExtractor Service ✅
- [x] `Services/PackageExtractor.cs` — CycloneDX + SPDX extraction with purl parsing
- [x] `tests/Services/PackageExtractorTests.cs` — 14 tests

### Step 3: ChatState Service ✅
- [x] `Services/ChatState.cs` — OnChange event pattern, vuln + chat state
- [x] `tests/Services/ChatStateTests.cs` — 9 tests

### Step 4: VulnerabilityService ✅
- [x] DI registration in `Program.cs`
- [x] `Services/VulnerabilityService.cs` — OSV.dev batch API client
- [x] `tests/Services/VulnerabilityServiceTests.cs` — 12 tests

### Step 5: VulnerabilitySummary UI ✅
- [x] `Components/VulnerabilitySummary.razor` — severity badges, searchable package list
- [x] `Components/VulnerabilityBadge.razor` — colored severity badge

### Step 6: Scan Integration ✅
- [x] Scan button + accordion in `DynamicSbomViewer.razor`
- [x] Full-page scanning overlay with progress
- [x] `Home.razor` wired to ChatState.OnChange
