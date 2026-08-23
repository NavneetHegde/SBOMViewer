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
            await recentStore.SaveAsync(result.FileName!, result.Format, content);

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
}
