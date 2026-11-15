using System.Text.Json;
using System.Text.RegularExpressions;
using SBOMViewer.Blazor.Models;

namespace SBOMViewer.Blazor.Services;

public static partial class PackageExtractor
{
    [GeneratedRegex(@"^pkg:(?<ecosystem>[^/]+)/(?<name>[^@]+)@(?<version>.+)$")]
    private static partial Regex PurlRegex();

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
    /// Public because the Components tab has to compute the same key to join scan results back
    /// onto its rows.
    /// </summary>
    public static string OsvName(string name, string? purl, string? group = null)
    {
        var ecosystem = ParseEcosystemFromPurl(purl);
        var (ns, purlName) = ParsePurlName(purl);

        if (ns is null || purlName is null)
            return QualifyName(name, group, ecosystem);

        return ecosystem switch
        {
            "Maven" => $"{ns}:{purlName}",
            "npm" or "Go" or "Packagist" => $"{ns}/{purlName}",
            _ => name
        };
    }

    /// <summary>Splits the purl's name part into (namespace, name); both null when it has no namespace.</summary>
    private static (string? Namespace, string? Name) ParsePurlName(string? purl)
    {
        if (string.IsNullOrEmpty(purl)) return (null, null);

        var match = PurlRegex().Match(purl);
        if (!match.Success) return (null, null);

        var full = match.Groups["name"].Value.Trim('/');
        var slash = full.LastIndexOf('/');
        if (slash <= 0 || slash == full.Length - 1) return (null, null);

        // Segments are percent-encoded individually — a scoped npm package is "%40scope/name".
        var ns = string.Join('/', full[..slash].Split('/').Select(Uri.UnescapeDataString));
        return (ns, Uri.UnescapeDataString(full[(slash + 1)..]));
    }

    private static string QualifyName(string name, string? group, string? ecosystem)
    {
        if (string.IsNullOrEmpty(group)) return name;

        return ecosystem switch
        {
            "Maven" when !name.Contains(':') => $"{group}:{name}",
            // An npm group can only be a scope; some generators drop the leading "@".
            "npm" when !name.StartsWith('@') => $"{(group.StartsWith('@') ? group : "@" + group)}/{name}",
            _ => name
        };
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

    private static string? ExtractPurlFromExternalRefs(JsonElement package)
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

    private static string? ParseEcosystemFromPurl(string? purl)
    {
        if (string.IsNullOrEmpty(purl))
            return null;

        var match = PurlRegex().Match(purl);
        if (!match.Success)
            return null;

        var ecosystem = match.Groups["ecosystem"].Value;

        return ecosystem.ToLowerInvariant() switch
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
            _ => ecosystem
        };
    }
}
