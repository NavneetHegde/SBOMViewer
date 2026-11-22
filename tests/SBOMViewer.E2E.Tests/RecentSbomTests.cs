using Microsoft.Playwright;
using NUnit.Framework;

namespace SBOMViewer.E2E.Tests;

/// <summary>
/// Covers the IndexedDB-backed "recent files" shortcut: content-hash identity, the 5-file / 20-name
/// caps, per-entry remove and the landing / compare UI. Each test starts by clearing the store so
/// the shared browser profile cannot leak state between tests.
/// </summary>
[TestFixture]
public class RecentSbomTests : TestBase
{
    private static string SamplesDir =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../samples"));

    private static string Sample(string name) => Path.Combine(SamplesDir, name);

    private async Task ClearStoreAndReload()
    {
        await Page.EvaluateAsync("() => window.sbomRecentClear()");
        await Page.ReloadAsync();
        await Page.WaitForSelectorAsync(".nav", new() { Timeout = 30_000 });
    }

    private async Task UploadAndReturnHome(string file)
    {
        await Page.Locator("input[type='file']").SetInputFilesAsync(file);
        await Expect(Page.Locator(".dashboard")).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await Page.ReloadAsync();
        await Page.WaitForSelectorAsync(".dropzone", new() { Timeout = 30_000 });
    }

    private async Task UploadAndReturnHome(FilePayload file)
    {
        await Page.Locator("input[type='file']").SetInputFilesAsync(file);
        await Expect(Page.Locator(".dashboard")).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await Page.ReloadAsync();
        await Page.WaitForSelectorAsync(".dropzone", new() { Timeout = 30_000 });
    }

    // ── Generated files ──────────────────────────────────────────────────────────────────────
    // The cap tests need many distinct documents; generating them keeps 21 fixtures out of the repo.

    private static string Bom(string subject, string version = "1.0.0") => $$"""
        {
          "bomFormat": "CycloneDX", "specVersion": "1.6", "version": 1,
          "metadata": { "component": { "type": "application", "name": "{{subject}}", "version": "{{version}}" } },
          "components": [ { "type": "library", "name": "left-pad", "version": "1.3.0" } ]
        }
        """;

    private static FilePayload Payload(string name, string content) => new()
    {
        Name = name,
        MimeType = "application/json",
        Buffer = System.Text.Encoding.UTF8.GetBytes(content)
    };

    /// <summary>
    /// Saves generated files straight through the store, oldest first, then reloads — much faster
    /// than the UI for the cap tests. Subjects are "project-1" (oldest) … "project-N" (newest). The
    /// hash is SHA-256 via crypto.subtle, the same digest the app computes in .NET, so a later
    /// re-drop of the same content through the UI matches the seeded entry.
    /// </summary>
    private async Task Seed(int count, string name = "bom.json")
    {
        var files = Enumerable.Range(1, count)
            .Select(i => new { name, subject = $"project-{i} 1.0.0", content = Bom($"project-{i}") })
            .ToArray();

        await Page.EvaluateAsync("""
            async (files) => {
              for (const f of files) {
                const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(f.content));
                const hash = [...new Uint8Array(digest)].map(b => b.toString(16).padStart(2, '0')).join('');
                await window.sbomRecentSave(f.name, 'CycloneDX 1.6', f.content, hash, f.subject);
              }
            }
            """, files);

        await Page.ReloadAsync();
        await Page.WaitForSelectorAsync(".dropzone", new() { Timeout = 30_000 });
    }

    private record StoredEntry(long Id, string Name, string? Hash, string? Subject, bool HasContent);
    private record StoreSnapshot(StoredEntry[] Entries, long[] ContentIds);

    /// <summary>Reads both object stores directly — not through the UI.</summary>
    private async Task<StoreSnapshot> ReadStore()
    {
        var json = await Page.EvaluateAsync<string>("""
            () => new Promise((resolve, reject) => {
              const req = indexedDB.open('sbom-viewer');
              req.onerror = () => reject(req.error);
              req.onsuccess = () => {
                const db = req.result;
                const t = db.transaction(['entries', 'content'], 'readonly');
                const out = {};
                t.objectStore('entries').getAll().onsuccess = e => out.entries = e.target.result.map(x => ({
                  id: x.id, name: x.name, hash: x.hash || null, subject: x.subject || null,
                  hasContent: x.hasContent !== false }));
                t.objectStore('content').getAllKeys().onsuccess = e => out.contentIds = e.target.result;
                t.oncomplete = () => { db.close(); resolve(JSON.stringify(out)); };
              };
            })
            """);

        return System.Text.Json.JsonSerializer.Deserialize<StoreSnapshot>(json,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
    }

    // ── Basics ───────────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task Recent_NotShownBeforeAnyUpload()
    {
        await ClearStoreAndReload();
        await Expect(Page.Locator(".recent-bar")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Recent_UploadedFileSurvivesAReload()
    {
        await ClearStoreAndReload();
        await UploadAndReturnHome(Sample("cyclonedx-1.6-full.json"));

        await Expect(Page.Locator(".recent-bar")).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await Expect(Page.Locator(".recent-item-name")).ToContainTextAsync("cyclonedx-1.6-full.json");
    }

    [Test]
    public async Task Recent_StripIsVisibleWithoutScrollingOnALaptopScreen()
    {
        await ClearStoreAndReload();
        await UploadAndReturnHome(Sample("spdx-2.3-full.json"));
        await UploadAndReturnHome(Sample("cyclonedx-1.6-full.json"));

        // With two recents the row used to sit below the cards and fall under the fold.
        await Page.SetViewportSizeAsync(1366, 650);

        await Expect(Page.Locator(".recent-item")).ToHaveCountAsync(2, new() { Timeout = 15_000 });
        await Expect(Page.Locator(".recent-bar")).ToBeInViewportAsync(new() { Ratio = 1 });
        await Expect(Page.Locator(".recent-clear")).ToBeInViewportAsync(new() { Ratio = 1 });
        await Expect(Page.Locator(".sample-link")).ToBeInViewportAsync(new() { Ratio = 1 });
    }

    [Test]
    public async Task Recent_StripSitsAboveTheUploadCards()
    {
        await ClearStoreAndReload();
        await UploadAndReturnHome(Sample("cyclonedx-1.6-full.json"));

        await Expect(Page.Locator(".quick-row .recent-bar")).ToBeVisibleAsync(new() { Timeout = 15_000 });
        var strip = await Page.Locator(".recent-bar").BoundingBoxAsync();
        var cards = await Page.Locator(".upload-options").BoundingBoxAsync();

        Assert.That(strip!.Y + strip.Height, Is.LessThanOrEqualTo(cards!.Y),
            "recent files must come before the cards, not after them");
    }

    [Test]
    public async Task Recent_LandingPageFitsADesktopScreenWithoutScrolling()
    {
        await ClearStoreAndReload();
        await UploadAndReturnHome(Sample("spdx-2.3-full.json"));
        await UploadAndReturnHome(Sample("cyclonedx-1.6-full.json"));

        await Page.SetViewportSizeAsync(1400, 900);
        await Expect(Page.Locator(".recent-item")).ToHaveCountAsync(2, new() { Timeout = 15_000 });

        // Hero, sample button, both recents and both cards together — nothing left to scroll to.
        var overflow = await Page.EvaluateAsync<int>(
            "() => { const e = document.querySelector('.upload-screen'); return e.scrollHeight - e.clientHeight; }");
        Assert.That(overflow, Is.LessThanOrEqualTo(0));
    }

    [Test]
    public async Task Recent_EntryShowsFormatAndKeepsThePrivacyNoteInItsTooltip()
    {
        await ClearStoreAndReload();
        await UploadAndReturnHome(Sample("cyclonedx-1.6-full.json"));

        await Expect(Page.Locator(".recent-item-meta")).ToContainTextAsync("CycloneDX 1.6", new() { Timeout = 15_000 });

        // The visible "kept in this browser only" line moved into tooltips when the row was
        // compacted; the reassurance itself must not be lost.
        await Expect(Page.Locator(".recent-bar-title")).ToHaveAttributeAsync(
            "title", new System.Text.RegularExpressions.Regex("this browser only"));
        await Expect(Page.Locator(".recent-item")).ToHaveAttributeAsync(
            "title", new System.Text.RegularExpressions.Regex("this browser only"));
    }

    [Test]
    public async Task Recent_ClickingAnEntryReopensIt()
    {
        await ClearStoreAndReload();
        await UploadAndReturnHome(Sample("cyclonedx-1.6-full.json"));

        await Page.Locator(".recent-item").First.ClickAsync();

        await Expect(Page.Locator(".dashboard")).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await Expect(Page.Locator(".sbom-meta-name")).ToContainTextAsync("cyclonedx-1.6-full.json");
    }

    // ── Identity: content hash, not file name ────────────────────────────────────────────────

    [Test]
    public async Task Recent_TwoDifferentFilesWithTheSameName_AreTwoEntries()
    {
        await ClearStoreAndReload();
        await UploadAndReturnHome(Payload("bom.json", Bom("storefront", "2.4.1")));
        await UploadAndReturnHome(Payload("bom.json", Bom("checkout", "1.0.0")));

        await Expect(Page.Locator(".recent-item")).ToHaveCountAsync(2, new() { Timeout = 15_000 });

        // The subject is what tells them apart.
        var names = string.Join("|", await Page.Locator(".recent-item-name").AllInnerTextsAsync());
        Assert.That(names, Does.Contain("bom.json · checkout 1.0.0"));
        Assert.That(names, Does.Contain("bom.json · storefront 2.4.1"));
    }

    [Test]
    public async Task Recent_ReuploadingSameFileDoesNotDuplicateIt()
    {
        await ClearStoreAndReload();
        await UploadAndReturnHome(Sample("cyclonedx-1.6-full.json"));
        await UploadAndReturnHome(Sample("cyclonedx-1.6-full.json"));

        await Expect(Page.Locator(".recent-item")).ToHaveCountAsync(1, new() { Timeout = 15_000 });
    }

    [Test]
    public async Task Recent_SameContentUnderANewName_IsOneEntryShowingTheNewName()
    {
        await ClearStoreAndReload();
        var content = Bom("storefront", "2.4.1");
        await UploadAndReturnHome(Payload("bom.json", content));
        var before = await ReadStore();

        await UploadAndReturnHome(Payload("storefront-sbom.json", content));

        await Expect(Page.Locator(".recent-item")).ToHaveCountAsync(1, new() { Timeout = 15_000 });
        await Expect(Page.Locator(".recent-item-name")).ToContainTextAsync("storefront-sbom.json");

        var after = await ReadStore();
        Assert.That(after.Entries.Single().Id, Is.EqualTo(before.Entries.Single().Id), "the entry keeps its id");
        Assert.That(after.Entries.Single().Hash, Has.Length.EqualTo(64));
    }

    [Test]
    public async Task Recent_EntrySavedBeforeHashing_IsMatchedOnReuploadNotDuplicated()
    {
        // Upgrade path: a row written by the previous build has no hash, subject or hasContent.
        await ClearStoreAndReload();
        var content = Bom("storefront", "2.4.1");
        await Page.EvaluateAsync("""
            (content) => new Promise((resolve, reject) => {
              const req = indexedDB.open('sbom-viewer');
              req.onerror = () => reject(req.error);
              req.onsuccess = () => {
                const db = req.result;
                const t = db.transaction(['entries', 'content'], 'readwrite');
                const add = t.objectStore('entries').add({
                  name: 'bom.json', format: 'CycloneDX 1.6', size: content.length, savedAt: Date.now() - 86400000 });
                add.onsuccess = () => t.objectStore('content').put({ id: add.result, content });
                t.oncomplete = () => { db.close(); resolve(); };
              };
            })
            """, content);
        await Page.ReloadAsync();
        await Page.WaitForSelectorAsync(".dropzone", new() { Timeout = 30_000 });

        // The legacy row still lists as openable.
        await Expect(Page.Locator(".recent-item:not(.recent-item-gone)")).ToHaveCountAsync(1, new() { Timeout = 15_000 });

        await UploadAndReturnHome(Payload("bom.json", content));

        var store = await ReadStore();
        Assert.That(store.Entries, Has.Length.EqualTo(1), "re-uploading a legacy file must not duplicate it");
        Assert.That(store.Entries[0].Hash, Has.Length.EqualTo(64), "the legacy row adopts the hash");
        Assert.That(store.Entries[0].Subject, Is.EqualTo("storefront 2.4.1"));
    }

    // ── Caps: 5 files in full, 20 names ──────────────────────────────────────────────────────

    [Test]
    public async Task Recent_SixthFile_KeepsOnlyTheNameOfTheOldest_AndReDroppingRestoresIt()
    {
        await ClearStoreAndReload();
        await Seed(6);

        var store = await ReadStore();
        Assert.That(store.Entries, Has.Length.EqualTo(6));
        Assert.That(store.ContentIds, Has.Length.EqualTo(5), "only the newest 5 keep their content");
        var oldest = store.Entries.Single(e => e.Subject == "project-1 1.0.0");
        Assert.That(oldest.HasContent, Is.False);
        Assert.That(store.ContentIds, Does.Not.Contain(oldest.Id));

        // Strip: newest 3, the rest behind "+3 more".
        await Expect(Page.Locator(".recent-list .recent-item")).ToHaveCountAsync(3, new() { Timeout = 15_000 });
        await Page.Locator(".recent-more").ClickAsync();
        await Expect(Page.Locator(".recent-panel .recent-item")).ToHaveCountAsync(3);
        var gone = Page.Locator(".recent-item-gone");
        await Expect(gone).ToHaveCountAsync(1);
        await Expect(gone).ToContainTextAsync("project-1");
        await Expect(gone).ToContainTextAsync("Drop this file again to reopen");

        // It cannot be opened: aria-disabled, which Playwright (like assistive tech) treats as disabled.
        await Expect(gone).ToBeDisabledAsync();

        // Dropping the same file again hash-matches the entry and restores it — as the newest, so
        // the entry that was 5th-newest (project-2) is now the one without content.
        await UploadAndReturnHome(Payload("bom.json", Bom("project-1")));

        store = await ReadStore();
        Assert.That(store.Entries, Has.Length.EqualTo(6));
        Assert.That(store.ContentIds, Has.Length.EqualTo(5));
        var restored = store.Entries.Single(e => e.Subject == "project-1 1.0.0");
        Assert.That(restored.HasContent, Is.True);
        Assert.That(restored.Id, Is.EqualTo(oldest.Id));
        Assert.That(store.Entries.Single(e => !e.HasContent).Subject, Is.EqualTo("project-2 1.0.0"));

        await Expect(Page.Locator(".recent-list .recent-item").First).ToContainTextAsync("project-1");
        await Page.Locator(".recent-list .recent-item").First.ClickAsync();
        await Expect(Page.Locator(".dashboard")).ToBeVisibleAsync(new() { Timeout = 15_000 });
    }

    [Test]
    public async Task Recent_RemembersAtMostTwentyNames()
    {
        await ClearStoreAndReload();
        await Seed(21);

        var store = await ReadStore();
        Assert.That(store.Entries, Has.Length.EqualTo(20));
        Assert.That(store.Entries.Select(e => e.Subject), Does.Not.Contain("project-1 1.0.0"),
            "the oldest name is forgotten");
        Assert.That(store.ContentIds, Has.Length.EqualTo(5));

        await Expect(Page.Locator(".recent-more")).ToHaveTextAsync("+17 more", new() { Timeout = 15_000 });
    }

    [Test]
    public async Task Recent_ManyEntries_StripStaysCompactOnALaptopScreen()
    {
        await ClearStoreAndReload();
        await Seed(8);
        await Page.SetViewportSizeAsync(1366, 650);

        await Expect(Page.Locator(".recent-list .recent-item")).ToHaveCountAsync(3, new() { Timeout = 15_000 });
        await Expect(Page.Locator(".recent-panel")).ToHaveCountAsync(0);
        await Expect(Page.Locator(".recent-bar")).ToBeInViewportAsync(new() { Ratio = 1 });
        await Expect(Page.Locator(".recent-more")).ToBeInViewportAsync(new() { Ratio = 1 });
        await Expect(Page.Locator(".recent-clear")).ToBeInViewportAsync(new() { Ratio = 1 });

        await Page.SetViewportSizeAsync(1400, 900);
        var overflow = await Page.EvaluateAsync<int>(
            "() => { const e = document.querySelector('.upload-screen'); return e.scrollHeight - e.clientHeight; }");
        Assert.That(overflow, Is.LessThanOrEqualTo(0));
    }

    // ── Forgetting ───────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task Recent_RemoveForgetsOneEntry_FromBothStores()
    {
        await ClearStoreAndReload();
        await UploadAndReturnHome(Payload("a.json", Bom("alpha")));
        await UploadAndReturnHome(Payload("b.json", Bom("beta")));

        var before = await ReadStore();
        var beta = before.Entries.Single(e => e.Name == "b.json");

        await Page.Locator(".recent-entry", new() { HasText = "b.json" }).Locator(".recent-remove").ClickAsync();
        await Expect(Page.Locator(".recent-item")).ToHaveCountAsync(1, new() { Timeout = 15_000 });

        var after = await ReadStore();
        Assert.That(after.Entries.Select(e => e.Id), Does.Not.Contain(beta.Id));
        Assert.That(after.ContentIds, Does.Not.Contain(beta.Id));
        Assert.That(after.Entries.Single().Name, Is.EqualTo("a.json"));
        Assert.That(after.ContentIds, Has.Length.EqualTo(1));
    }

    [Test]
    public async Task Recent_ClearRemovesEverything()
    {
        await ClearStoreAndReload();
        await UploadAndReturnHome(Sample("cyclonedx-1.6-full.json"));
        await Expect(Page.Locator(".recent-bar")).ToBeVisibleAsync(new() { Timeout = 15_000 });

        await Page.Locator(".recent-clear").ClickAsync();

        await Expect(Page.Locator(".recent-bar")).ToHaveCountAsync(0, new() { Timeout = 15_000 });
    }

    // ── Compare page ─────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task Recent_ShortcutFillsAComparisonSlot()
    {
        await ClearStoreAndReload();
        await UploadAndReturnHome(Sample("cyclonedx-1.6-full.json"));

        await Page.GotoAsync($"{BaseUrl}/compare");
        await Page.WaitForSelectorAsync(".compare-slots", new() { Timeout = 30_000 });

        await Page.Locator(".slot-recent-item").First.ClickAsync();

        await Expect(Page.Locator(".compare-slot-filled")).ToHaveCountAsync(1, new() { Timeout = 15_000 });
        await Expect(Page.Locator(".compare-slot-file")).ToContainTextAsync("cyclonedx-1.6-full.json");
    }

    [Test]
    public async Task Recent_CompareChips_OfferOnlyFilesWithContent_NewestThree()
    {
        await ClearStoreAndReload();
        await Seed(6);

        await Page.GotoAsync($"{BaseUrl}/compare");
        await Page.WaitForSelectorAsync(".compare-slots", new() { Timeout = 30_000 });

        var chips = Page.Locator(".compare-slot").First.Locator(".slot-recent-item");
        await Expect(chips).ToHaveCountAsync(3, new() { Timeout = 15_000 });

        var titles = new List<string>();
        foreach (var chip in await chips.AllAsync())
            titles.Add(await chip.GetAttributeAsync("title") ?? "");

        var joined = string.Join("|", titles);
        Assert.That(joined, Does.Contain("project-6").And.Contain("project-4"));
        Assert.That(joined, Does.Not.Contain("project-1 "), "an entry without content is never offered");
    }
}
