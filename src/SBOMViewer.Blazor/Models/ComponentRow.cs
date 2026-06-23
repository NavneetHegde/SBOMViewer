namespace SBOMViewer.Blazor.Models;

public record ComponentRow(string Name, string Version, string Type, string License, string? Purl, LicenseRisk Risk);
