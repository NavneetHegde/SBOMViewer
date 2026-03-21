namespace SBOMViewer.Blazor.Models;

public record PackageInfo(string Name, string Version, string? Ecosystem, string? Purl);
