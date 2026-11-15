using System.Text.Json;
using SBOMViewer.Blazor.Models;

namespace SBOMViewer.Blazor.Services;

public static class PackageExtractor
{
    public static List<PackageInfo> ExtractPackages(JsonElement root, SbomFormat format)
    {
        return format switch
        {
            SbomFormat.CycloneDX_1_5 or SbomFormat.CycloneDX_1_6 or SbomFormat.CycloneDX_1_7 => ExtractCycloneDx(root),
            SbomFormat.SPDX_2_2 or SbomFormat.SPDX_2_3 => ExtractSpdx(root),
            SbomFormat.SPDX_3_0 => ExtractSpdx3(root),
            _ => []
        };
    }

    private static List<PackageInfo> ExtractCycloneDx(JsonElement root)
    {
        var packages = new List<PackageInfo>();

        if (!root.TryGetProperty("components", out var components) ||
            components.ValueKind != JsonValueKind.Array)
            return packages;

        foreach (var comp in components.EnumerateArray())
        {
            var name = comp.TryGetProperty("name", out var n) ? n.GetString() : null;
            var version = comp.TryGetProperty("version", out var v) ? v.GetString() : null;
            var purl = comp.TryGetProperty("purl", out var p) ? p.GetString() : null;

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(version))
                continue;

            var ecosystem = ParseEcosystemFromPurl(purl);
            var group = comp.TryGetProperty("group", out var g) ? g.GetString() : null;
            packages.Add(new PackageInfo(OsvName(name, purl, group), version, ecosystem, purl));
        }

        return packages;
    }

    /// <summary>
    /// The name OSV indexes a package under. SBOMs usually carry only the short name
    /// (<c>log4j-core</c>), while OSV wants the namespace joined in: <c>group:artifact</c> for
    /// Maven, <c>@scope/name</c> for npm, <c>vendor/name</c> for Packagist and the full module
    /// path for Go. Querying with the bare name returns nothing — or, worse, another package's
    /// advisories.
    ///
    /// The purl is the source of truth because every format carries it. The CycloneDX
    /// <paramref name="group"/> field is only a fallback for a purl with no namespace.
    ///
    /// <see cref="ComponentRowExtractor"/> calls this with the same inputs and stores the result
    /// on the row, which is what lets the Components tab join scan results back onto its rows.
    /// </summary>
    public static string OsvName(string name, string? purl, string? group = null)
    {
        var parsed = ParsePurl(purl);
        if (parsed is null) return name;

        var (type, ns, purlName) = parsed.Value;
        var ecosystem = MapEcosystem(type);

        if (ns is null)
            return QualifyName(name, group, ecosystem);

        return ecosystem switch
        {
            "Maven" => $"{ns}:{purlName}",
            "npm" => $"{NpmScope(ns)}/{purlName}",
            "Go" or "Packagist" => $"{ns}/{purlName}",
            _ => name
        };
    }

    private static string QualifyName(string name, string? group, string? ecosystem)
    {
        if (string.IsNullOrEmpty(group)) return name;

        return ecosystem switch
        {
            "Maven" when !name.Contains(':') => $"{group}:{name}",
            "npm" when !name.StartsWith('@') => $"{NpmScope(group)}/{name}",
            _ => name
        };
    }

    /// <summary>An npm namespace can only be a scope; some generators drop the leading "@".</summary>
    private static string NpmScope(string scope) => scope.StartsWith('@') ? scope : "@" + scope;

    /// <summary>
    /// Splits <c>pkg:type/namespace/name@version?qualifiers#subpath</c> into its type, namespace
    /// and name. The namespace is null when the purl has none; the whole result is null when the
    /// string is not a purl.
    ///
    /// Hand-rolled rather than a regex because of scoped npm packages: the scope may be written
    /// <c>%40angular</c> or as a literal <c>@angular</c>, and only the <em>last</em> "@" — and
    /// only when it does not open a path segment — separates the version.
    /// </summary>
    private static (string Type, string? Namespace, string Name)? ParsePurl(string? purl)
    {
        if (string.IsNullOrEmpty(purl) || !purl.StartsWith("pkg:", StringComparison.OrdinalIgnoreCase))
            return null;

        var rest = purl.AsSpan(4).TrimStart('/');

        // Qualifiers and subpath come last and may themselves contain "@" or "/".
        var cut = rest.IndexOfAny('?', '#');
        if (cut >= 0) rest = rest[..cut];

        var typeEnd = rest.IndexOf('/');
        if (typeEnd <= 0) return null;

        var type = rest[..typeEnd].ToString();
        var path = rest[(typeEnd + 1)..].Trim('/');

        var at = path.LastIndexOf('@');
        if (at > 0 && path[at - 1] != '/') path = path[..at];
        if (path.IsEmpty) return null;

        // Segments are percent-encoded individually — a scoped npm package is "%40scope/name".
        var segments = path.ToString().Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.UnescapeDataString)
            .ToArray();
        if (segments.Length == 0) return null;

        var ns = segments.Length > 1 ? string.Join('/', segments[..^1]) : null;
        return (type, ns, segments[^1]);
    }

    private static List<PackageInfo> ExtractSpdx(JsonElement root)
    {
        var packages = new List<PackageInfo>();

        if (!root.TryGetProperty("packages", out var pkgs) ||
            pkgs.ValueKind != JsonValueKind.Array)
            return packages;

        foreach (var pkg in pkgs.EnumerateArray())
        {
            var name = pkg.TryGetProperty("name", out var n) ? n.GetString() : null;
            var version = pkg.TryGetProperty("versionInfo", out var v) ? v.GetString() : null;

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(version))
                continue;

            var purl = ExtractPurlFromExternalRefs(pkg);
            var ecosystem = ParseEcosystemFromPurl(purl);
            packages.Add(new PackageInfo(OsvName(name, purl), version, ecosystem, purl));
        }

        return packages;
    }

    private static List<PackageInfo> ExtractSpdx3(JsonElement root)
    {
        var packages = new List<PackageInfo>();

        if (!root.TryGetProperty("@graph", out var graph) || graph.ValueKind != JsonValueKind.Array)
            return packages;

        foreach (var element in graph.EnumerateArray())
        {
            if (!element.TryGetProperty("type", out var type) || type.GetString() != "software_Package")
                continue;

            var name = element.TryGetProperty("name", out var n) ? n.GetString() : null;
            var version = element.TryGetProperty("software_packageVersion", out var v) ? v.GetString() : null;

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(version))
                continue;

            var purl = element.TryGetProperty("software_packageUrl", out var p) ? p.GetString() : null;
            var ecosystem = ParseEcosystemFromPurl(purl);
            packages.Add(new PackageInfo(OsvName(name, purl), version, ecosystem, purl));
        }

        return packages;
    }

    /// <summary>
    /// The purl of an SPDX 2.x package: the first external reference whose type is "purl".
    /// Shared with <see cref="ComponentRowExtractor"/> so both pick the same reference — a package
    /// can list several PACKAGE-MANAGER references, and not all of them are purls.
    /// </summary>
    public static string? ExtractPurlFromExternalRefs(JsonElement package)
    {
        if (!package.TryGetProperty("externalRefs", out var refs) ||
            refs.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var extRef in refs.EnumerateArray())
        {
            if (extRef.TryGetProperty("referenceType", out var refType) &&
                refType.GetString() == "purl" &&
                extRef.TryGetProperty("referenceLocator", out var locator))
            {
                return locator.GetString();
            }
        }

        return null;
    }

    private static string? ParseEcosystemFromPurl(string? purl) =>
        ParsePurl(purl) is { } parsed ? MapEcosystem(parsed.Type) : null;

    /// <summary>Maps a purl type to the ecosystem name OSV uses.</summary>
    private static string MapEcosystem(string purlType) => purlType.ToLowerInvariant() switch
    {
        "npm" => "npm",
        "nuget" => "NuGet",
        "pypi" => "PyPI",
        "maven" => "Maven",
        "cargo" => "crates.io",
        "gem" => "RubyGems",
        "golang" => "Go",
        "packagist" or "composer" => "Packagist",
        "pub" => "Pub",
        "hex" => "Hex",
        "cocoapods" => "CocoaPods",
        "swift" => "SwiftURL",
        _ => purlType
    };
}
