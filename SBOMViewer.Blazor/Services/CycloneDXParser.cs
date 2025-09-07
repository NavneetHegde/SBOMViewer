using SBOMViewer.Blazor.Models.CycloneDX;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SBOMViewer.Blazor.Services;

public class CycloneDXParser
{
    public static async Task<CycloneDXDocument?> ParseCycloneDXBomJsonAsyncParseJson(string fileContent)
    {
        if (string.IsNullOrWhiteSpace(fileContent))
        {
            return null;
        }

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            var document = Task.Run(() => JsonSerializer.Deserialize<CycloneDXDocument>(fileContent, options)).Result;
            if (document == null || string.IsNullOrWhiteSpace(document.BomFormat) || document.Metadata == null)
                return null;

            return document;

        }
        catch (Exception)
        {
            return null;
        }
    }
}
