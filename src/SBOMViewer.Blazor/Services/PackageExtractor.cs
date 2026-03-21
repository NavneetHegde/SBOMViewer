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
            SbomFormat.CycloneDX_1_6 or SbomFormat.CycloneDX_1_7 => ExtractCycloneDx(root),
            SbomFormat.SPDX_2_2 => ExtractSpdx(root),
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
            packages.Add(new PackageInfo(name, version, ecosystem, purl));
        }

        return packages;
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
            packages.Add(new PackageInfo(name, version, ecosystem, purl));
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
            "packagist" => "Packagist",
            "pub" => "Pub",
            "hex" => "Hex",
            "cocoapods" => "CocoaPods",
            "swift" => "SwiftURL",
            _ => ecosystem
        };
    }
}
