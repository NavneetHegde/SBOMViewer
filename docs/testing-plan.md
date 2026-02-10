# Plan: Add Unit Tests to SBOMViewer

## Context

The SBOMViewer project has zero tests. The parsers (`SpdxParser`, `CycloneDXParser`) and state service (`SbomState`) contain logic worth covering — null handling, JSON deserialization edge cases, and event notifications. Adding tests now establishes a safety net before future changes.

## Approach

**Framework:** xUnit + FluentAssertions (standard for .NET 10 projects, lightweight).
**Test data:** Inline JSON constants in a `TestJson.cs` helper (project is small, no need for resource files).
**Scope:** 30 unit tests across 3 test classes covering the 3 testable units.

## Steps

### 1. Scaffold test project

```bash
dotnet new xunit -n SBOMViewer.Blazor.Tests -f net9.0 -o SBOMViewer.Blazor.Tests
dotnet sln SBOMViewer.sln add SBOMViewer.Blazor.Tests/SBOMViewer.Blazor.Tests.csproj
dotnet add SBOMViewer.Blazor.Tests reference SBOMViewer.Blazor/SBOMViewer.Blazor.csproj
dotnet add SBOMViewer.Blazor.Tests package FluentAssertions
```

Delete auto-generated `UnitTest1.cs`.

### 2. Create test data — `SBOMViewer.Blazor.Tests/TestData/TestJson.cs`

Static class with `const string` fields:
- `ValidSpdxMinimal` — minimal valid SPDX with name + creationInfo
- `ValidSpdxWithPackages` — SPDX with packages and relationships
- `SpdxMissingName` — omits name field (defaults to `""`, caught by `IsNullOrWhiteSpace`)
- `SpdxNullCreationInfo` — explicitly `"creationInfo": null` (important: omitting the field won't trigger null guard because `CreationInfo` defaults to `new()`)
- `ValidCycloneDXMinimal` — minimal valid CycloneDX with bomFormat + metadata
- `ValidCycloneDXWithComponents` — CycloneDX with components, dependencies, licenses
- `CycloneDXMissingBomFormat` — omits bomFormat (defaults to null)
- `CycloneDXMissingMetadata` — omits metadata (defaults to null)

### 3. Create `SBOMViewer.Blazor.Tests/Services/SbomStateTests.cs` (10 tests)

| Test | What it verifies |
|------|-----------------|
| `CycloneDXData_Set_TriggersOnChange` | Setting CycloneDXData fires event |
| `CycloneDXData_SetNull_TriggersOnChange` | Setting to null still fires event |
| `SpdxData_Set_TriggersOnChange` | Setting SpdxData fires event |
| `SpdxData_SetNull_TriggersOnChange` | Setting to null still fires event |
| `CycloneDXFileName_Set_DoesNotTriggerOnChange` | Filename setters don't fire event |
| `SpdxFileName_Set_DoesNotTriggerOnChange` | Filename setters don't fire event |
| `OnChange_NoSubscribers_DoesNotThrow` | Null-conditional `?.Invoke()` works |
| `CycloneDXData_Set_ValuePersisted` | Getter returns what was set |
| `SpdxData_Set_ValuePersisted` | Getter returns what was set |
| `MultipleSubscribers_BothNotified` | Two handlers both get called |

### 4. Create `SBOMViewer.Blazor.Tests/Services/SpdxParserTests.cs` (10 tests)

| Test | What it verifies |
|------|-----------------|
| `NullInput_ReturnsNull` | Null guard |
| `EmptyString_ReturnsNull` | Empty guard |
| `WhitespaceOnly_ReturnsNull` | Whitespace guard |
| `MalformedJson_ReturnsNull` | Exception handling returns null |
| `MissingName_ReturnsNull` | Name defaults to `""` → null |
| `NullCreationInfo_ReturnsNull` | Explicit null creationInfo → null |
| `ValidMinimal_ReturnsDocument` | Parses name, version, creationInfo |
| `ValidWithPackages_ParsesPackages` | Parses packages, relationships |
| `JsonWithTrailingComma_Succeeds` | AllowTrailingCommas option works |
| `JsonWithComments_Succeeds` | ReadCommentHandling.Skip works |

### 5. Create `SBOMViewer.Blazor.Tests/Services/CycloneDXParserTests.cs` (10 tests)

Same structure as SpdxParser tests but for CycloneDX:
- Null/empty/whitespace/malformed → null
- Missing bomFormat / missing metadata → null
- Valid minimal → parses bomFormat, specVersion, metadata
- Valid with components → parses components, licenses, dependencies
- Trailing commas and comments → succeed

### 6. Update `CLAUDE.md`

Replace "There are no tests in this project currently." with test commands and add test project to the project structure tree.

```bash
dotnet test                                            # Run all tests
dotnet test --filter "FullyQualifiedName~SpdxParser"   # Run single test class
```

## Files to create/modify

| File | Action |
|------|--------|
| `SBOMViewer.Blazor.Tests/SBOMViewer.Blazor.Tests.csproj` | Create (scaffolded) |
| `SBOMViewer.Blazor.Tests/TestData/TestJson.cs` | Create |
| `SBOMViewer.Blazor.Tests/Services/SbomStateTests.cs` | Create |
| `SBOMViewer.Blazor.Tests/Services/SpdxParserTests.cs` | Create |
| `SBOMViewer.Blazor.Tests/Services/CycloneDXParserTests.cs` | Create |
| `SBOMViewer.sln` | Modified by `dotnet sln add` |
| `CLAUDE.md` | Update test section + project structure |

## Key gotcha

`SpdxDocument.CreationInfo` defaults to `new()` — omitting the field from JSON won't make it null. Tests must use explicit `"creationInfo": null` to trigger the null guard. CycloneDX models default to null so omitting fields works as expected there.

## Verification

```bash
dotnet test --verbosity normal
```

Expect 30 passing tests, 0 failures.
