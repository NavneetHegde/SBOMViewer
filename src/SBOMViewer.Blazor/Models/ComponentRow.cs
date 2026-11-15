namespace SBOMViewer.Blazor.Models;

public record ComponentRow(string Name, string Version, string Type, string License, string? Purl, LicenseRisk Risk)
{
    /// <summary>
    /// The name OSV indexes this component under (<c>group:artifact</c>, <c>@scope/name</c>) —
    /// the key vulnerability scan results are stored by. <see cref="Name"/> is what is displayed;
    /// this is what is looked up. Defaults to the display name when no namespace applies.
    /// </summary>
    public string OsvName { get; init; } = Name;
}
