namespace SBOMViewer.Blazor.Models;

/// <summary>
/// Metadata for one previously uploaded SBOM held in IndexedDB. The file content is fetched
/// separately by <c>Id</c> so listing recents stays cheap.
/// </summary>
public record RecentSbom(long Id, string Name, string Format, long Size, long SavedAt)
{
    public DateTimeOffset SavedAtUtc => DateTimeOffset.FromUnixTimeMilliseconds(SavedAt);

    /// <summary>Compact "how long ago" label, e.g. "just now", "12 min ago", "3 days ago".</summary>
    public string SavedAgo(DateTimeOffset now)
    {
        var elapsed = now - SavedAtUtc;

        if (elapsed < TimeSpan.Zero)        return "just now";
        if (elapsed.TotalMinutes < 1)       return "just now";
        if (elapsed.TotalMinutes < 60)      return $"{(int)elapsed.TotalMinutes} min ago";
        if (elapsed.TotalHours   < 24)      return $"{(int)elapsed.TotalHours} hr ago";
        if (elapsed.TotalDays    < 2)       return "yesterday";
        return $"{(int)elapsed.TotalDays} days ago";
    }

    public string SizeLabel => Size switch
    {
        < 1024                => $"{Size} B",
        < 1024 * 1024         => $"{Size / 1024.0:0.#} KB",
        _                     => $"{Size / (1024.0 * 1024.0):0.#} MB"
    };
}
