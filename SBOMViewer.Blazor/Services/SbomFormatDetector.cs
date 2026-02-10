using System.Text.Json;
using SBOMViewer.Blazor.Models;

namespace SBOMViewer.Blazor.Services;

public record DetectionResult(SbomFormat? Format, string? DetectedVersion, bool IsUnsupportedVersion);

public static class SbomFormatDetector
{
    public static readonly string[] SupportedVersions = ["CycloneDX 1.6", "CycloneDX 1.7", "SPDX 2.2"];

    private static readonly HashSet<string> SupportedCycloneDXVersions = ["1.6", "1.7"];
    private static readonly HashSet<string> SupportedSpdxVersions = ["SPDX-2.2"];

    public static SbomFormat? Detect(string jsonContent) => DetectWithDetails(jsonContent).Format;

    public static DetectionResult DetectWithDetails(string jsonContent)
    {
        if (string.IsNullOrWhiteSpace(jsonContent))
            return new DetectionResult(null, null, false);

        try
        {
            using var doc = JsonDocument.Parse(jsonContent, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

            var root = doc.RootElement;

            if (root.TryGetProperty("bomFormat", out var bomFormat) &&
                bomFormat.GetString()?.Equals("CycloneDX", StringComparison.OrdinalIgnoreCase) == true)
            {
                var version = root.TryGetProperty("specVersion", out var specVersion)
                    ? specVersion.GetString()
                    : null;

                if (version != null && !SupportedCycloneDXVersions.Contains(version))
                    return new DetectionResult(null, $"CycloneDX {version}", true);

                return version == "1.7"
                    ? new DetectionResult(SbomFormat.CycloneDX_1_7, "CycloneDX 1.7", false)
                    : new DetectionResult(SbomFormat.CycloneDX_1_6, "CycloneDX 1.6", false);
            }

            if (root.TryGetProperty("spdxVersion", out var spdxVersion))
            {
                var version = spdxVersion.GetString();

                if (version != null && !SupportedSpdxVersions.Contains(version))
                    return new DetectionResult(null, version, true);

                return new DetectionResult(SbomFormat.SPDX_2_2, "SPDX 2.2", false);
            }

            return new DetectionResult(null, null, false);
        }
        catch
        {
            return new DetectionResult(null, null, false);
        }
    }
}
