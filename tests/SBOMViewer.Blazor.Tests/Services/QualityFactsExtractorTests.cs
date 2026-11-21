using System.Text.Json;
using FluentAssertions;
using SBOMViewer.Blazor.Models;
using SBOMViewer.Blazor.Services;

namespace SBOMViewer.Blazor.Tests.Services;

public class QualityFactsExtractorTests
{
    private static QualityFacts Extract(string json, SbomFormat format)
    {
        using var doc = JsonDocument.Parse(json);
        return QualityFactsExtractor.Extract(doc.RootElement, format);
    }

    private static readonly string SamplesDir =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../samples"));

    private static QualityFacts ExtractSample(string file, SbomFormat format) =>
        Extract(File.ReadAllText(Path.Combine(SamplesDir, file)), format);

    // ─── CycloneDX ──────────────────────────────────────────

    private const string FullCycloneDx = """
        {
          "bomFormat": "CycloneDX", "specVersion": "1.6", "version": 1,
          "serialNumber": "urn:uuid:3e671687-395b-41f5-a30f-a58921a69b79",
          "metadata": {
            "timestamp": "2026-01-01T00:00:00Z",
            "authors": [ { "name": "Contoso Ltd" } ],
            "lifecycles": [ { "phase": "build" } ],
            "tools": { "components": [ { "type": "application", "name": "cdxgen", "version": "11.0.0" } ] },
            "component": { "type": "application", "bom-ref": "root", "name": "app", "version": "1.0.0" }
          },
          "components": [
            { "type": "library", "bom-ref": "lodash", "name": "lodash", "version": "4.17.21",
              "supplier": { "name": "OpenJS Foundation" },
              "purl": "pkg:npm/lodash@4.17.21",
              "hashes": [ { "alg": "SHA-256", "content": "abc123" } ],
              "licenses": [ { "license": { "id": "MIT" } } ] }
          ],
          "dependencies": [
            { "ref": "root", "dependsOn": [ "lodash" ] },
            { "ref": "lodash", "dependsOn": [] }
          ],
          "signature": { "algorithm": "ES256", "value": "xyz" }
        }
        """;

    [Fact]
    public void CycloneDx_FullyPopulated_IsPresentEverywhere()
    {
        var f = Extract(FullCycloneDx, SbomFormat.CycloneDX_1_6);

        new[] { f.DataFormat, f.Author, f.AuthorSignature, f.Timestamp, f.ToolName, f.ToolVersion,
                f.SbomVersion, f.GenerationContext, f.DependencyRelationships }
            .Should().AllSatisfy(s => s.Should().Be(FactState.Present));

        var c = f.Components.Should().ContainSingle().Subject;
        new[] { c.Name, c.Version, c.Supplier, c.Identifier, c.Hash, c.License }
            .Should().AllSatisfy(s => s.Should().Be(FactState.Present));
        c.InRelationship.Should().BeTrue();
        c.Label.Should().Be("lodash@4.17.21");
    }

    [Theory]
    [InlineData("\"hashes\": [ { \"alg\": \"SHA-256\", \"content\": \"abc123\" } ],", "Hash")]
    [InlineData("\"supplier\": { \"name\": \"OpenJS Foundation\" },", "Supplier")]
    [InlineData("\"purl\": \"pkg:npm/lodash@4.17.21\",", "Identifier")]
    [InlineData("\"version\": \"4.17.21\",", "Version")]
    public void CycloneDx_RemovingOneComponentField_FlipsExactlyThatFact(string fragment, string fact)
    {
        FullCycloneDx.Should().Contain(fragment);
        var f = Extract(FullCycloneDx.Replace(fragment, ""), SbomFormat.CycloneDX_1_6);
        var c = f.Components.Single();

        var states = new Dictionary<string, FactState>
        {
            ["Name"] = c.Name,
            ["Version"] = c.Version,
            ["Supplier"] = c.Supplier,
            ["Identifier"] = c.Identifier,
            ["Hash"] = c.Hash,
            ["License"] = c.License
        };
        states[fact].Should().Be(FactState.Missing);
        states.Where(kv => kv.Key != fact).Should().AllSatisfy(kv => kv.Value.Should().Be(FactState.Present));
    }

    [Fact]
    public void CycloneDx_RemovingDocumentFields_FlipsThoseFacts()
    {
        var json = FullCycloneDx
            .Replace("\"authors\": [ { \"name\": \"Contoso Ltd\" } ],", "")
            .Replace("\"lifecycles\": [ { \"phase\": \"build\" } ],", "")
            .Replace("\"signature\": {", "\"x-not-a-signature\": {");

        var f = Extract(json, SbomFormat.CycloneDX_1_6);

        f.Author.Should().Be(FactState.Missing);
        f.GenerationContext.Should().Be(FactState.Missing);
        f.AuthorSignature.Should().Be(FactState.Missing);
        f.Timestamp.Should().Be(FactState.Present);
        f.ToolName.Should().Be(FactState.Present);
    }

    [Fact]
    public void CycloneDx_MetadataSupplier_IsNotTheSbomAuthor()
    {
        // metadata.supplier/manufacturer describe the component the BOM is about, not who wrote the BOM.
        var json = """
            { "bomFormat": "CycloneDX", "specVersion": "1.6",
              "metadata": { "supplier": { "name": "Contoso" }, "manufacturer": { "name": "Contoso" } },
              "components": [] }
            """;
        Extract(json, SbomFormat.CycloneDX_1_6).Author.Should().Be(FactState.Missing);
    }

    [Fact]
    public void CycloneDx_LegacyToolsArray_IsRecognised()
    {
        var json = """
            { "bomFormat": "CycloneDX", "specVersion": "1.5",
              "metadata": { "tools": [ { "vendor": "CycloneDX", "name": "cdxgen", "version": "9.8.0" } ] },
              "components": [] }
            """;
        var f = Extract(json, SbomFormat.CycloneDX_1_5);
        f.ToolName.Should().Be(FactState.Present);
        f.ToolVersion.Should().Be(FactState.Present);
    }

    [Fact]
    public void CycloneDx_ToolsObjectShape_IsRecognised_AndVersionIsSeparate()
    {
        var json = """
            { "bomFormat": "CycloneDX", "specVersion": "1.6",
              "metadata": { "tools": { "services": [ { "name": "sbom-service" } ] } },
              "components": [] }
            """;
        var f = Extract(json, SbomFormat.CycloneDX_1_6);
        f.ToolName.Should().Be(FactState.Present);
        f.ToolVersion.Should().Be(FactState.Missing);
    }

    [Fact]
    public void CycloneDx_ComponentOutsideDependencyGraph_IsNotInRelationship()
    {
        var json = """
            { "bomFormat": "CycloneDX", "specVersion": "1.6", "metadata": {},
              "components": [
                { "bom-ref": "a", "name": "a" }, { "bom-ref": "b", "name": "b" }, { "name": "c" } ],
              "dependencies": [ { "ref": "a", "dependsOn": [] }, { "ref": "root", "dependsOn": ["a"] } ] }
            """;
        var f = Extract(json, SbomFormat.CycloneDX_1_6);

        f.DependencyRelationships.Should().Be(FactState.Present);
        f.Components.Select(c => c.InRelationship).Should().Equal(true, false, false);
    }

    [Fact]
    public void CycloneDx_OnlyEmptyDependsOn_IsNoDependencyRelationship()
    {
        var json = """
            { "bomFormat": "CycloneDX", "specVersion": "1.6", "metadata": {},
              "components": [ { "bom-ref": "a", "name": "a" } ],
              "dependencies": [ { "ref": "a", "dependsOn": [] } ] }
            """;
        Extract(json, SbomFormat.CycloneDX_1_6).DependencyRelationships.Should().Be(FactState.Missing);
    }

    [Fact]
    public void CycloneDx_UnnamedComponent_IsKeptWithAPlaceholderLabel()
    {
        // ComponentRowExtractor drops unnamed components; the scorecard must report them instead.
        var json = """
            { "bomFormat": "CycloneDX", "specVersion": "1.6", "metadata": {},
              "components": [ { "name": "a" }, { "version": "1.0" } ] }
            """;
        var f = Extract(json, SbomFormat.CycloneDX_1_6);

        f.Components.Should().HaveCount(2);
        f.Components[1].Name.Should().Be(FactState.Missing);
        f.Components[1].Label.Should().Be("(unnamed component #2)@1.0");
    }

    [Fact]
    public void CycloneDx_CpeOrSwidOrOmnibor_CountsAsIdentifier()
    {
        var json = """
            { "bomFormat": "CycloneDX", "specVersion": "1.6", "metadata": {},
              "components": [
                { "name": "a", "cpe": "cpe:2.3:a:x:a:1.0:*:*:*:*:*:*:*" },
                { "name": "b", "swid": { "tagId": "t", "name": "b" } },
                { "name": "c", "omniborId": [ "gitoid:blob:sha1:abc" ] },
                { "name": "d" } ] }
            """;
        Extract(json, SbomFormat.CycloneDX_1_6).Components.Select(c => c.Identifier)
            .Should().Equal(FactState.Present, FactState.Present, FactState.Present, FactState.Missing);
    }

    // ─── SPDX 2.x ───────────────────────────────────────────

    private const string FullSpdx23 = """
        {
          "spdxVersion": "SPDX-2.3", "dataLicense": "CC0-1.0", "SPDXID": "SPDXRef-DOCUMENT",
          "name": "app", "documentNamespace": "https://example.org/app-1.0",
          "creationInfo": {
            "created": "2026-01-01T00:00:00Z",
            "creators": [ "Tool: syft-1.18.0", "Organization: Contoso Ltd" ]
          },
          "packages": [
            { "name": "app", "SPDXID": "SPDXRef-Root", "versionInfo": "1.0.0",
              "supplier": "Organization: Contoso Ltd",
              "checksums": [ { "algorithm": "SHA256", "checksumValue": "abc" } ],
              "licenseConcluded": "MIT",
              "externalRefs": [ { "referenceCategory": "PACKAGE-MANAGER", "referenceType": "purl", "referenceLocator": "pkg:npm/app@1.0.0" } ] },
            { "name": "dep", "SPDXID": "SPDXRef-Dep", "versionInfo": "2.0.0",
              "supplier": "Organization: Dep Org",
              "checksums": [ { "algorithm": "SHA256", "checksumValue": "def" } ],
              "licenseConcluded": "Apache-2.0",
              "externalRefs": [ { "referenceCategory": "SECURITY", "referenceType": "cpe23Type", "referenceLocator": "cpe:2.3:a:dep:dep:2.0.0:*:*:*:*:*:*:*" } ] }
          ],
          "relationships": [
            { "spdxElementId": "SPDXRef-DOCUMENT", "relationshipType": "DESCRIBES", "relatedSpdxElement": "SPDXRef-Root" },
            { "spdxElementId": "SPDXRef-Root", "relationshipType": "DEPENDS_ON", "relatedSpdxElement": "SPDXRef-Dep" }
          ]
        }
        """;

    [Fact]
    public void Spdx23_FullyPopulated_IsPresentExceptWhatTheFormatCannotSay()
    {
        var f = Extract(FullSpdx23, SbomFormat.SPDX_2_3);

        new[] { f.DataFormat, f.Author, f.Timestamp, f.ToolName, f.ToolVersion, f.SbomVersion, f.DependencyRelationships }
            .Should().AllSatisfy(s => s.Should().Be(FactState.Present));
        f.GenerationContext.Should().Be(FactState.NotExpressible);
        f.AuthorSignature.Should().Be(FactState.NotExpressible);

        f.Components.Should().HaveCount(2).And.AllSatisfy(c =>
        {
            new[] { c.Name, c.Version, c.Supplier, c.Identifier, c.Hash, c.License }
                .Should().AllSatisfy(s => s.Should().Be(FactState.Present));
            c.InRelationship.Should().BeTrue();
        });
    }

    [Fact]
    public void Spdx_NoAssertionSupplier_IsDeclaredUnknown_NotMissingOrPresent()
    {
        var f = Extract(FullSpdx23.Replace("\"supplier\": \"Organization: Dep Org\"", "\"supplier\": \"NOASSERTION\""), SbomFormat.SPDX_2_3);
        f.Components[1].Supplier.Should().Be(FactState.DeclaredUnknown);
        f.Components[0].Supplier.Should().Be(FactState.Present);
    }

    [Fact]
    public void Spdx_NoAssertionLicense_IsDeclaredUnknown_NotMissingOrPresent()
    {
        var f = Extract(FullSpdx23.Replace("\"licenseConcluded\": \"Apache-2.0\"", "\"licenseConcluded\": \"NOASSERTION\""), SbomFormat.SPDX_2_3);
        f.Components[1].License.Should().Be(FactState.DeclaredUnknown);
    }

    [Fact]
    public void Spdx_NoneLicense_IsADefiniteAnswer()
    {
        // NONE asserts there is no license — a real value, unlike NOASSERTION.
        var f = Extract(FullSpdx23.Replace("\"licenseConcluded\": \"Apache-2.0\"", "\"licenseConcluded\": \"NONE\""), SbomFormat.SPDX_2_3);
        f.Components[1].License.Should().Be(FactState.Present);
    }

    [Fact]
    public void Spdx_DeclaredLicenseSatisfiesWhenConcludedIsNoAssertion()
    {
        var f = Extract(FullSpdx23.Replace("\"licenseConcluded\": \"Apache-2.0\"",
            "\"licenseConcluded\": \"NOASSERTION\", \"licenseDeclared\": \"Apache-2.0\""), SbomFormat.SPDX_2_3);
        f.Components[1].License.Should().Be(FactState.Present);
    }

    [Fact]
    public void Spdx_ToolOnlyCreator_HasNoAuthor()
    {
        var f = Extract(FullSpdx23.Replace(", \"Organization: Contoso Ltd\" ]", " ]"), SbomFormat.SPDX_2_3);
        f.Author.Should().Be(FactState.Missing);
        f.ToolName.Should().Be(FactState.Present);
    }

    [Fact]
    public void Spdx_ToolWithoutEmbeddedVersion_HasNoToolVersion()
    {
        var f = Extract(FullSpdx23.Replace("Tool: syft-1.18.0", "Tool: syft"), SbomFormat.SPDX_2_3);
        f.ToolName.Should().Be(FactState.Present);
        f.ToolVersion.Should().Be(FactState.Missing);
    }

    [Fact]
    public void Spdx_NonIdentifierExternalRef_DoesNotCountAsIdentifier()
    {
        var f = Extract(FullSpdx23.Replace("\"referenceType\": \"cpe23Type\"", "\"referenceType\": \"advisory\""), SbomFormat.SPDX_2_3);
        f.Components[1].Identifier.Should().Be(FactState.Missing);
    }

    [Fact]
    public void Spdx_OnlyDescribes_IsNoDependencyRelationship()
    {
        var json = FullSpdx23.Replace("\"relationshipType\": \"DEPENDS_ON\"", "\"relationshipType\": \"OTHER\"");
        var f = Extract(json, SbomFormat.SPDX_2_3);

        f.DependencyRelationships.Should().Be(FactState.Missing);
        f.Components[0].InRelationship.Should().BeTrue();   // via DESCRIBES
        f.Components[1].InRelationship.Should().BeFalse();
    }

    // ─── SPDX 3.0.1 ─────────────────────────────────────────

    private const string FullSpdx3 = """
        {
          "@context": "https://spdx.org/rdf/3.0.1/spdx-context.jsonld",
          "@graph": [
            { "type": "CreationInfo", "@id": "_:ci", "specVersion": "3.0.1", "created": "2026-01-01T00:00:00Z",
              "createdBy": [ "urn:agent:contoso" ], "createdUsing": [ "urn:tool:syft" ] },
            { "type": "Organization", "spdxId": "urn:agent:contoso", "creationInfo": "_:ci", "name": "Contoso Ltd" },
            { "type": "Tool", "spdxId": "urn:tool:syft", "creationInfo": "_:ci", "name": "syft-1.18.0" },
            { "type": "SpdxDocument", "spdxId": "urn:doc", "creationInfo": "_:ci", "rootElement": [ "urn:sbom" ] },
            { "type": "software_Sbom", "spdxId": "urn:sbom", "creationInfo": "_:ci", "software_sbomType": [ "build" ],
              "rootElement": [ "urn:pkg:root" ] },
            { "type": "software_Package", "spdxId": "urn:pkg:root", "creationInfo": "_:ci", "name": "app",
              "software_packageVersion": "1.0.0", "suppliedBy": "urn:agent:contoso",
              "software_packageUrl": "pkg:npm/app@1.0.0",
              "verifiedUsing": [ { "type": "Hash", "algorithm": "sha256", "hashValue": "abc" } ] },
            { "type": "software_Package", "spdxId": "urn:pkg:dep", "creationInfo": "_:ci", "name": "dep",
              "software_packageVersion": "2.0.0", "suppliedBy": "urn:agent:contoso",
              "externalIdentifier": [ { "type": "ExternalIdentifier", "externalIdentifierType": "cpe23", "identifier": "cpe:2.3:a:x:dep:2.0.0:*:*:*:*:*:*:*" } ],
              "verifiedUsing": [ { "type": "Hash", "algorithm": "sha256", "hashValue": "def" } ] },
            { "type": "simplelicensing_LicenseExpression", "spdxId": "urn:lic:mit", "creationInfo": "_:ci",
              "simplelicensing_licenseExpression": "MIT" },
            { "type": "Relationship", "spdxId": "urn:rel:1", "creationInfo": "_:ci", "from": "urn:pkg:root",
              "relationshipType": "dependsOn", "to": [ "urn:pkg:dep" ] },
            { "type": "Relationship", "spdxId": "urn:rel:2", "creationInfo": "_:ci", "from": "urn:pkg:root",
              "relationshipType": "hasConcludedLicense", "to": [ "urn:lic:mit" ] },
            { "type": "Relationship", "spdxId": "urn:rel:3", "creationInfo": "_:ci", "from": "urn:pkg:dep",
              "relationshipType": "hasDeclaredLicense", "to": [ "https://spdx.org/rdf/3.0.1/terms/Expanded/NoAssertionLicense" ] }
          ]
        }
        """;

    [Fact]
    public void Spdx3_FullyPopulated_IsPresentExceptWhatTheFormatCannotSay()
    {
        var f = Extract(FullSpdx3, SbomFormat.SPDX_3_0);

        new[] { f.DataFormat, f.Author, f.Timestamp, f.ToolName, f.ToolVersion, f.GenerationContext, f.DependencyRelationships }
            .Should().AllSatisfy(s => s.Should().Be(FactState.Present));
        f.AuthorSignature.Should().Be(FactState.NotExpressible);
        f.SbomVersion.Should().Be(FactState.NotExpressible);

        f.Components.Should().HaveCount(2);
        var root = f.Components[0];
        new[] { root.Name, root.Version, root.Supplier, root.Identifier, root.Hash, root.License }
            .Should().AllSatisfy(s => s.Should().Be(FactState.Present));
        f.Components[1].Identifier.Should().Be(FactState.Present);
        f.Components.Should().AllSatisfy(c => c.InRelationship.Should().BeTrue());
    }

    [Fact]
    public void Spdx3_NoAssertionLicenseIndividual_IsDeclaredUnknown()
    {
        Extract(FullSpdx3, SbomFormat.SPDX_3_0).Components[1].License.Should().Be(FactState.DeclaredUnknown);
    }

    [Fact]
    public void Spdx3_NoSbomElement_HasNoGenerationContext()
    {
        var json = FullSpdx3.Replace("\"software_sbomType\": [ \"build\" ],", "");
        Extract(json, SbomFormat.SPDX_3_0).GenerationContext.Should().Be(FactState.Missing);
    }

    [Fact]
    public void Spdx3_MissingHash_FlipsOnlyHash()
    {
        var json = FullSpdx3.Replace("\"hashValue\": \"abc\"", "\"hashValue\": \"\"");
        var root = Extract(json, SbomFormat.SPDX_3_0).Components[0];
        root.Hash.Should().Be(FactState.Missing);
        root.Identifier.Should().Be(FactState.Present);
        root.Supplier.Should().Be(FactState.Present);
    }

    [Fact]
    public void Spdx3_PackageHash_FollowsHasDistributionArtifact()
    {
        // Shape of spdx-tools' 2.3 → 3.0.1 conversion: the package keeps only a PackageVerificationCode and
        // its checksum moves to the distribution-artifact file.
        var json = """
            { "@context": "https://spdx.org/rdf/3.0.1/spdx-context.jsonld", "@graph": [
              { "@id": "_:ci", "type": "CreationInfo", "specVersion": "3.0.1", "created": "2024-01-01T00:00:00Z" },
              { "spdxId": "pkg", "type": "software_Package", "name": "app", "creationInfo": "_:ci",
                "verifiedUsing": [ { "type": "PackageVerificationCode", "algorithm": "sha1", "hashValue": "c124" } ] },
              { "spdxId": "jar", "type": "software_File", "name": "app.jar", "creationInfo": "_:ci",
                "verifiedUsing": [ { "type": "Hash", "algorithm": "sha1", "hashValue": "b8a7" } ] },
              { "spdxId": "other", "type": "software_Package", "name": "lib", "creationInfo": "_:ci",
                "verifiedUsing": [ { "type": "PackageVerificationCode", "algorithm": "sha1", "hashValue": "d00d" } ] },
              { "spdxId": "r1", "type": "Relationship", "relationshipType": "hasDistributionArtifact",
                "from": "pkg", "to": [ "jar" ], "creationInfo": "_:ci" } ] }
            """;
        var f = Extract(json, SbomFormat.SPDX_3_0);

        f.Components.Should().HaveCount(2, "the software_File is not a component itself");
        f.Components[0].Hash.Should().Be(FactState.Present);
        f.Components[1].Hash.Should().Be(FactState.Missing, "a PackageVerificationCode is not the component's hash");
    }

    [Fact]
    public void CycloneDx_CryptographicAssetAndFileComponents_AreNotCounted()
    {
        // CBOM entries are not software; `file` components are the counterpart of SPDX files, which the SPDX
        // extractors do not count either (syft writes the same workflow files both ways).
        var json = """
            { "bomFormat": "CycloneDX", "specVersion": "1.7", "metadata": {},
              "components": [
                { "type": "library", "name": "a", "version": "1" },
                { "type": "cryptographic-asset", "name": "AES-256-GCM" },
                { "type": "file", "name": "/.github/workflows/ci.yml", "hashes": [ { "alg": "SHA-256", "content": "12f3" } ] },
                { "name": "untyped" } ] }
            """;
        var f = Extract(json, SbomFormat.CycloneDX_1_7);

        f.Components.Select(c => c.Label).Should().Equal("a@1", "untyped");
    }

    // ─── Shared helpers & edge cases ────────────────────────

    [Theory]
    [InlineData("syft-0.100.0", "syft", "0.100.0")]
    [InlineData("Microsoft.SBOMTool-2.2.5", "Microsoft.SBOMTool", "2.2.5")]
    [InlineData("cdxgen 10.2.1", "cdxgen", "10.2.1")]
    [InlineData("trivy-v0.50.1", "trivy", "0.50.1")]
    [InlineData("syft", "syft", null)]
    [InlineData("my-tool", "my-tool", null)]
    public void SplitToolVersion_ParsesTrailingVersion(string input, string name, string? version)
    {
        QualityFactsExtractor.SplitToolVersion(input).Should().Be((name, version));
    }

    [Fact]
    public void UnknownFormat_ReturnsEmptyFacts()
    {
        using var doc = JsonDocument.Parse("{}");
        var f = QualityFactsExtractor.Extract(doc.RootElement, null);
        f.Components.Should().BeEmpty();
        f.Author.Should().Be(FactState.Missing);
    }

    // ─── Repo fixtures ──────────────────────────────────────

    [Fact]
    public void Sample_CycloneDx16Minimal_HasNoAuthorNoSupplierAndPartialHashes()
    {
        var f = ExtractSample("cyclonedx-1.6-minimal.json", SbomFormat.CycloneDX_1_6);

        f.Author.Should().Be(FactState.Missing);
        f.ToolName.Should().Be(FactState.Present);
        f.ToolVersion.Should().Be(FactState.Present);
        f.Components.Should().HaveCount(3);
        f.Components.Should().AllSatisfy(c => c.Supplier.Should().Be(FactState.Missing));
        f.Components.Count(c => c.Hash == FactState.Present).Should().Be(1);
        f.Components.Should().AllSatisfy(c => c.InRelationship.Should().BeTrue());
    }

    [Fact]
    public void Sample_Spdx22Minimal_DeclaresOneSupplierUnknown()
    {
        var f = ExtractSample("spdx-2.2-minimal.json", SbomFormat.SPDX_2_2);

        f.Author.Should().Be(FactState.Missing);              // only a Tool: creator
        f.ToolVersion.Should().Be(FactState.Present);         // "syft-0.100.0"
        f.Components.Select(c => c.Supplier).Should().Equal(FactState.Present, FactState.Present, FactState.DeclaredUnknown);
        f.Components.Should().AllSatisfy(c => c.Identifier.Should().Be(FactState.Missing));
    }

    [Fact]
    public void Sample_Cdx17Full_HasLifecycles()
    {
        ExtractSample("cyclonedx-1.7-full.json", SbomFormat.CycloneDX_1_7).GenerationContext.Should().Be(FactState.Present);
    }

    [Fact]
    public void Sample_Spdx301Full_ResolvesAuthorAgentAndCoversEveryPackage()
    {
        var f = ExtractSample("spdx-3.0.1-full.json", SbomFormat.SPDX_3_0);

        f.Author.Should().Be(FactState.Present);
        f.ToolName.Should().Be(FactState.Missing);
        f.Components.Should().HaveCount(3);
        f.Components.Should().AllSatisfy(c =>
        {
            c.License.Should().Be(FactState.Present);
            c.InRelationship.Should().BeTrue();
        });
    }
}
