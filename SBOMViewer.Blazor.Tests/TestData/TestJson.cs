namespace SBOMViewer.Blazor.Tests.TestData;

public static class TestJson
{
    // ─── SPDX ───────────────────────────────────────────────

    public const string ValidSpdxMinimal = """
        {
            "spdxVersion": "SPDX-2.2",
            "dataLicense": "CC0-1.0",
            "SPDXID": "SPDXRef-DOCUMENT",
            "name": "Test SBOM",
            "documentNamespace": "https://example.org/test",
            "creationInfo": {
                "created": "2024-01-01T00:00:00Z",
                "creators": ["Tool: test"]
            }
        }
        """;

    public const string ValidSpdxWithPackages = """
        {
            "spdxVersion": "SPDX-2.2",
            "dataLicense": "CC0-1.0",
            "SPDXID": "SPDXRef-DOCUMENT",
            "name": "Test SBOM with packages",
            "documentNamespace": "https://example.org/test",
            "creationInfo": {
                "created": "2024-01-01T00:00:00Z",
                "creators": ["Tool: test"]
            },
            "packages": [
                {
                    "name": "PackageA",
                    "SPDXID": "SPDXRef-PackageA",
                    "versionInfo": "1.0.0",
                    "supplier": "Organization: ExampleOrg",
                    "downloadLocation": "https://example.org/packagea",
                    "licenseConcluded": "MIT",
                    "licenseDeclared": "MIT",
                    "copyrightText": "Copyright 2024 Example"
                },
                {
                    "name": "PackageB",
                    "SPDXID": "SPDXRef-PackageB",
                    "versionInfo": "2.0.0",
                    "supplier": "Organization: ExampleOrg",
                    "downloadLocation": "https://example.org/packageb",
                    "licenseConcluded": "Apache-2.0",
                    "licenseDeclared": "Apache-2.0",
                    "copyrightText": "Copyright 2024 Example"
                }
            ],
            "relationships": [
                {
                    "spdxElementId": "SPDXRef-DOCUMENT",
                    "relationshipType": "DESCRIBES",
                    "relatedSpdxElement": "SPDXRef-PackageA"
                }
            ]
        }
        """;

    public const string SpdxMissingName = """
        {
            "spdxVersion": "SPDX-2.2",
            "dataLicense": "CC0-1.0",
            "SPDXID": "SPDXRef-DOCUMENT",
            "documentNamespace": "https://example.org/test",
            "creationInfo": {
                "created": "2024-01-01T00:00:00Z",
                "creators": ["Tool: test"]
            }
        }
        """;

    public const string SpdxNullCreationInfo = """
        {
            "spdxVersion": "SPDX-2.2",
            "dataLicense": "CC0-1.0",
            "SPDXID": "SPDXRef-DOCUMENT",
            "name": "Test SBOM",
            "documentNamespace": "https://example.org/test",
            "creationInfo": null
        }
        """;

    // ─── CycloneDX ──────────────────────────────────────────

    public const string ValidCycloneDXMinimal = """
        {
            "bomFormat": "CycloneDX",
            "specVersion": "1.6",
            "version": 1,
            "metadata": {
                "timestamp": "2024-01-01T00:00:00Z",
                "tools": [
                    { "vendor": "TestVendor", "name": "TestTool", "version": "1.0" }
                ]
            }
        }
        """;

    public const string ValidCycloneDXWithComponents = """
        {
            "bomFormat": "CycloneDX",
            "specVersion": "1.6",
            "version": 1,
            "metadata": {
                "timestamp": "2024-01-01T00:00:00Z",
                "tools": [
                    { "vendor": "TestVendor", "name": "TestTool", "version": "1.0" }
                ]
            },
            "components": [
                {
                    "type": "library",
                    "bom-ref": "comp-1",
                    "name": "ComponentA",
                    "version": "1.0.0",
                    "licenses": [
                        { "license": { "id": "MIT" } }
                    ]
                },
                {
                    "type": "library",
                    "bom-ref": "comp-2",
                    "name": "ComponentB",
                    "version": "2.0.0",
                    "licenses": [
                        { "license": { "id": "Apache-2.0" } }
                    ]
                }
            ],
            "dependencies": [
                {
                    "ref": "comp-1",
                    "dependsOn": ["comp-2"]
                }
            ]
        }
        """;

    public const string CycloneDXMissingBomFormat = """
        {
            "specVersion": "1.6",
            "version": 1,
            "metadata": {
                "timestamp": "2024-01-01T00:00:00Z"
            }
        }
        """;

    public const string CycloneDXMissingMetadata = """
        {
            "bomFormat": "CycloneDX",
            "specVersion": "1.6",
            "version": 1
        }
        """;

    // ─── CycloneDX 1.7 ────────────────────────────────────────

    public const string ValidCycloneDX17Minimal = """
        {
            "bomFormat": "CycloneDX",
            "specVersion": "1.7",
            "version": 1,
            "metadata": {
                "timestamp": "2025-06-01T00:00:00Z",
                "lifecycles": [
                    { "phase": "build" },
                    { "phase": "operations" }
                ]
            }
        }
        """;

    public const string ValidCycloneDX17WithNewFeatures = """
        {
            "bomFormat": "CycloneDX",
            "specVersion": "1.7",
            "version": 1,
            "metadata": {
                "timestamp": "2025-06-01T00:00:00Z",
                "lifecycles": [
                    { "phase": "build" }
                ]
            },
            "components": [
                {
                    "type": "library",
                    "bom-ref": "comp-1",
                    "name": "ComponentA",
                    "version": "1.0.0",
                    "tags": ["security", "crypto"],
                    "omniborId": ["gitoid:blob:sha256:abc123"],
                    "swhid": ["swh:1:cnt:def456"]
                }
            ],
            "definitions": {
                "standards": [
                    {
                        "bom-ref": "std-1",
                        "name": "NIST SP 800-53",
                        "version": "5.0",
                        "description": "Security and Privacy Controls"
                    }
                ]
            },
            "declarations": {
                "assessors": [
                    {
                        "bom-ref": "assessor-1",
                        "organization": { "name": "Security Corp" }
                    }
                ],
                "claims": [
                    {
                        "bom-ref": "claim-1",
                        "target": "comp-1",
                        "predicate": "compliant-with-std-1"
                    }
                ]
            },
            "formulation": [
                {
                    "bom-ref": "formula-1",
                    "components": [
                        { "type": "library", "name": "BuildTool", "version": "3.0" }
                    ]
                }
            ]
        }
        """;
}
