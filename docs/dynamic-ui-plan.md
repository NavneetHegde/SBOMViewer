# Dynamic Schema-Driven UI for SBOMViewer

## Context

Previously, the SBOMViewer used **static C# model classes** (`CycloneDXDocument`, `SpdxDocument`) and **hardcoded viewer components** (`CycloneDXViewer.razor`, `SpdxViewer.razor`) that rendered specific properties. Every new SBOM property required model changes AND viewer changes.

This was replaced with a **dynamic, data-driven approach** where:
- Uploaded JSON is parsed as a `JsonDocument` tree (no typed deserialization)
- A `SchemaNode` tree is built directly from the JSON content to infer types and apply render hints
- UI sections are generated dynamically based on what's actually present in the uploaded file
- Lightweight validation checks required fields before rendering
- The Fluent UI visual style (accordions, cards, details/summary, search, badges) is preserved

## Architecture

### Three-Layer Design

1. **Validation Layer** — `SbomFormatDetector.Validate()` checks required fields per format (fast, no schema file loading)
2. **Schema Layer** — `SchemaService.BuildFromJson()` infers types directly from the uploaded JSON and applies render hints
3. **Rendering Layer** — Three recursive Blazor components (`DynamicSbomViewer`, `DynamicSection`, `DynamicObject`) walk `JsonElement` + `SchemaNode` together

### Upload Flow
```
read file
→ SbomFormatDetector.DetectWithDetails(content)    // format + version detection
→ JsonDocument.Parse(content)                       // syntax validation
→ SbomFormatDetector.Validate(root, format)         // required fields validation
→ SchemaService.BuildFromJson(root)                 // build SchemaNode tree from JSON
→ set SbomState.Schema, DetectedFormat, FileName, Document
→ DynamicSbomViewer renders from JsonElement + SchemaNode
```

## Lightweight Validation

`SbomFormatDetector.Validate(JsonElement root, SbomFormat format)` checks required fields per format. Returns `null` if valid, or an error message listing missing fields.

**CycloneDX required fields:** `bomFormat`, `specVersion`, `metadata` (object), `components` (array)

**SPDX required fields:** `spdxVersion`, `name`, `SPDXID`, `dataLicense`, `documentNamespace`, `creationInfo` (object)

## Key Files

### Created
| File | Purpose |
|---|---|
| `Models/SchemaNode.cs` | Schema tree model: `SchemaNodeType` enum, `RenderHint` enum, `SchemaNode` class with `PropertyName`, `Title`, `NodeType`, `Properties`, `ItemSchema`, `PropertyOrder` |
| `Services/SchemaService.cs` | `BuildFromJson(JsonElement)` — infers `SchemaNode` tree from JSON data, applies render hints for well-known properties |
| `Components/DynamicSbomViewer.razor` | Top-level viewer — FluentCard + FluentAccordion, groups scalars into "General Information", creates accordion sections per complex property, icon mapping for known sections |
| `Components/DynamicSection.razor` | Array/object renderer — FluentSearch for filtering (>5 items), scrollable container, `<details>/<summary>` per item with indented content + left border |
| `Components/DynamicObject.razor` | Recursive object renderer — key-value pairs for scalars, FluentBadge for tag-like arrays, indented nested objects with left border, delegates to DynamicSection for object arrays |

### Modified
| File | Change |
|---|---|
| `Services/SbomState.cs` | Holds `JsonDocument? Document`, `SchemaNode? Schema`, `SbomFormat? DetectedFormat`, `string? FileName`. `Clear()` disposes `JsonDocument` |
| `Services/SbomFormatDetector.cs` | Added `Validate(JsonElement, SbomFormat)` for lightweight required-field checks |
| `Components/UploadFile.razor` | Uses `JsonDocument.Parse()` + validation + `SchemaService.BuildFromJson()` instead of typed parsers |
| `Pages/Home.razor` | Renders single `<DynamicSbomViewer>` when document is loaded |
| `Program.cs` | Registers `SchemaService` as singleton |
| `_Imports.razor` | Added `@using System.Text.Json`, removed old model namespaces |

### Deleted
| File | Reason |
|---|---|
| `Components/CycloneDXViewer.razor` | Replaced by `DynamicSbomViewer` |
| `Components/SpdxViewer.razor` | Replaced by `DynamicSbomViewer` |
| `Services/CycloneDXParser.cs` | Replaced by `JsonDocument.Parse()` |
| `Services/SpdxParser.cs` | Replaced by `JsonDocument.Parse()` |
| `Models/CycloneDXDocument.cs` | Replaced by `JsonElement` |
| `Models/SpdxDocument.cs` | Replaced by `JsonElement` |
| `NJsonSchema` package | Removed — schema inferred from JSON data instead |

## Component Details

### DynamicSbomViewer
- Groups top-level scalar properties (string, number, boolean) into a single **General Information** accordion (expanded)
- Creates individual accordion sections for each complex property (object/array) present in the JSON
- Unknown scalar properties in the JSON are included in General Information
- Unknown complex properties get a list icon and humanized title
- Icon mapping for well-known sections: metadata→Tab, components→Layer, packages→Box, dependencies→Link, vulnerabilities→ShieldError, files→Document, formulation→Beaker, declarations→Shield, etc.

### DynamicSection
- Arrays with >5 items or `SearchableList` hint get a `FluentSearch` input
- Search filters via `GetRawText().Contains()` with `OrdinalIgnoreCase`
- Each object item rendered as `<details>/<summary>` with smart summary extraction (looks for `name`, `ref`, `fileName`, `SPDXID`, `version`, `type`, `scope` fields)
- Expanded content indented with `padding-left: 1.2rem` and `border-left: 2px solid var(--neutral-stroke-rest)`

### DynamicObject
- Scalar properties → `<strong>Label:</strong> Value`
- String arrays ≤10 items + badge candidates (tags, creators, dependsOn, omniborId, swhid, etc.) → FluentBadge row
- String arrays >10 items → indented `<ul>/<li>` list
- Object arrays → delegates to `DynamicSection` with `padding-left: 1rem`
- Nested objects → recursive `DynamicObject` with left border indent
- Null values are skipped
- Labels come from `SchemaNode.Title` or `HumanizePropertyName()` (camelCase → Title Case)

### SchemaService.BuildFromJson
- Walks top-level JSON properties, infers `SchemaNodeType` from `JsonValueKind`
- Only recurses one level deep (top-level properties) — deeper rendering handled by components
- Peeks at first array item to build `ItemSchema` for array properties
- Applies `RenderHint` for well-known property names:
  - `SearchableList`: components, packages, files, vulnerabilities, services
  - `AccordionSection`: dependencies, relationships
  - `KeyValueGroup`: all scalar properties

## Testing

| Test File | Coverage |
|---|---|
| `SbomFormatDetectorTests.cs` | Format detection (14 tests) + validation: valid CycloneDX/SPDX, missing fields (bomFormat, metadata, components, name, SPDXID, creationInfo), null creationInfo, multiple missing fields |
| `SchemaServiceTests.cs` | BuildFromJson: root structure, scalar/object/array detection, item schema, property ordering, render hints (KeyValue, SearchableList, Accordion), title humanization, SPDX structure, CycloneDX 1.7 sections, edge cases (empty object, boolean, empty array) |
| `SbomStateTests.cs` | OnChange events, Clear(), value persistence, multiple subscribers |

## Performance

- No external schema file loading — `BuildFromJson` works directly on the already-parsed `JsonDocument`
- No NJsonSchema dependency — eliminated WASM bundle overhead
- `JsonDocument` is read-only and memory-efficient (pooled `Utf8JsonReader`)
- Schema tree built in microseconds (only walks top-level + one level deep)
- Search filtering is O(n) per keystroke via `GetRawText().Contains()`, adequate for typical SBOMs
