using System.Text.RegularExpressions;
using SBOMViewer.Blazor.Models;

namespace SBOMViewer.Blazor.Services;

public static partial class LicenseClassifier
{
    private static readonly Dictionary<string, LicenseRisk> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        // Permissive
        ["MIT"] = LicenseRisk.Permissive,
        ["MIT-0"] = LicenseRisk.Permissive,
        ["Apache-1.1"] = LicenseRisk.Permissive,
        ["Apache-2.0"] = LicenseRisk.Permissive,
        ["BSD-2-Clause"] = LicenseRisk.Permissive,
        ["BSD-3-Clause"] = LicenseRisk.Permissive,
        ["BSD-3-Clause-Clear"] = LicenseRisk.Permissive,
        ["BSD-4-Clause"] = LicenseRisk.Permissive,
        ["0BSD"] = LicenseRisk.Permissive,
        ["ISC"] = LicenseRisk.Permissive,
        ["Unlicense"] = LicenseRisk.Permissive,
        ["PostgreSQL"] = LicenseRisk.Permissive,
        ["Python-2.0"] = LicenseRisk.Permissive,
        ["Zlib"] = LicenseRisk.Permissive,
        ["libpng-2.0"] = LicenseRisk.Permissive,
        ["X11"] = LicenseRisk.Permissive,
        ["WTFPL"] = LicenseRisk.Permissive,
        ["CC0-1.0"] = LicenseRisk.Permissive,
        ["BlueOak-1.0.0"] = LicenseRisk.Permissive,
        ["Artistic-2.0"] = LicenseRisk.Permissive,
        ["NCSA"] = LicenseRisk.Permissive,
        ["Boost-1.0"] = LicenseRisk.Permissive,
        ["OpenSSL"] = LicenseRisk.Permissive,
        ["Vim"] = LicenseRisk.Permissive,
        ["CC-BY-3.0"] = LicenseRisk.Permissive,
        ["CC-BY-4.0"] = LicenseRisk.Permissive,

        // Weak copyleft
        ["LGPL-2.0"] = LicenseRisk.WeakCopyleft,
        ["LGPL-2.1"] = LicenseRisk.WeakCopyleft,
        ["LGPL-3.0"] = LicenseRisk.WeakCopyleft,
        ["MPL-1.0"] = LicenseRisk.WeakCopyleft,
        ["MPL-1.1"] = LicenseRisk.WeakCopyleft,
        ["MPL-2.0"] = LicenseRisk.WeakCopyleft,
        ["EPL-1.0"] = LicenseRisk.WeakCopyleft,
        ["EPL-2.0"] = LicenseRisk.WeakCopyleft,
        ["CDDL-1.0"] = LicenseRisk.WeakCopyleft,
        ["CDDL-1.1"] = LicenseRisk.WeakCopyleft,
        ["CPL-1.0"] = LicenseRisk.WeakCopyleft,

        // Strong copyleft
        ["GPL-1.0"] = LicenseRisk.StrongCopyleft,
        ["GPL-2.0"] = LicenseRisk.StrongCopyleft,
        ["GPL-3.0"] = LicenseRisk.StrongCopyleft,
        ["AGPL-1.0"] = LicenseRisk.StrongCopyleft,
        ["AGPL-3.0"] = LicenseRisk.StrongCopyleft,
        ["SSPL-1.0"] = LicenseRisk.StrongCopyleft,
        ["OSL-3.0"] = LicenseRisk.StrongCopyleft,
        ["EUPL-1.1"] = LicenseRisk.StrongCopyleft,
        ["EUPL-1.2"] = LicenseRisk.StrongCopyleft,
    };

    public static LicenseRisk Classify(string? license)
    {
        if (string.IsNullOrWhiteSpace(license)) return LicenseRisk.Unknown;

        var normalized = Normalize(license);
        return Map.TryGetValue(normalized, out var risk) ? risk : LicenseRisk.Unknown;
    }

    private static string Normalize(string license)
    {
        var s = license.Trim();
        if (s.EndsWith('+')) s = s[..^1];
        return SuffixRegex().Replace(s, "");
    }

    [GeneratedRegex(@"-(only|or-later)$", RegexOptions.IgnoreCase)]
    private static partial Regex SuffixRegex();
}
