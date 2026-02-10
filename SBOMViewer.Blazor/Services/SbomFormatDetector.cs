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

    /// <summary>
    /// Lightweight validation: checks required fields are present for the detected format.
    /// Returns null if valid, or an error message string if invalid.
    /// </summary>
    public static string? Validate(JsonElement root, SbomFormat format)
    {
        return format switch
        {
            SbomFormat.CycloneDX_1_6 or SbomFormat.CycloneDX_1_7 => ValidateCycloneDX(root),
            SbomFormat.SPDX_2_2 => ValidateSpdx(root),
            _ => null
        };
    }

    private static string? ValidateCycloneDX(JsonElement root)
    {
        var missing = new List<string>();

        if (!root.TryGetProperty("bomFormat", out _))
            missing.Add("bomFormat");
        if (!root.TryGetProperty("specVersion", out _))
            missing.Add("specVersion");
        if (!root.TryGetProperty("metadata", out var metadata) || metadata.ValueKind != JsonValueKind.Object)
            missing.Add("metadata");
        if (!root.TryGetProperty("components", out var components) || components.ValueKind != JsonValueKind.Array)
            missing.Add("components");

        return missing.Count > 0
            ? $"Invalid CycloneDX: missing required fields: {string.Join(", ", missing)}"
            : null;
    }

    private static string? ValidateSpdx(JsonElement root)
    {
        var missing = new List<string>();

        if (!root.TryGetProperty("spdxVersion", out _))
            missing.Add("spdxVersion");
        if (!root.TryGetProperty("name", out _))
            missing.Add("name");
        if (!root.TryGetProperty("SPDXID", out _))
            missing.Add("SPDXID");
        if (!root.TryGetProperty("dataLicense", out _))
            missing.Add("dataLicense");
        if (!root.TryGetProperty("documentNamespace", out _))
            missing.Add("documentNamespace");
        if (!root.TryGetProperty("creationInfo", out var ci) || ci.ValueKind != JsonValueKind.Object)
            missing.Add("creationInfo");

        return missing.Count > 0
            ? $"Invalid SPDX: missing required fields: {string.Join(", ", missing)}"
            : null;
    }

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
