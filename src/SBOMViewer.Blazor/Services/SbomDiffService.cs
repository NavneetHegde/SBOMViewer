using System.Text.Json;
using SBOMViewer.Blazor.Models;

namespace SBOMViewer.Blazor.Services;

/// <summary>
/// Compares two SBOMs at the component level.
///
/// The comparison runs over <see cref="ComponentRow"/> values produced by
/// <see cref="ComponentRowExtractor"/> rather than over raw JSON, so a baseline in one
/// format can be compared against a current document in another (e.g. SPDX 2.2 vs CycloneDX 1.6).
/// </summary>
public static class SbomDiffService
{
    public static SbomDiff Compare(
        JsonElement baselineRoot, SbomFormat? baselineFormat,
        JsonElement currentRoot,  SbomFormat? currentFormat)
        => Compare(
            ComponentRowExtractor.Extract(baselineRoot, baselineFormat),
            ComponentRowExtractor.Extract(currentRoot,  currentFormat));

    public static SbomDiff Compare(List<ComponentRow> baseline, List<ComponentRow> current)
    {
        var baselineEntries = baseline.Select(r => new Entry(r)).ToList();
        var currentEntries  = current.Select(r => new Entry(r)).ToList();

        var added          = new List<ComponentChange>();
        var removed        = new List<ComponentChange>();
        var versionChanged = new List<ComponentChange>();
        var licenseChanged = new List<ComponentChange>();
        var unchanged      = 0;

        var pairs = new List<(Entry Baseline, Entry Current)>();

        // Pass 1: match on version-stripped purl — the strongest identity signal.
        MatchBy(baselineEntries, currentEntries, e => PurlIdentity(e.Row.Purl), pairs);

        // Pass 2: match whatever is left by name. This is what lets a document carrying purls
        // be compared against one that does not (common when comparing across formats).
        MatchBy(baselineEntries, currentEntries, e => NameIdentity(e.Row.Name), pairs);

        foreach (var (b, c) in pairs)
        {
            var key  = PurlIdentity(c.Row.Purl) ?? PurlIdentity(b.Row.Purl) ?? NameIdentity(c.Row.Name)!;
            var name = string.IsNullOrEmpty(c.Row.Name) ? b.Row.Name : c.Row.Name;

            var versionDiffers = !string.Equals(b.Row.Version, c.Row.Version, StringComparison.Ordinal);
            var licenseDiffers = !string.Equals(b.Row.License, c.Row.License, StringComparison.OrdinalIgnoreCase)
                                 || b.Row.Risk != c.Row.Risk;

            // A component can change both version and licence; it is reported under both.
            if (versionDiffers)
                versionChanged.Add(new ComponentChange(key, name, b.Row, c.Row, DiffChangeKind.VersionChanged));
            if (licenseDiffers)
                licenseChanged.Add(new ComponentChange(key, name, b.Row, c.Row, DiffChangeKind.LicenseChanged));
            if (!versionDiffers && !licenseDiffers)
                unchanged++;
        }

        foreach (var e in baselineEntries.Where(e => !e.Matched))
            removed.Add(new ComponentChange(
                PurlIdentity(e.Row.Purl) ?? NameIdentity(e.Row.Name)!, e.Row.Name, e.Row, null, DiffChangeKind.Removed));

        foreach (var e in currentEntries.Where(e => !e.Matched))
            added.Add(new ComponentChange(
                PurlIdentity(e.Row.Purl) ?? NameIdentity(e.Row.Name)!, e.Row.Name, null, e.Row, DiffChangeKind.Added));

        return new SbomDiff(
            Sort(added), Sort(removed), Sort(versionChanged), Sort(licenseChanged),
            unchanged, baseline.Count, current.Count);
    }

    /// <summary>
    /// Pairs unmatched baseline and current entries sharing an identity. Entries whose identity
    /// selector returns null are skipped. Where a document lists the same identity more than once
    /// (e.g. two versions of one package) the occurrences are paired in document order and any
    /// surplus falls through to the added/removed buckets.
    /// </summary>
    private static void MatchBy(
        List<Entry> baseline, List<Entry> current,
        Func<Entry, string?> identity,
        List<(Entry, Entry)> pairs)
    {
        var currentByIdentity = current
            .Where(e => !e.Matched && identity(e) is not null)
            .GroupBy(identity!)
            .ToDictionary(g => g.Key!, g => new Queue<Entry>(g), StringComparer.Ordinal);

        foreach (var b in baseline.Where(e => !e.Matched))
        {
            var id = identity(b);
            if (id is null || !currentByIdentity.TryGetValue(id, out var queue)) continue;

            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                if (c.Matched) continue;
                b.Matched = true;
                c.Matched = true;
                pairs.Add((b, c));
                break;
            }
        }
    }

    /// <summary>
    /// Reduces a purl to a version-independent identity: qualifiers, subpath and the version
    /// segment are stripped. Without this every version bump would surface as an add plus a
    /// removal rather than as a version change.
    /// </summary>
    internal static string? PurlIdentity(string? purl)
    {
        if (string.IsNullOrWhiteSpace(purl)) return null;

        var value = purl.Trim();

        var hash = value.IndexOf('#');
        if (hash >= 0) value = value[..hash];

        var query = value.IndexOf('?');
        if (query >= 0) value = value[..query];

        // Scoped npm names encode '@' as %40, so the last '@' is the version separator.
        var at = value.LastIndexOf('@');
        if (at > 0) value = value[..at];

        value = value.Trim();
        return value.Length == 0 ? null : value.ToLowerInvariant();
    }

    private static string? NameIdentity(string? name)
        => string.IsNullOrWhiteSpace(name) ? null : "name:" + name.Trim().ToLowerInvariant();

    private static List<ComponentChange> Sort(List<ComponentChange> changes)
        => changes.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();

    private sealed class Entry(ComponentRow row)
    {
        public ComponentRow Row { get; } = row;
        public bool Matched { get; set; }
    }
}
