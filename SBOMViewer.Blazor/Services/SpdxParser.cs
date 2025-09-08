using SBOMViewer.Blazor.Models.Spdx;
using System.Reflection.Metadata;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SBOMViewer.Blazor.Services;

public static class SpdxParser
{
    public static async Task<SpdxDocument?> ParseSpdxJsonAsync(string fileContent)
    {
        if (string.IsNullOrWhiteSpace(fileContent))
        {
            Console.WriteLine("Input JSON content is null or empty.");
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

            var document = Task.Run(() => JsonSerializer.Deserialize<SpdxDocument>(fileContent, options)).Result;
            if (document == null || string.IsNullOrWhiteSpace(document.Name) || document.CreationInfo == null)
                return null;

            return document;

        }
        catch (Exception)
        {
            return null;
        }

    }
}
