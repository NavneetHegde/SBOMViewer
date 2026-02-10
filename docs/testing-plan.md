# Testing Plan — SBOMViewer v3.0

## Context

The v3.0 rewrite replaced format-specific parsers and models with a dynamic rendering pipeline. The testable services are now `SbomState`, `SbomFormatDetector`, and `SchemaService`.

## Framework

**xUnit + FluentAssertions v8.2.0**
Test data: inline JSON constants in `TestData/TestJson.cs`.

## Test Coverage — 47 tests

### `SbomStateTests` (8 tests)

| Test | What it verifies |
|------|-----------------|
| `SetSbom_TriggersOnChange` | Setting SBOM data fires event |
| `SetSbom_NullDocument_TriggersOnChange` | Setting to null still fires event |
| `SetSbom_PersistsDocument` | Getter returns what was set |
| `SetSbom_PersistsSchema` | SchemaNode getter returns what was set |
| `SetSbom_PersistsFormat` | Format getter returns what was set |
| `SetSbom_PersistsFileName` | FileName getter returns what was set |
| `Clear_DisposesDocument` | Clear disposes JsonDocument and nulls fields |
| `OnChange_MultipleSubscribers_AllNotified` | Multiple handlers all get called |

### `SbomFormatDetectorTests` (22 tests — detection + validation)

- Detection: CycloneDX 1.6, 1.7, SPDX 2.2, unknown format, missing fields
- Validation: required fields present/absent for each format

### `SchemaServiceTests` (17 tests)

- `BuildFromJson` creates correct `SchemaNode` tree from CycloneDX and SPDX JSON
- Node types inferred correctly (String, Integer, Array, Object, Boolean)
- Render hints applied (AccordionSection, SearchableList, BadgeList, KeyValueGroup)
- Property order preserved
- Array item schemas built correctly

## Running Tests

```bash
dotnet test                                                # Run all tests
dotnet test --filter "FullyQualifiedName~SchemaService"    # Run single test class
```
