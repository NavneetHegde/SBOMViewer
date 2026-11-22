using System.Text.Json;
using Microsoft.AspNetCore.Components.Forms;
using SBOMViewer.Blazor.Models;

namespace SBOMViewer.Blazor.Services;

public record SbomLoadResult(
    JsonDocument? Document,
    SchemaNode? Schema,
    SbomFormat? Format,
    string? FileName,
    string? Error)
{
    public bool Success => Error is null && Document is not null;

    public static SbomLoadResult Failed(string error) => new(null, null, null, null, error);
}

/// <summary>
/// Reads an uploaded file and runs the detect → validate → build-schema pipeline.
/// Shared by the single-document upload screen and the comparison screen so the two
/// cannot drift apart on size limits, format support or error wording.
/// </summary>
public class SbomLoader(SchemaService schemaService, RecentSbomStore? recentStore = null)
{
    public const long MaxFileSizeBytes = 20 * 1024 * 1024;

    public async Task<SbomLoadResult> LoadAsync(IBrowserFile file)
    {
        if (file.Size > MaxFileSizeBytes)
            return SbomLoadResult.Failed("File size exceeds the 20MB limit.");

        if (!file.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return SbomLoadResult.Failed("Only .json files are supported.");

        string content;
        using (var stream = file.OpenReadStream(MaxFileSizeBytes))
        using (var reader = new StreamReader(stream))
            content = await reader.ReadToEndAsync();

        var result = Load(content, file.Name);

        // Only genuine file uploads are remembered. The Load(string, string) overload is also used
        // to re-parse a document when handing it between the viewer and the compare page, and those
        // hand-offs must not churn the recent history.
        if (result.Success && recentStore is not null)
            await recentStore.SaveAsync(result.FileName!, result.Format, content,
                ExtractSubject(result.Document!.RootElement, result.Format));

        return result;
    }

    public SbomLoadResult Load(string content, string fileName)
    {
        var detection = SbomFormatDetector.DetectWithDetails(content);

        if (detection.IsUnsupportedVersion)
            return SbomLoadResult.Failed(
                $"Version \"{detection.DetectedVersion}\" is not supported. " +
                $"Supported versions: {string.Join(", ", SbomFormatDetector.SupportedVersions)}.");

        if (detection.Format is null)
            return SbomLoadResult.Failed("Unrecognized SBOM format. Please upload a valid CycloneDX or SPDX JSON file.");

        JsonDocument jsonDoc;
        try
        {
            jsonDoc = JsonDocument.Parse(content, new JsonDocumentOptions
            {
                CommentHandling   = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
        }
        catch (JsonException)
        {
            return SbomLoadResult.Failed("Failed to parse JSON. Please check the file is valid JSON.");
        }

        var validationError = SbomFormatDetector.Validate(jsonDoc.RootElement, detection.Format.Value);
        if (validationError is not null)
        {
            jsonDoc.Dispose();
            return SbomLoadResult.Failed(validationError);
        }

        return new SbomLoadResult(
            jsonDoc,
            schemaService.BuildFromJson(jsonDoc.RootElement),
            detection.Format,
            fileName,
            null);
    }

    /// <summary>Longest subject kept; SPDX 2.x document names are often generated paths.</summary>
    public const int MaxSubjectLength = 60;

    /// <summary>
    /// A short label for what the document describes — "storefront 2.4.1" — shown beside the file
    /// name in the recents list so two files both called bom.json can be told apart. Null when the
    /// document does not say.
    /// </summary>
    public static string? ExtractSubject(JsonElement root, SbomFormat? format)
    {
        string? subject = format switch
        {
            SbomFormat.CycloneDX_1_5 or SbomFormat.CycloneDX_1_6 or SbomFormat.CycloneDX_1_7
                => CycloneDxSubject(root),
            SbomFormat.SPDX_2_2 or SbomFormat.SPDX_2_3
                => StringProp(root, "name"),
            SbomFormat.SPDX_3_0
                => Spdx3Subject(root),
            _ => null
        };

        subject = subject?.Trim();
        if (string.IsNullOrEmpty(subject))
            return null;

        return subject.Length <= MaxSubjectLength
            ? subject
            : subject[..(MaxSubjectLength - 1)].TrimEnd() + "…";
    }

    private static string? CycloneDxSubject(JsonElement root)
    {
        if (!root.TryGetProperty("metadata", out var metadata) || metadata.ValueKind != JsonValueKind.Object ||
            !metadata.TryGetProperty("component", out var component) || component.ValueKind != JsonValueKind.Object)
            return null;

        var name = StringProp(component, "name")?.Trim();
        if (string.IsNullOrEmpty(name))
            return null;

        var version = StringProp(component, "version")?.Trim();
        return string.IsNullOrEmpty(version) ? name : $"{name} {version}";
    }

    private static string? Spdx3Subject(JsonElement root)
    {
        if (!root.TryGetProperty("@graph", out var graph) || graph.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var element in graph.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.Object &&
                StringProp(element, "type") == "SpdxDocument")
                return StringProp(element, "name");
        }

        return null;
    }

    private static string? StringProp(JsonElement obj, string name) =>
        obj.ValueKind == JsonValueKind.Object &&
        obj.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
