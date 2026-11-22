using System.Security.Cryptography;
using System.Text;
using Microsoft.JSInterop;
using SBOMViewer.Blazor.Models;

namespace SBOMViewer.Blazor.Services;

/// <summary>
/// Keeps the most recently uploaded SBOMs in IndexedDB so they can be re-opened on a later visit
/// without locating the file again. Storage is local to the browser — nothing is transmitted.
///
/// A file is identified by the SHA-256 of its content, not its name: SBOMs are very often all
/// called bom.json. The full content of the newest <see cref="MaxContent"/> files is kept; the
/// names of up to <see cref="MaxEntries"/> are remembered.
///
/// Every call is best-effort: IndexedDB is unavailable in some private-browsing modes and can be
/// disabled outright, and quota errors are possible with large files. A persistence failure must
/// never break an upload, so failures degrade to "no recents" rather than surfacing an error.
/// </summary>
public class RecentSbomStore(IJSRuntime js)
{
    /// <summary>Mirrors MAX_CONTENT in wwwroot/js/sbom-recent.js — files kept in full.</summary>
    public const int MaxContent = 5;

    /// <summary>Mirrors MAX_ENTRIES in wwwroot/js/sbom-recent.js — files remembered by name.</summary>
    public const int MaxEntries = 20;

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

    /// <summary>
    /// Saves an uploaded file and returns its entry id, or null when storage is unavailable. The same
    /// content saved again — even under another name — updates the existing entry.
    /// </summary>
    public async Task<long?> SaveAsync(string name, SbomFormat? format, string content, string? subject = null)
    {
        try
        {
            var hash = ContentHash(content);
            return await js.InvokeAsync<long?>(
                "sbomRecentSave", name, SbomFormatLabel.For(format), content, hash, subject);
        }
        catch
        {
            // Best-effort: an upload must still succeed when storage is unavailable or full.
            return null;
        }
    }

    /// <summary>
    /// Lower-case hex SHA-256 of the UTF-8 content. Computed here rather than with crypto.subtle in
    /// JS because .NET's managed SHA-256 also works outside a secure context (plain HTTP). Measured
    /// at ~770 ms for a 19 MB file in the published build — about 3% of that file's ~27 s load, and
    /// ~40 ms for a typical 1 MB SBOM. Returns null if hashing fails; the save then still succeeds
    /// (the JS side matches only hash-less legacy rows by name + size).
    /// </summary>
    public static string? ContentHash(string content)
    {
        try
        {
            return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
        }
        catch
        {
            return null;
        }
    }

    public async Task RemoveAsync(long id)
    {
        try
        {
            await js.InvokeVoidAsync("sbomRecentRemove", id);
        }
        catch
        {
            // Nothing to surface — the list simply stays as it was.
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
