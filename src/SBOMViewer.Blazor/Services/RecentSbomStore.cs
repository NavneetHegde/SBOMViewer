using Microsoft.JSInterop;
using SBOMViewer.Blazor.Models;

namespace SBOMViewer.Blazor.Services;

/// <summary>
/// Keeps the most recently uploaded SBOMs in IndexedDB so they can be re-opened on a later visit
/// without locating the file again. Storage is local to the browser — nothing is transmitted.
///
/// Every call is best-effort: IndexedDB is unavailable in some private-browsing modes and can be
/// disabled outright, and quota errors are possible with large files. A persistence failure must
/// never break an upload, so failures degrade to "no recents" rather than surfacing an error.
/// </summary>
public class RecentSbomStore(IJSRuntime js)
{
    /// <summary>Mirrors MAX_ENTRIES in wwwroot/js/sbom-recent.js.</summary>
    public const int MaxEntries = 2;

    public async Task<IReadOnlyList<RecentSbom>> ListAsync()
    {
        try
        {
            return await js.InvokeAsync<RecentSbom[]>("sbomRecentList") ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<string?> GetContentAsync(long id)
    {
        try
        {
            return await js.InvokeAsync<string?>("sbomRecentGet", id);
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveAsync(string name, SbomFormat? format, string content)
    {
        try
        {
            await js.InvokeVoidAsync("sbomRecentSave", name, SbomFormatLabel.For(format), content);
        }
        catch
        {
            // Best-effort: an upload must still succeed when storage is unavailable or full.
        }
    }

    public async Task ClearAsync()
    {
        try
        {
            await js.InvokeVoidAsync("sbomRecentClear");
        }
        catch
        {
            // Nothing to surface — the list simply stays as it was.
        }
    }
}
