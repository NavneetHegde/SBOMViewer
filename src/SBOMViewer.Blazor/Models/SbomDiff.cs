namespace SBOMViewer.Blazor.Models;

public enum DiffChangeKind { Added, Removed, VersionChanged, LicenseChanged }

/// <summary>
/// A single component-level difference. <see cref="Baseline"/> is null for additions,
/// <see cref="Current"/> is null for removals; both are populated for changes.
/// </summary>
public record ComponentChange(
    string Key,
    string Name,
    ComponentRow? Baseline,
    ComponentRow? Current,
    DiffChangeKind Kind);

public record SbomDiff(
    IReadOnlyList<ComponentChange> Added,
    IReadOnlyList<ComponentChange> Removed,
    IReadOnlyList<ComponentChange> VersionChanged,
    IReadOnlyList<ComponentChange> LicenseChanged,
    int UnchangedCount,
    int BaselineCount,
    int CurrentCount)
{
    public int TotalChanges => Added.Count + Removed.Count + VersionChanged.Count + LicenseChanged.Count;

    public bool IsIdentical => TotalChanges == 0;

    public static SbomDiff Empty { get; } = new([], [], [], [], 0, 0, 0);
}
