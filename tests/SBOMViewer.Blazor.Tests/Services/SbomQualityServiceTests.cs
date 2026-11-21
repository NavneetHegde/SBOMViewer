using System.Text.Json;
using FluentAssertions;
using SBOMViewer.Blazor.Models;
using SBOMViewer.Blazor.Services;
using SBOMViewer.Blazor.Tests.TestData;

namespace SBOMViewer.Blazor.Tests.Services;

public class SbomQualityServiceTests
{
    private static ComponentFacts Perfect(string name) => new()
    {
        Label = name,
        Name = FactState.Present,
        Version = FactState.Present,
        Supplier = FactState.Present,
        Identifier = FactState.Present,
        Hash = FactState.Present,
        License = FactState.Present,
        InRelationship = true
    };

    private static QualityFacts PerfectDoc(params ComponentFacts[] components) => new()
    {
        DataFormat = FactState.Present,
        Author = FactState.Present,
        AuthorSignature = FactState.Present,
        Timestamp = FactState.Present,
        ToolName = FactState.Present,
        ToolVersion = FactState.Present,
        SbomVersion = FactState.Present,
        GenerationContext = FactState.Present,
        DependencyRelationships = FactState.Present,
        Components = components.Length == 0 ? [Perfect("a"), Perfect("b")] : components
    };

    private static QualityCheck Check(QualityReport r, string id) => r.Checks.Single(c => c.Id == id);

    [Fact]
    public void Profiles_HaveTheConfirmedElementCounts()
    {
        // NTIA 2021: seven data fields. CISA 2026: the data fields (format name/version and hash
        // value/algorithm each merged into one check) plus the Coverage practice.
        SbomQualityService.Evaluate(PerfectDoc(), QualityProfile.Ntia2021).Checks.Should().HaveCount(7);
        SbomQualityService.Evaluate(PerfectDoc(), QualityProfile.Cisa2026).Checks.Should().HaveCount(16);
    }

    [Fact]
    public void EveryCheck_CarriesAReferenceAndAFixHint()
    {
        foreach (var profile in SbomQualityService.Profiles)
            SbomQualityService.Evaluate(PerfectDoc(), profile).Checks.Should().AllSatisfy(c =>
            {
                c.Reference.Should().StartWith(profile == QualityProfile.Cisa2026 ? "CISA 2026" : "NTIA 2021");
                c.FixHint.Should().NotBeNullOrWhiteSpace();
            });
    }

    [Theory]
    [InlineData(QualityProfile.Ntia2021)]
    [InlineData(QualityProfile.Cisa2026)]
    public void PerfectDocument_PassesEverything(QualityProfile profile)
    {
        var r = SbomQualityService.Evaluate(PerfectDoc(), profile);
        r.Passed.Should().Be(r.Checks.Count);
        r.Applicable.Should().Be(r.Checks.Count);
        r.ComponentsWithGap.Should().Be(0);
        r.AverageComponentCoverage.Should().Be(100);
    }

    [Fact]
    public void ComponentCheck_ZeroPercent_Fails()
    {
        var comps = new[] { Perfect("a") with { Hash = FactState.Missing }, Perfect("b") with { Hash = FactState.Missing } };
        var c = Check(SbomQualityService.Evaluate(PerfectDoc(comps), QualityProfile.Cisa2026), "hash");

        c.Status.Should().Be(CheckStatus.Fail);
        c.Affected.Should().Be(2);
        c.Total.Should().Be(2);
        c.AffectedComponents.Should().Equal("a", "b");
    }

    [Fact]
    public void ComponentCheck_InBetween_IsPartialWithCounts()
    {
        var comps = new[] { Perfect("a"), Perfect("b") with { Hash = FactState.Missing }, Perfect("c"), Perfect("d") };
        var r = SbomQualityService.Evaluate(PerfectDoc(comps), QualityProfile.Cisa2026);
        var c = Check(r, "hash");

        c.Status.Should().Be(CheckStatus.Partial);
        c.Affected.Should().Be(1);
        c.Total.Should().Be(4);
        c.Satisfied.Should().Be(3);
        c.CoveragePct.Should().Be(75);
        c.AffectedComponents.Should().Equal("b");
        r.ComponentsWithGap.Should().Be(1);
    }

    [Fact]
    public void ComponentCheck_AllPresent_Passes()
    {
        Check(SbomQualityService.Evaluate(PerfectDoc(), QualityProfile.Ntia2021), "supplier")
            .Status.Should().Be(CheckStatus.Pass);
    }

    [Fact]
    public void DeclaredUnknown_IsCountedSeparately_ButIsNotAPass()
    {
        var comps = new[] { Perfect("a") with { Supplier = FactState.DeclaredUnknown }, Perfect("b") with { Supplier = FactState.Missing }, Perfect("c") };
        var c = Check(SbomQualityService.Evaluate(PerfectDoc(comps), QualityProfile.Ntia2021), "supplier");

        c.Status.Should().Be(CheckStatus.Partial);
        c.Affected.Should().Be(2);
        c.DeclaredUnknown.Should().Be(1);
    }

    [Fact]
    public void DocumentCheck_DeclaredUnknown_IsPartial()
    {
        var facts = PerfectDoc() with { ToolVersion = FactState.DeclaredUnknown };
        Check(SbomQualityService.Evaluate(facts, QualityProfile.Cisa2026), "tool-version")
            .Status.Should().Be(CheckStatus.Partial);
    }

    [Fact]
    public void NotExpressible_IsExcludedFromApplicable_SoAFullPassIsReachable()
    {
        var facts = PerfectDoc() with
        {
            GenerationContext = FactState.NotExpressible,
            AuthorSignature = FactState.NotExpressible
        };
        var r = SbomQualityService.Evaluate(facts, QualityProfile.Cisa2026);

        Check(r, "generation-context").Status.Should().Be(CheckStatus.NotExpressible);
        r.Applicable.Should().Be(14);
        r.Passed.Should().Be(14);
    }

    [Fact]
    public void ZeroComponents_DoesNotDivideByZero_AndComponentChecksFail()
    {
        var facts = PerfectDoc() with { Components = [] };
        var r = SbomQualityService.Evaluate(facts, QualityProfile.Cisa2026);

        r.Checks.Where(c => c.Scope == CheckScope.Component).Should().AllSatisfy(c =>
        {
            c.Status.Should().Be(CheckStatus.Fail);
            c.Total.Should().Be(0);
            c.CoveragePct.Should().Be(0);
        });
        r.AverageComponentCoverage.Should().BeNull();
        r.ComponentsWithGap.Should().Be(0);
    }

    [Fact]
    public void Coverage_FailsForEveryComponent_WhenThereAreNoRelationships()
    {
        var facts = PerfectDoc() with { DependencyRelationships = FactState.Missing };
        var r = SbomQualityService.Evaluate(facts, QualityProfile.Cisa2026);

        Check(r, "coverage").Status.Should().Be(CheckStatus.Fail);
        Check(r, "dependencies").Status.Should().Be(CheckStatus.Fail);
    }

    [Fact]
    public void Coverage_IsPartial_WhenSomeComponentsAreOutsideTheGraph()
    {
        var comps = new[] { Perfect("a"), Perfect("b") with { InRelationship = false } };
        var c = Check(SbomQualityService.Evaluate(PerfectDoc(comps), QualityProfile.Cisa2026), "coverage");

        c.Status.Should().Be(CheckStatus.Partial);
        c.AffectedComponents.Should().Equal("b");
        c.Note.Should().Contain("Heuristic");
    }

    [Fact]
    public void SwitchingProfile_ChangesTheCheckCount()
    {
        var facts = PerfectDoc();
        SbomQualityService.Evaluate(facts, QualityProfile.Ntia2021).Applicable
            .Should().NotBe(SbomQualityService.Evaluate(facts, QualityProfile.Cisa2026).Applicable);
    }

    [Fact]
    public void Ntia_DoesNotRequireHashesOrLicenses()
    {
        var comps = new[] { Perfect("a") with { Hash = FactState.Missing, License = FactState.Missing } };
        var facts = PerfectDoc(comps) with { ToolName = FactState.Missing, GenerationContext = FactState.Missing };

        var r = SbomQualityService.Evaluate(facts, QualityProfile.Ntia2021);
        r.Passed.Should().Be(r.Applicable);
    }

    // ─── End to end over repo fixtures ──────────────────────

    private static readonly string SamplesDir =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../samples"));

    private static QualityReport EvaluateSample(string file, SbomFormat format, QualityProfile profile)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(SamplesDir, file)));
        return SbomQualityService.Evaluate(QualityFactsExtractor.Extract(doc.RootElement, format), profile);
    }

    [Fact]
    public void Sample_Spdx22Minimal_FailsTheChecksYouWouldExpect()
    {
        var r = EvaluateSample("spdx-2.2-minimal.json", SbomFormat.SPDX_2_2, QualityProfile.Ntia2021);

        Check(r, "author").Status.Should().Be(CheckStatus.Fail);          // only a Tool: creator
        Check(r, "identifier").Status.Should().Be(CheckStatus.Fail);      // no externalRefs
        Check(r, "supplier").Status.Should().Be(CheckStatus.Partial);     // dotenv is NOASSERTION
        Check(r, "supplier").DeclaredUnknown.Should().Be(1);
        Check(r, "timestamp").Status.Should().Be(CheckStatus.Pass);
        Check(r, "dependencies").Status.Should().Be(CheckStatus.Pass);
    }

    [Fact]
    public void Sample_Spdx23Full_PassesMostNtiaChecks()
    {
        var r = EvaluateSample("spdx-2.3-full.json", SbomFormat.SPDX_2_3, QualityProfile.Ntia2021);

        // The root package carries no purl, so identifiers are 3 of 4; everything else passes.
        r.Checks.Where(c => c.Id != "identifier").Should().AllSatisfy(c => c.Status.Should().Be(CheckStatus.Pass));
        Check(r, "identifier").Status.Should().Be(CheckStatus.Partial);
    }

    [Fact]
    public void Sample_Spdx23Full_CisaExcludesWhatSpdx2CannotExpress()
    {
        var r = EvaluateSample("spdx-2.3-full.json", SbomFormat.SPDX_2_3, QualityProfile.Cisa2026);

        Check(r, "generation-context").Status.Should().Be(CheckStatus.NotExpressible);
        Check(r, "signature").Status.Should().Be(CheckStatus.NotExpressible);
        r.Applicable.Should().Be(14);
    }

    [Fact]
    public void Sample_Cdx16Full_FailsAuthorAndSupplier_ButHasToolsAndGraph()
    {
        // The "full" CycloneDX fixtures were written before this feature and omit metadata.authors and
        // component suppliers. Five of the eight components carry component-level authors, which count as
        // the producer. The rules are right; the fixtures are simply incomplete in those fields.
        var r = EvaluateSample("cyclonedx-1.6-full.json", SbomFormat.CycloneDX_1_6, QualityProfile.Cisa2026);

        Check(r, "author").Status.Should().Be(CheckStatus.Fail);
        Check(r, "supplier").Status.Should().Be(CheckStatus.Partial);
        Check(r, "supplier").Satisfied.Should().Be(5);
        Check(r, "tool-name").Status.Should().Be(CheckStatus.Pass);
        Check(r, "tool-version").Status.Should().Be(CheckStatus.Pass);
        Check(r, "sbom-version").Status.Should().Be(CheckStatus.Pass);
        Check(r, "license").Status.Should().Be(CheckStatus.Pass);
        Check(r, "coverage").Status.Should().Be(CheckStatus.Pass);
        Check(r, "hash").Status.Should().Be(CheckStatus.Partial);
    }

    [LocalSampleFact("sample-1.7.cdx.json")]
    public void Sample_Cdx17EdgeCases_MatchesAHandReading()
    {
        // Hand-written 1.7 fixture: tools object, lifecycles, metadata.supplier (not the author), container,
        // operating-system, a CBOM cryptographic asset and a service.
        var r = EvaluateSample("sample-1.7.cdx.json", SbomFormat.CycloneDX_1_7, QualityProfile.Cisa2026);

        r.ComponentCount.Should().Be(10, "the AES-256-GCM cryptographic asset is not a software component");
        Check(r, "author").Status.Should().Be(CheckStatus.Pass);              // metadata.authors
        Check(r, "signature").Status.Should().Be(CheckStatus.Fail);
        Check(r, "generation-context").Status.Should().Be(CheckStatus.Pass);  // lifecycles: build
        Check(r, "tool-name").Status.Should().Be(CheckStatus.Pass);           // tools.components
        Check(r, "tool-version").Status.Should().Be(CheckStatus.Pass);
        Check(r, "supplier").Satisfied.Should().Be(1);                        // only openssl's publisher
        Check(r, "hash").Satisfied.Should().Be(1);                            // only express
        Check(r, "identifier").Satisfied.Should().Be(9);                      // alpine has neither purl nor cpe
        Check(r, "license").Satisfied.Should().Be(8);                         // node and alpine have none
        Check(r, "version").Status.Should().Be(CheckStatus.Pass);
        Check(r, "coverage").Status.Should().Be(CheckStatus.Pass);
        r.Passed.Should().Be(11);
    }

    [LocalSampleFact("cdxgen-12.9.3_express-5.3.0.cdx.json")]
    public void Sample_Cdxgen1293_Express_FailsOnlySignatureAndSupplier()
    {
        // Real generator output: cdxgen 12.9.3 over express 5.3.0's package-lock (368 npm components).
        // cdxgen populates hashes, purls, licenses and the full dependency graph, but no component-level
        // supplier/publisher/author for npm packages, and does not sign.
        var r = EvaluateSample("cdxgen-12.9.3_express-5.3.0.cdx.json", SbomFormat.CycloneDX_1_7, QualityProfile.Cisa2026);

        r.ComponentCount.Should().Be(368);
        r.Checks.Where(c => c.Status != CheckStatus.Pass).Select(c => c.Id)
            .Should().BeEquivalentTo(["signature", "supplier"]);
        Check(r, "supplier").Status.Should().Be(CheckStatus.Fail);
        Check(r, "supplier").Satisfied.Should().Be(0);
        r.Passed.Should().Be(14);
        r.Applicable.Should().Be(16);
    }

    [LocalSampleFact("cdxgen-12.9.3_express-5.3.0_signed.cdx.json")]
    public void Sample_Cdxgen1293_ExpressSigned_PassesSignature()
    {
        // The same project signed by cdxgen (JSF, RS512, public key embedded; key in sbom_sign_public.pem).
        // This run also recorded component authors for 307 of 368 packages, so supplier moves from Fail
        // to Partial: the generator's data changed, not the rule.
        var r = EvaluateSample("cdxgen-12.9.3_express-5.3.0_signed.cdx.json", SbomFormat.CycloneDX_1_7, QualityProfile.Cisa2026);

        Check(r, "signature").Status.Should().Be(CheckStatus.Pass);
        Check(r, "signature").Note.Should().Contain("does not verify");
        Check(r, "supplier").Status.Should().Be(CheckStatus.Partial);
        Check(r, "supplier").Satisfied.Should().Be(307);
        r.Checks.Where(c => c.Status != CheckStatus.Pass).Select(c => c.Id).Should().BeEquivalentTo(["supplier"]);
        r.Passed.Should().Be(15);
    }

    // ─── Real generator output: express 5.3.0 scanned by trivy, syft and Microsoft sbom-tool ───

    private static string[] NotPassing(QualityReport r) =>
        r.Checks.Where(c => c.Status is CheckStatus.Partial or CheckStatus.Fail).Select(c => c.Id).ToArray();

    [LocalSampleFact("trivy-0.76.2_express-5.3.0.cdx.json")]
    public void Sample_Trivy0762_Express()
    {
        // No metadata.authors (the manufacturer belongs to the tool), no lifecycles, no hashes or suppliers
        // for npm packages. The one gap in identifier/license/version is trivy's "package-lock.json"
        // application component, which really does carry none of them.
        var r = EvaluateSample("trivy-0.76.2_express-5.3.0.cdx.json", SbomFormat.CycloneDX_1_7, QualityProfile.Cisa2026);

        r.ComponentCount.Should().Be(65);
        NotPassing(r).Should().BeEquivalentTo(
            ["author", "signature", "generation-context", "supplier", "hash", "identifier", "license", "version"]);
        Check(r, "version").Satisfied.Should().Be(64);
        Check(r, "tool-version").Status.Should().Be(CheckStatus.Pass);
        r.Passed.Should().Be(8);
    }

    [LocalSampleFact("syft-1.57.2_express-5.3.0.cdx.json")]
    public void Sample_Syft1572_ExpressCdx_IgnoresFileComponents()
    {
        // syft lists the ten .github/workflows/*.yml files as `file` components; they are excluded, as SPDX
        // files are. The 25 GitHub Actions it found have no dependency edges in this output, so coverage is
        // Partial; that is syft's graph, not the heuristic.
        var r = EvaluateSample("syft-1.57.2_express-5.3.0.cdx.json", SbomFormat.CycloneDX_1_7, QualityProfile.Cisa2026);

        r.ComponentCount.Should().Be(90);
        NotPassing(r).Should().BeEquivalentTo(
            ["author", "signature", "generation-context", "supplier", "hash", "license", "coverage"]);
        Check(r, "hash").Status.Should().Be(CheckStatus.Fail, "only the excluded workflow files carried hashes");
        Check(r, "license").Satisfied.Should().Be(65);
        Check(r, "coverage").Satisfied.Should().Be(65);
        r.Passed.Should().Be(9);
    }

    [LocalSampleFact("syft-1.57.2_express-5.3.0.spdx.json")]
    public void Sample_Syft1572_ExpressSpdx()
    {
        // The same scan as SPDX 2.3: "Organization: Anchore, Inc" is the creator, so author passes; supplier
        // and license are NOASSERTION for most packages (reported as declared unknown); no package checksums.
        // The 91st package is the scanned directory "express", which has no version or purl.
        var r = EvaluateSample("syft-1.57.2_express-5.3.0.spdx.json", SbomFormat.SPDX_2_3, QualityProfile.Cisa2026);

        r.ComponentCount.Should().Be(91);
        NotPassing(r).Should().BeEquivalentTo(["supplier", "hash", "identifier", "license", "version"]);
        Check(r, "supplier").DeclaredUnknown.Should().Be(66);
        Check(r, "license").Satisfied.Should().Be(65);
        Check(r, "license").DeclaredUnknown.Should().Be(26);
        Check(r, "tool-version").Status.Should().Be(CheckStatus.Pass);   // "Tool: syft-1.57.2"
        r.Passed.Should().Be(9);
        r.Applicable.Should().Be(14);
    }

    [LocalSampleFact("syft-1.57.2_express-5.3.0.cdx.json", "syft-1.57.2_express-5.3.0.spdx.json")]
    public void Sample_Syft1572_LicenseCountIsTheSameInBothFormats()
    {
        var cdx = Check(EvaluateSample("syft-1.57.2_express-5.3.0.cdx.json", SbomFormat.CycloneDX_1_7, QualityProfile.Cisa2026), "license");
        var spdx = Check(EvaluateSample("syft-1.57.2_express-5.3.0.spdx.json", SbomFormat.SPDX_2_3, QualityProfile.Cisa2026), "license");

        cdx.Satisfied.Should().Be(spdx.Satisfied);
    }

    [LocalSampleFact("sbom-tool-4.1.13_express-5.3.0.spdx.json")]
    public void Sample_SbomTool4113_ExpressSpdx22()
    {
        // Microsoft sbom-tool: "Tool: Microsoft.SBOMTool-4.1.13" splits into name + version; every package
        // has a purl and a DEPENDS_ON edge, but licenses are all NOASSERTION and packages carry no checksums.
        var r = EvaluateSample("sbom-tool-4.1.13_express-5.3.0.spdx.json", SbomFormat.SPDX_2_2, QualityProfile.Cisa2026);

        r.ComponentCount.Should().Be(370);
        NotPassing(r).Should().BeEquivalentTo(["supplier", "hash", "license"]);
        Check(r, "tool-version").Status.Should().Be(CheckStatus.Pass);
        Check(r, "license").Status.Should().Be(CheckStatus.Fail);
        Check(r, "license").DeclaredUnknown.Should().Be(370);
        Check(r, "supplier").Satisfied.Should().Be(309);
        r.Passed.Should().Be(11);
    }

    // ─── Real generator output: spdx-maven-plugin + Parlay, and its SPDX 3.0.1 conversion ───

    [LocalSampleFact("examplemaven-0.0.1-enriched.spdx.json")]
    public void Sample_MavenPluginSpdx23_MatchesAHandReading()
    {
        var r = EvaluateSample("examplemaven-0.0.1-enriched.spdx.json", SbomFormat.SPDX_2_3, QualityProfile.Cisa2026);

        Check(r, "author").Status.Should().Be(CheckStatus.Pass);            // Person: Gary O'Neall
        Check(r, "tool-name").Status.Should().Be(CheckStatus.Pass);
        Check(r, "tool-version").Status.Should().Be(CheckStatus.Fail);      // "Tool: spdx-maven-plugin" carries no version
        Check(r, "identifier").Status.Should().Be(CheckStatus.Fail);        // no externalRefs anywhere
        Check(r, "supplier").Satisfied.Should().Be(2);                      // examplemaven supplier, JUnit originator
        Check(r, "version").Satisfied.Should().Be(2);                       // the four log4j/slf4j packages have none
        Check(r, "hash").Satisfied.Should().Be(1);                          // only the examplemaven jar
        Check(r, "license").Satisfied.Should().Be(2);                       // JUnit: concluded NOASSERTION, declared CPL-1.0
        Check(r, "license").DeclaredUnknown.Should().Be(4);
        Check(r, "coverage").Status.Should().Be(CheckStatus.Pass);
        r.Passed.Should().Be(8);
        r.Applicable.Should().Be(14);
    }

    [LocalSampleTheory("examplemaven-0.0.1-enriched.spdx.json", "examplemaven-0.0.1-enriched.spdx3.json")]
    [InlineData("supplier")]
    [InlineData("version")]
    [InlineData("hash")]
    [InlineData("identifier")]
    [InlineData("license")]
    [InlineData("name")]
    [InlineData("coverage")]
    public void Sample_MavenPlugin_SameDocumentScoresTheSameInSpdx2AndSpdx3(string checkId)
    {
        // The SPDX 3.0.1 file is a conversion of the 2.3 one. Component data must not change with the
        // format; the hash case is the one that needs hasDistributionArtifact followed.
        var v2 = Check(EvaluateSample("examplemaven-0.0.1-enriched.spdx.json", SbomFormat.SPDX_2_3, QualityProfile.Cisa2026), checkId);
        var v3 = Check(EvaluateSample("examplemaven-0.0.1-enriched.spdx3.json", SbomFormat.SPDX_3_0, QualityProfile.Cisa2026), checkId);

        (v3.Status, v3.Satisfied, v3.Total, v3.DeclaredUnknown)
            .Should().Be((v2.Status, v2.Satisfied, v2.Total, v2.DeclaredUnknown));
    }

    [LocalSampleFact("examplemaven-0.0.1-enriched.spdx3.json")]
    public void Sample_MavenPluginSpdx3_DocumentLevelDifferencesAreTheFormatsNotTheData()
    {
        var r = EvaluateSample("examplemaven-0.0.1-enriched.spdx3.json", SbomFormat.SPDX_3_0, QualityProfile.Cisa2026);

        Check(r, "sbom-version").Status.Should().Be(CheckStatus.NotExpressible);
        Check(r, "generation-context").Status.Should().Be(CheckStatus.Fail);  // expressible, but no software_Sbom element
        Check(r, "hash").Satisfied.Should().Be(1);                            // via hasDistributionArtifact, not the PackageVerificationCode
    }
}
