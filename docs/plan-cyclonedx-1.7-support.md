# Plan: Add CycloneDX 1.7 Support with Auto-Detect Format Selection

## Context

The SBOM Viewer currently supports CycloneDX 1.6 and SPDX 2.2 with a manual format selector dropdown. The user wants to:
1. Add CycloneDX 1.7 support with practical (not exhaustive) properties
2. Remove the format selector — auto-detect the format from the uploaded JSON content
3. Update the UI to display 1.7-specific sections when present

CycloneDX 1.7 is fully backward-compatible with 1.6 (additive only), so the same model classes can handle both versions with nullable properties for 1.7 additions.

---

## Step 1: Auto-Detect Format — New `SbomFormatDetector` Service

**New file:** `SBOMViewer.Blazor/Services/SbomFormatDetector.cs`

Create a static class that peeks into the JSON to determine the format:
- Check for `"bomFormat"` property → CycloneDX (read `specVersion` for 1.6 vs 1.7)
- Check for `"spdxVersion"` property → SPDX
- Return a result enum/value indicating detected format, or `null` if unrecognized

Uses `System.Text.Json.JsonDocument` for lightweight parsing without full deserialization.

**Modify:** `SBOMViewer.Blazor/Models/SbomFormat.cs`
- Add `CycloneDX_1_7` enum value
- Keep existing values for internal state tracking (the enum is no longer user-facing)

---

## Step 2: Update `UploadFile.razor` — Remove Format Selector

**Modify:** `SBOMViewer.Blazor/Components/UploadFile.razor`

- Remove the `FluentSelect` dropdown for format selection
- Remove `SelectedSbomFormat` / `SelectedSbomFormatString` properties
- In `OnCompleted`, after reading file content:
  1. Call `SbomFormatDetector.Detect(content)` to determine format
  2. Route to the appropriate parser based on detected format
  3. Both `CycloneDX_1_6` and `CycloneDX_1_7` use the same `CycloneDXParser`
  4. Show an error message if format is unrecognized
- Simplify UI to just the file upload button (no selector needed)

---

## Step 3: Extend CycloneDX Models for 1.7

**Modify:** `SBOMViewer.Blazor/Models/CycloneDXDocument.cs`

### New nullable properties on `CycloneDXDocument`:
```csharp
[JsonPropertyName("formulation")]
public List<Formula>? Formulation { get; set; }

[JsonPropertyName("declarations")]
public Declarations? Declarations { get; set; }

[JsonPropertyName("definitions")]
public Definitions? Definitions { get; set; }
```

### New nullable properties on `Metadata`:
```csharp
[JsonPropertyName("lifecycles")]
public List<Lifecycle>? Lifecycles { get; set; }
```

### New nullable properties on `Component`:
```csharp
[JsonPropertyName("omniborId")]
public List<string>? OmniborId { get; set; }

[JsonPropertyName("swhid")]
public List<string>? Swhid { get; set; }

[JsonPropertyName("tags")]
public List<string>? Tags { get; set; }
```

### New model classes to add (in same file):

```csharp
// Lifecycle phase
public class Lifecycle
{
    [JsonPropertyName("phase")]
    public string? Phase { get; set; }          // design, pre-build, build, post-build, operations, discovery, decommission

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

// Formulation (simplified)
public class Formula
{
    [JsonPropertyName("bom-ref")]
    public string? BomRef { get; set; }

    [JsonPropertyName("components")]
    public List<Component>? Components { get; set; }
}

// Declarations (simplified — claims + assessors)
public class Declarations
{
    [JsonPropertyName("assessors")]
    public List<Assessor>? Assessors { get; set; }

    [JsonPropertyName("claims")]
    public List<Claim>? Claims { get; set; }
}

public class Assessor
{
    [JsonPropertyName("bom-ref")]
    public string? BomRef { get; set; }

    [JsonPropertyName("organization")]
    public OrganizationalEntity? Organization { get; set; }
}

public class OrganizationalEntity
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public class Claim
{
    [JsonPropertyName("bom-ref")]
    public string? BomRef { get; set; }

    [JsonPropertyName("target")]
    public string? Target { get; set; }

    [JsonPropertyName("predicate")]
    public string? Predicate { get; set; }
}

// Definitions (simplified — standards)
public class Definitions
{
    [JsonPropertyName("standards")]
    public List<Standard>? Standards { get; set; }
}

public class Standard
{
    [JsonPropertyName("bom-ref")]
    public string? BomRef { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}
```

---

## Step 4: Update CycloneDX Parser

**Modify:** `SBOMViewer.Blazor/Services/CycloneDXParser.cs`

- No structural changes needed — `System.Text.Json` will automatically deserialize the new nullable properties when present in 1.7 JSON, and leave them null for 1.6 JSON
- The existing validation (BomFormat not null, Metadata not null) still applies

---

## Step 5: Update CycloneDX Viewer UI

**Modify:** `SBOMViewer.Blazor/Components/CycloneDXViewer.razor`

### Metadata section enhancements:
- Add lifecycle display: if `Document.Metadata.Lifecycles` has items, show them as badges/chips

### Component section enhancements:
- In each component's `<details>`, conditionally show:
  - **Tags** — as inline badges if `component.Tags` has items
  - **OmniBOR ID** — if `component.OmniborId` has items
  - **SWHID** — if `component.Swhid` has items

### New accordion sections (shown only when data is present):

**Definitions section** (after Dependencies):
- Show standards list with name, version, description
- Only render this section if `Document.Definitions?.Standards` has items

**Declarations section** (after Definitions):
- Show claims and assessors
- Only render this section if `Document.Declarations` has data

**Formulation section** (after Declarations):
- Show formula references and their component lists
- Only render this section if `Document.Formulation` has items

---

## Step 6: Update Tests

### Modify: `SBOMViewer.Blazor.Tests/TestData/TestJson.cs`
- Add `ValidCycloneDX17Minimal` — specVersion "1.7", includes lifecycles
- Add `ValidCycloneDX17WithNewFeatures` — includes tags, omniborId, swhid, definitions, declarations

### New file: `SBOMViewer.Blazor.Tests/Services/SbomFormatDetectorTests.cs`
- Test auto-detection of CycloneDX 1.6, CycloneDX 1.7, SPDX 2.2
- Test unrecognized format returns null
- Test malformed JSON returns null

### Modify: `SBOMViewer.Blazor.Tests/Services/CycloneDXParserTests.cs`
- Add test: `Valid17Minimal_ReturnsDocument` — parses 1.7 minimal JSON
- Add test: `Valid17WithLifecycles_ParsesLifecycles` — verifies lifecycle parsing
- Add test: `Valid17WithComponentTags_ParsesTags` — verifies tag/omniborId/swhid parsing
- Add test: `Valid17WithDefinitions_ParsesStandards` — verifies definitions parsing
- Add test: `Valid16Json_LeavesNew17PropertiesNull` — backward compat test

---

## Step 7: Update Footer/Version

**Modify:** `SBOMViewer.Blazor/Layout/MainLayout.razor`
- Update footer version text from v2.0.1 to v3.0 (matches the release/3.0 branch)

---

## Files Summary

| File | Action |
|------|--------|
| `SBOMViewer.Blazor/Models/SbomFormat.cs` | Add `CycloneDX_1_7` enum value |
| `SBOMViewer.Blazor/Models/CycloneDXDocument.cs` | Add 1.7 properties + new model classes |
| `SBOMViewer.Blazor/Services/SbomFormatDetector.cs` | **New** — auto-detect format from JSON |
| `SBOMViewer.Blazor/Services/CycloneDXParser.cs` | No changes needed (auto-handles new properties) |
| `SBOMViewer.Blazor/Components/UploadFile.razor` | Remove selector, use auto-detect |
| `SBOMViewer.Blazor/Components/CycloneDXViewer.razor` | Add 1.7 sections (lifecycles, tags, definitions, declarations, formulation) |
| `SBOMViewer.Blazor/Pages/Home.razor` | No changes needed |
| `SBOMViewer.Blazor/Services/SbomState.cs` | No changes needed |
| `SBOMViewer.Blazor.Tests/TestData/TestJson.cs` | Add 1.7 test JSON constants |
| `SBOMViewer.Blazor.Tests/Services/SbomFormatDetectorTests.cs` | **New** — detector unit tests |
| `SBOMViewer.Blazor.Tests/Services/CycloneDXParserTests.cs` | Add 1.7 parser tests |
| `SBOMViewer.Blazor/Layout/MainLayout.razor` | Update footer version |

---

## Verification

1. `dotnet build` — ensure solution compiles
2. `dotnet test` — all existing + new tests pass
3. Manual test with a CycloneDX 1.6 JSON file — should auto-detect and display correctly (no 1.7 sections shown)
4. Manual test with a CycloneDX 1.7 JSON file — should auto-detect and display 1.7 sections
5. Manual test with an SPDX 2.2 JSON file — should auto-detect and display correctly
6. Manual test uploading an invalid/non-SBOM JSON file — should show error gracefully
