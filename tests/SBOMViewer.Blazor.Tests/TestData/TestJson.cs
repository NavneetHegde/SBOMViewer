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

    // ─── CycloneDX 1.5 ────────────────────────────────────────

    public const string ValidCycloneDX15Minimal = """
        {
            "bomFormat": "CycloneDX",
            "specVersion": "1.5",
            "version": 1,
            "metadata": {
                "timestamp": "2023-06-01T00:00:00Z",
                "tools": [
                    { "vendor": "TestVendor", "name": "TestTool", "version": "1.0" }
                ]
            }
        }
        """;

    public const string ValidCycloneDX15WithComponents = """
        {
            "bomFormat": "CycloneDX",
            "specVersion": "1.5",
            "version": 1,
            "metadata": {
                "timestamp": "2023-06-01T00:00:00Z"
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
                }
            ]
        }
        """;

    // ─── SPDX 2.3 ───────────────────────────────────────────────

    public const string ValidSpdx23Minimal = """
        {
            "spdxVersion": "SPDX-2.3",
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

    public const string ValidSpdx23WithPackages = """
        {
            "spdxVersion": "SPDX-2.3",
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
                    "licenseConcluded": "MIT",
                    "externalRefs": [
                        { "referenceCategory": "PACKAGE-MANAGER", "referenceType": "purl", "referenceLocator": "pkg:npm/PackageA@1.0.0" }
                    ]
                }
            ]
        }
        """;

    // SPDX 2.3 added primaryPackagePurpose, builtDate/releaseDate/validUntilDate, and
    // the SECURITY externalRefs category (cpe23Type, advisory). The extractors should
    // ignore these new fields and must not mistake a SECURITY ref for a purl.
    public const string ValidSpdx23WithSecurityAndSupplyChainFields = """
        {
            "spdxVersion": "SPDX-2.3",
            "dataLicense": "CC0-1.0",
            "SPDXID": "SPDXRef-DOCUMENT",
            "name": "Test SBOM with 2.3 fields",
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
                    "licenseConcluded": "MIT",
                    "primaryPackagePurpose": "LIBRARY",
                    "builtDate": "2024-11-01T00:00:00Z",
                    "releaseDate": "2024-11-12T00:00:00Z",
                    "validUntilDate": "2027-11-12T00:00:00Z",
                    "externalRefs": [
                        { "referenceCategory": "PACKAGE-MANAGER", "referenceType": "purl", "referenceLocator": "pkg:npm/PackageA@1.0.0" },
                        { "referenceCategory": "SECURITY", "referenceType": "cpe23Type", "referenceLocator": "cpe:2.3:a:vendor:packagea:1.0.0:*:*:*:*:*:*:*" },
                        { "referenceCategory": "SECURITY", "referenceType": "advisory", "referenceLocator": "https://github.com/advisories/GHSA-example-0001" }
                    ]
                }
            ]
        }
        """;

    // ─── SPDX 3.0.1 (JSON-LD) ───────────────────────────────────

    public const string ValidSpdx30Minimal = """
        {
            "@context": "https://spdx.org/rdf/3.0.1/spdx-context.jsonld",
            "@graph": [
                {
                    "type": "SpdxDocument",
                    "spdxId": "https://example.org/doc1",
                    "name": "Test SBOM",
                    "rootElement": ["https://example.org/doc1#SPDXRef-Package-A"]
                },
                {
                    "type": "CreationInfo",
                    "spdxId": "_:creationinfo",
                    "specVersion": "3.0.1",
                    "created": "2024-01-01T00:00:00Z"
                }
            ]
        }
        """;

    public const string ValidSpdx30WithPackages = """
        {
            "@context": "https://spdx.org/rdf/3.0.1/spdx-context.jsonld",
            "@graph": [
                {
                    "type": "SpdxDocument",
                    "spdxId": "https://example.org/doc1",
                    "name": "Test SBOM with packages",
                    "rootElement": ["https://example.org/doc1#SPDXRef-Package-A"]
                },
                {
                    "type": "CreationInfo",
                    "spdxId": "_:creationinfo",
                    "specVersion": "3.0.1",
                    "created": "2024-01-01T00:00:00Z"
                },
                {
                    "type": "software_Package",
                    "spdxId": "https://example.org/doc1#SPDXRef-Package-A",
                    "name": "PackageA",
                    "software_packageVersion": "1.0.0",
                    "software_packageUrl": "pkg:npm/PackageA@1.0.0",
                    "software_concludedLicenseExpression": "MIT"
                }
            ]
        }
        """;

    // SPDX 3.0's headline features: native security/VEX (security_Vulnerability),
    // Agent-based creator attribution, and the build profile (Build). The extractors
    // should still find only the software_Package element and ignore everything else.
    public const string ValidSpdx30WithSecurityAndBuildProfile = """
        {
            "@context": "https://spdx.org/rdf/3.0.1/spdx-context.jsonld",
            "@graph": [
                {
                    "type": "SpdxDocument",
                    "spdxId": "https://example.org/doc1",
                    "name": "Test SBOM with security and build profile",
                    "rootElement": ["https://example.org/doc1#SPDXRef-Package-A"]
                },
                {
                    "type": "CreationInfo",
                    "spdxId": "_:creationinfo",
                    "specVersion": "3.0.1",
                    "created": "2024-01-01T00:00:00Z",
                    "createdBy": ["https://example.org/doc1#SPDXRef-Agent-Test"],
                    "profileConformance": ["core", "software", "security", "build"]
                },
                {
                    "type": "Agent",
                    "spdxId": "https://example.org/doc1#SPDXRef-Agent-Test",
                    "name": "Test Organization"
                },
                {
                    "type": "software_Package",
                    "spdxId": "https://example.org/doc1#SPDXRef-Package-A",
                    "name": "PackageA",
                    "software_packageVersion": "1.0.0",
                    "software_packageUrl": "pkg:npm/PackageA@1.0.0",
                    "software_concludedLicenseExpression": "MIT"
                },
                {
                    "type": "security_Vulnerability",
                    "spdxId": "https://example.org/doc1#SPDXRef-Vuln-CVE-2024-0001",
                    "name": "CVE-2024-0001",
                    "summary": "Example vulnerability",
                    "published": "2024-01-02T00:00:00Z"
                },
                {
                    "type": "Relationship",
                    "spdxId": "_:relationship-vuln",
                    "from": "https://example.org/doc1#SPDXRef-Vuln-CVE-2024-0001",
                    "relationshipType": "hasAssociatedVulnerability",
                    "to": ["https://example.org/doc1#SPDXRef-Package-A"]
                },
                {
                    "type": "Build",
                    "spdxId": "https://example.org/doc1#SPDXRef-Build-CI",
                    "build_buildType": "https://example.org/build-types/ci",
                    "build_configSourceEntrypoint": ["build.yml"]
                },
                {
                    "type": "Relationship",
                    "spdxId": "_:relationship-build",
                    "from": "https://example.org/doc1#SPDXRef-Build-CI",
                    "relationshipType": "generates",
                    "to": ["https://example.org/doc1#SPDXRef-Package-A"]
                }
            ]
        }
        """;
}
