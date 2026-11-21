using System.Text.Json;
using FluentAssertions;
using SBOMViewer.Blazor.Models;
using SBOMViewer.Blazor.Services;

namespace SBOMViewer.Blazor.Tests.Services;

public class QualityReportExporterTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 21, 18, 30, 0, TimeSpan.Zero);

    private static QualityReport Report(QualityProfile profile = QualityProfile.Cisa2026) =>
        SbomQualityService.Evaluate(new QualityFacts
        {
            Author = FactState.Present,
            Timestamp = FactState.Present,
            DependencyRelationships = FactState.Present,
            Components =
            [
                new ComponentFacts { Label = "@scope/pkg@1.0.0", Name = FactState.Present, Version = FactState.Present, Supplier = FactState.DeclaredUnknown },
                new ComponentFacts { Label = "lodash@4.17.21", Name = FactState.Present, Version = FactState.Present, Supplier = FactState.Present },
                new ComponentFacts { Label = "left-pad@1.3.0", Name = FactState.Present, Version = FactState.Present },
            ]
        }, profile);

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonElement CheckById(JsonElement root, string id) =>
        root.GetProperty("checks").EnumerateArray().Single(c => c.GetProperty("id").GetString() == id);

    [Fact]
    public void ToJson_CarriesSummaryProfileSourceAndDisclaimer()
    {
        var report = Report();
        var root = Parse(QualityReportExporter.ToJson(report, At, "acme.cdx.json", "CycloneDX 1.6", "3.3.0"));

        root.GetProperty("schemaVersion").GetString().Should().Be("1");
        root.GetProperty("generatedAt").GetString().Should().Be("2026-11-21T18:30:00Z");
        root.GetProperty("generator").GetProperty("version").GetString().Should().Be("3.3.0");
        root.GetProperty("beta").GetBoolean().Should().BeTrue();
        root.GetProperty("disclaimer").GetString().Should().Contain("not a compliance determination");
        root.GetProperty("source").GetProperty("fileName").GetString().Should().Be("acme.cdx.json");
        root.GetProperty("source").GetProperty("format").GetString().Should().Be("CycloneDX 1.6");
        root.GetProperty("profile").GetProperty("id").GetString().Should().Be("cisa-2026");

        var summary = root.GetProperty("summary");
        summary.GetProperty("passed").GetInt32().Should().Be(report.Passed);
        summary.GetProperty("applicable").GetInt32().Should().Be(report.Applicable);
        summary.GetProperty("componentCount").GetInt32().Should().Be(3);
        summary.GetProperty("componentsWithGap").GetInt32().Should().Be(report.ComponentsWithGap);
        root.GetProperty("checks").GetArrayLength().Should().Be(16);
    }

    [Fact]
    public void ToJson_ComponentCheck_HasCountsAndTheFullAffectedList()
    {
        var root = Parse(QualityReportExporter.ToJson(Report(), At));
        var supplier = CheckById(root, "supplier");

        supplier.GetProperty("scope").GetString().Should().Be("component");
        supplier.GetProperty("status").GetString().Should().Be("partial");
        supplier.GetProperty("total").GetInt32().Should().Be(3);
        supplier.GetProperty("satisfied").GetInt32().Should().Be(1);
        supplier.GetProperty("declaredUnknown").GetInt32().Should().Be(1);
        supplier.GetProperty("missing").GetInt32().Should().Be(1);
        supplier.GetProperty("reference").GetString().Should().StartWith("CISA 2026 ·");
        supplier.GetProperty("fixHint").GetString().Should().NotBeNullOrWhiteSpace();
        supplier.GetProperty("affectedComponents").EnumerateArray().Select(e => e.GetString())
            .Should().BeEquivalentTo(["@scope/pkg@1.0.0", "left-pad@1.3.0"]);
    }

    [Fact]
    public void ToJson_DocumentCheck_HasStateNotCounts()
    {
        var root = Parse(QualityReportExporter.ToJson(Report(), At));

        var author = CheckById(root, "author");
        author.GetProperty("status").GetString().Should().Be("pass");
        author.GetProperty("state").GetString().Should().Be("present");
        author.TryGetProperty("total", out _).Should().BeFalse();

        CheckById(root, "signature").GetProperty("state").GetString().Should().Be("missing");
    }

    [Fact]
    public void ToJson_NotExpressibleCheck_HasNeitherStateNorCounts()
    {
        var report = SbomQualityService.Evaluate(
            new QualityFacts { AuthorSignature = FactState.NotExpressible }, QualityProfile.Cisa2026);
        var sig = CheckById(Parse(QualityReportExporter.ToJson(report, At)), "signature");

        sig.GetProperty("status").GetString().Should().Be("notExpressible");
        sig.TryGetProperty("state", out _).Should().BeFalse();
        sig.TryGetProperty("total", out _).Should().BeFalse();
    }

    [Fact]
    public void ToJson_AffectedListIsNotCappedLikeTheTab()
    {
        var comps = Enumerable.Range(0, QualityTabCap + 50)
            .Select(i => new ComponentFacts { Label = $"pkg{i}", Name = FactState.Present }).ToList();
        var report = SbomQualityService.Evaluate(new QualityFacts { Components = comps }, QualityProfile.Cisa2026);

        CheckById(Parse(QualityReportExporter.ToJson(report, At)), "hash")
            .GetProperty("affectedComponents").GetArrayLength().Should().Be(QualityTabCap + 50);
    }

    private const int QualityTabCap = 200; // SBOMViewer.Blazor.Components.QualityTab.AffectedCap

    [Fact]
    public void ToJson_WritesNamesAsWritten_AndNullsForMissingContext()
    {
        var json = QualityReportExporter.ToJson(Report(), At);

        json.Should().Contain("\"@scope/pkg@1.0.0\"", "component names must not come out as \\u escapes");
        var root = Parse(json);
        root.GetProperty("source").GetProperty("fileName").ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("generator").GetProperty("version").GetString().Should().Be("unknown");
    }

    [Fact]
    public void ToJson_NtiaProfile()
    {
        var root = Parse(QualityReportExporter.ToJson(Report(QualityProfile.Ntia2021), At));

        root.GetProperty("profile").GetProperty("id").GetString().Should().Be("ntia-2021");
        root.GetProperty("profile").GetProperty("label").GetString().Should().Be("NTIA 2021");
        root.GetProperty("checks").GetArrayLength().Should().Be(7);
    }

    [Theory]
    [InlineData("acme-app.cdx.json", QualityProfile.Cisa2026, "acme-app.cdx.quality-cisa-2026.json")]
    [InlineData("bom.JSON", QualityProfile.Ntia2021, "bom.quality-ntia-2021.json")]
    [InlineData(null, QualityProfile.Cisa2026, "sbom.quality-cisa-2026.json")]
    [InlineData("   ", QualityProfile.Cisa2026, "sbom.quality-cisa-2026.json")]
    [InlineData("C:\\fakepath\\my:sbom.json", QualityProfile.Cisa2026, "my_sbom.quality-cisa-2026.json")]
    public void FileNameFor_KeepsTheStemAndNamesTheProfile(string? source, QualityProfile profile, string expected)
    {
        QualityReportExporter.FileNameFor(source, profile).Should().Be(expected);
    }
}
