using System.Text.Json;
using SBOMViewer.Blazor.Models;

namespace SBOMViewer.Blazor.Services;

public static class ComponentRowExtractor
{
    public static List<ComponentRow> Extract(JsonElement root, SbomFormat? format)
    {
        return format switch
        {
            SbomFormat.CycloneDX_1_5 or SbomFormat.CycloneDX_1_6 or SbomFormat.CycloneDX_1_7 => ExtractCycloneDx(root),
            SbomFormat.SPDX_2_2 or SbomFormat.SPDX_2_3 => ExtractSpdx(root),
            SbomFormat.SPDX_3_0 => ExtractSpdx3(root),
            _ => []
        };
    }

    private static List<ComponentRow> ExtractCycloneDx(JsonElement root)
    {
        var rows = new List<ComponentRow>();

        if (!root.TryGetProperty("components", out var comps) ||
            comps.ValueKind != JsonValueKind.Array) return rows;

        foreach (var comp in comps.EnumerateArray())
        {
            var name    = comp.TryGetProperty("name",    out var n) ? n.GetString() ?? "" : "";
            var version = comp.TryGetProperty("version", out var v) ? v.GetString() ?? "" : "";
            var type    = comp.TryGetProperty("type",    out var t) ? t.GetString() ?? "" : "";
            var purl    = comp.TryGetProperty("purl",    out var p) ? p.GetString()      : null;
            var license = "";
            if (comp.TryGetProperty("licenses", out var lics) && lics.ValueKind == JsonValueKind.Array)
            {
                var first = lics.EnumerateArray().FirstOrDefault();
                if (first.ValueKind == JsonValueKind.Object)
                {
                    if (first.TryGetProperty("license", out var lic) && lic.TryGetProperty("id", out var lid))
                        license = lid.GetString() ?? "";
                    else if (first.TryGetProperty("license", out var licName) && licName.TryGetProperty("name", out var lname))
                        license = lname.GetString() ?? "";
                    else if (first.TryGetProperty("expression", out var expr))
                        license = expr.GetString() ?? "";
                }
            }
            if (!string.IsNullOrEmpty(name))
                rows.Add(new ComponentRow(name, version, type, license, purl, LicenseClassifier.Classify(license)));
        }

        return rows;
    }

    private static List<ComponentRow> ExtractSpdx(JsonElement root)
    {
        var rows = new List<ComponentRow>();

        if (!root.TryGetProperty("packages", out var pkgs) ||
            pkgs.ValueKind != JsonValueKind.Array) return rows;

        foreach (var pkg in pkgs.EnumerateArray())
        {
            var name    = pkg.TryGetProperty("name",             out var n) ? n.GetString() ?? "" : "";
            var version = pkg.TryGetProperty("versionInfo",      out var v) ? v.GetString() ?? "" : "";
            var license = pkg.TryGetProperty("licenseConcluded", out var l) ? l.GetString() ?? "" : "";
            if (license is "NOASSERTION" or "NONE") license = "";
            string? purl = null;
            if (pkg.TryGetProperty("externalRefs", out var refs) && refs.ValueKind == JsonValueKind.Array)
            {
                foreach (var r in refs.EnumerateArray())
                {
                    if (r.TryGetProperty("referenceCategory", out var cat) &&
                        r.TryGetProperty("referenceLocator",  out var loc) &&
                        cat.GetString() is "PACKAGE-MANAGER" or "PACKAGE_MANAGER")
                    { purl = loc.GetString(); break; }
                }
            }
            if (!string.IsNullOrEmpty(name))
                rows.Add(new ComponentRow(name, version, "package", license, purl, LicenseClassifier.Classify(license)));
        }

        return rows;
    }

    private static List<ComponentRow> ExtractSpdx3(JsonElement root)
    {
        var rows = new List<ComponentRow>();

        if (!root.TryGetProperty("@graph", out var graph) || graph.ValueKind != JsonValueKind.Array)
            return rows;

        foreach (var element in graph.EnumerateArray())
        {
            if (!element.TryGetProperty("type", out var type) || type.GetString() != "software_Package")
                continue;

            var name    = element.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            var version = element.TryGetProperty("software_packageVersion", out var v) ? v.GetString() ?? "" : "";
            var purl    = element.TryGetProperty("software_packageUrl", out var p) ? p.GetString() : null;

            var license = element.TryGetProperty("software_concludedLicenseExpression", out var lic) ? lic.GetString() ?? ""
                : element.TryGetProperty("software_declaredLicenseExpression", out var declLic) ? declLic.GetString() ?? ""
                : "";
            if (license is "NOASSERTION" or "NONE") license = "";

            if (!string.IsNullOrEmpty(name))
                rows.Add(new ComponentRow(name, version, "package", license, purl, LicenseClassifier.Classify(license)));
        }

        return rows;
    }
}
