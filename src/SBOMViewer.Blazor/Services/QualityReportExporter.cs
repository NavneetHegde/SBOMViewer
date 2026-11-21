using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using SBOMViewer.Blazor.Models;

namespace SBOMViewer.Blazor.Services;

/// <summary>
/// Serialises a <see cref="QualityReport"/> to the JSON file behind the Quality tab's "Download JSON" button.
/// The file is built and saved in the browser; nothing is sent anywhere. Unlike the "Report a wrong result"
/// link, it therefore includes the file name and the full, uncapped affected-component lists — that is the
/// point of downloading it.
/// </summary>
public static class QualityReportExporter
{
    /// <summary>Bumped when a field is renamed or removed, so scripts reading the file can tell.</summary>
    public const string SchemaVersion = "1";

    public const string Disclaimer =
        "Readiness check, not a compliance determination or certification. It reports whether the fields each " +
        "element asks for are populated; it does not check that the values are correct. Beta: generators map " +
        "these fields differently.";

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        // Component names such as "@scope/pkg" or "a<b" should read as written, not as @ escapes.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string ToJson(
        QualityReport report,
        DateTimeOffset generatedAt,
        string? sourceFileName = null,
        string? sourceFormat = null,
        string? appVersion = null)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, WriterOptions))
        {
            w.WriteStartObject();
            w.WriteString("schemaVersion", SchemaVersion);
            w.WriteString("generatedAt", generatedAt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"));
            w.WriteStartObject("generator");
            w.WriteString("name", "SBOM Viewer");
            w.WriteString("version", string.IsNullOrWhiteSpace(appVersion) ? "unknown" : appVersion);
            w.WriteString("url", "https://sbomviewer.com");
            w.WriteEndObject();
            w.WriteBoolean("beta", true);
            w.WriteString("disclaimer", Disclaimer);

            w.WriteStartObject("source");
            WriteStringOrNull(w, "fileName", sourceFileName);
            WriteStringOrNull(w, "format", sourceFormat);
            w.WriteEndObject();

            w.WriteStartObject("profile");
            w.WriteString("id", ProfileId(report.Profile));
            w.WriteString("label", report.ProfileLabel);
            w.WriteString("document", ProfileDocument(report.Profile));
            w.WriteEndObject();

            w.WriteStartObject("summary");
            w.WriteNumber("passed", report.Passed);
            w.WriteNumber("applicable", report.Applicable);
            w.WriteNumber("notExpressible", report.Checks.Count(c => c.Status == CheckStatus.NotExpressible));
            w.WriteNumber("componentCount", report.ComponentCount);
            w.WriteNumber("componentsWithGap", report.ComponentsWithGap);
            if (report.AverageComponentCoverage is { } avg)
                w.WriteNumber("averageComponentCoveragePct", Math.Round(avg, 1));
            else
                w.WriteNull("averageComponentCoveragePct");
            w.WriteEndObject();

            w.WriteStartArray("checks");
            foreach (var c in report.Checks)
            {
                w.WriteStartObject();
                w.WriteString("id", c.Id);
                w.WriteString("title", c.Title);
                w.WriteString("reference", c.Reference);
                w.WriteString("scope", c.Scope == CheckScope.Component ? "component" : "document");
                w.WriteString("status", StatusId(c.Status));
                if (c.Scope == CheckScope.Component && c.Status != CheckStatus.NotExpressible)
                {
                    w.WriteNumber("total", c.Total);
                    w.WriteNumber("satisfied", c.Satisfied);
                    w.WriteNumber("affected", c.Affected);
                    w.WriteNumber("declaredUnknown", c.DeclaredUnknown);
                    w.WriteNumber("missing", c.Affected - c.DeclaredUnknown);
                    w.WriteNumber("coveragePct", Math.Round(c.CoveragePct, 1));
                }
                else if (c.Status != CheckStatus.NotExpressible)
                {
                    // Document checks: present / declaredUnknown / missing, matching what the tab shows.
                    w.WriteString("state", c.Status switch
                    {
                        CheckStatus.Pass => "present",
                        CheckStatus.Partial => "declaredUnknown",
                        _ => "missing",
                    });
                }
                WriteStringOrNull(w, "note", c.Note);
                w.WriteString("fixHint", c.FixHint);
                w.WriteStartArray("affectedComponents");
                foreach (var name in c.AffectedComponents) w.WriteStringValue(name);
                w.WriteEndArray();
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// <c>acme-app.cdx.json</c> → <c>acme-app.cdx.quality-cisa-2026.json</c>. The profile is in the name
    /// because the same SBOM downloaded under both profiles must not overwrite itself.
    /// </summary>
    public static string FileNameFor(string? sourceFileName, QualityProfile profile)
    {
        // Split on both separators by hand: in WebAssembly (and on Linux CI) Path treats '\' as an ordinary character.
        var stem = string.IsNullOrWhiteSpace(sourceFileName) ? "sbom" : sourceFileName.Trim().Split('/', '\\')[^1];
        if (stem.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) stem = stem[..^5];
        // A fixed Windows-safe set, not Path.GetInvalidFileNameChars(): that is platform-dependent, and the
        // file is saved on the user's OS, not the one this code runs on.
        var safe = new string(stem.Select(ch => char.IsControl(ch) || "<>:\"/\\|?*".Contains(ch) ? '_' : ch).ToArray());
        if (string.IsNullOrWhiteSpace(safe)) safe = "sbom";
        return $"{safe}.quality-{ProfileId(profile)}.json";
    }

    public static string ProfileId(QualityProfile p) => p switch
    {
        QualityProfile.Ntia2021 => "ntia-2021",
        _ => "cisa-2026",
    };

    private static string ProfileDocument(QualityProfile p) => p switch
    {
        QualityProfile.Ntia2021 => "NTIA, The Minimum Elements for a Software Bill of Materials (SBOM), July 2021",
        _ => "CISA, 2026 Minimum Elements for a Software Bill of Materials (SBOM), v2.1, July 2026",
    };

    private static string StatusId(CheckStatus s) => s switch
    {
        CheckStatus.Pass => "pass",
        CheckStatus.Partial => "partial",
        CheckStatus.Fail => "fail",
        _ => "notExpressible",
    };

    private static void WriteStringOrNull(Utf8JsonWriter w, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) w.WriteNull(name);
        else w.WriteString(name, value);
    }
}
