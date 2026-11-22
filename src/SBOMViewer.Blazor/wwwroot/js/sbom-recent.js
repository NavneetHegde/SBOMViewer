/*
 * Recent SBOM storage (IndexedDB).
 *
 * Keeps the most recent MAX_CONTENT uploads in full so they can be re-opened without the user
 * finding the file again, and remembers the names of up to MAX_ENTRIES. Everything stays on this
 * machine — nothing is sent anywhere.
 *
 * Metadata and file content live in separate object stores so listing the recents does not have
 * to read tens of megabytes of JSON back out.
 *
 * Naming follows the flat `sbom*` global convention used by the inline scripts in index.html.
 */
(function () {
  var DB_NAME = 'sbom-viewer';
  var DB_VERSION = 1;
  var ENTRIES = 'entries';
  var CONTENT = 'content';
  var MAX_CONTENT = 5;   // entries whose file content is kept, newest first
  var MAX_ENTRIES = 20;  // entries whose metadata (name, format, subject) is kept

  function openDb() {
    return new Promise(function (resolve, reject) {
      if (!self.indexedDB) { reject(new Error('IndexedDB unavailable')); return; }

      var req = indexedDB.open(DB_NAME, DB_VERSION);
      req.onupgradeneeded = function () {
        var db = req.result;
        if (!db.objectStoreNames.contains(ENTRIES)) {
          db.createObjectStore(ENTRIES, { keyPath: 'id', autoIncrement: true });
        }
        if (!db.objectStoreNames.contains(CONTENT)) {
          db.createObjectStore(CONTENT, { keyPath: 'id' });
        }
      };
      req.onsuccess = function () { resolve(req.result); };
      req.onerror = function () { reject(req.error); };
      req.onblocked = function () { reject(new Error('IndexedDB blocked')); };
    });
  }

  // Ties (two saves in the same millisecond) fall back to the autoincrement id, so eviction order
  // is deterministic.
  function newestFirst(a, b) { return (b.savedAt - a.savedAt) || (b.id - a.id); }

  // Rows written before content hashing have no hasContent field; they always had content.
  function hasContent(x) { return x.hasContent !== false; }

  /*
   * Saves one file and resolves with its entry id.
   *
   * Identity is the content hash (computed by the caller), not the name: two different files both
   * called bom.json are two entries, and the same file dropped again under a new name is one.
   * Rows saved before hashing existed have no hash; they are matched by name + size instead and
   * adopt the incoming hash, so upgrading users do not see a duplicate.
   *
   * Eviction is two-stage: beyond the newest MAX_CONTENT entries only the file content is dropped
   * (hasContent: false — the name is still remembered); beyond MAX_ENTRIES the entry goes too.
   *
   * All reads and writes happen inside a single transaction — awaiting between steps would let
   * the transaction go inactive.
   */
  window.sbomRecentSave = function (name, format, content, hash, subject) {
    return openDb().then(function (db) {
      return new Promise(function (resolve, reject) {
        var t = db.transaction([ENTRIES, CONTENT], 'readwrite');
        var entries = t.objectStore(ENTRIES);
        var contents = t.objectStore(CONTENT);
        var savedId = null;

        entries.getAll().onsuccess = function (e) {
          var all = e.target.result || [];
          var size = content.length;

          var match = (hash && all.find(function (x) { return x.hash === hash; })) ||
                      all.find(function (x) { return !x.hash && x.name === name && x.size === size; });

          var entry = {
            name: name,
            format: format || '',
            size: size,
            savedAt: Date.now(),
            hash: hash || null,
            subject: subject || null,
            hasContent: true
          };
          if (match) {
            entry.id = match.id;
            // Never lose a known hash because this save could not compute one.
            if (!entry.hash) entry.hash = match.hash || null;
          }

          var write = match ? entries.put(entry) : entries.add(entry);
          write.onsuccess = function () {
            savedId = write.result;
            entry.id = savedId;
            contents.put({ id: savedId, content: content });

            var rest = all.filter(function (x) { return x.id !== savedId; }).sort(newestFirst);
            var ordered = [entry].concat(rest);

            ordered.forEach(function (x, i) {
              if (i >= MAX_ENTRIES) {
                entries.delete(x.id);
                contents.delete(x.id);
              } else if (i >= MAX_CONTENT && hasContent(x)) {
                x.hasContent = false;
                entries.put(x);
                contents.delete(x.id);
              }
            });
          };
        };

        t.oncomplete = function () { resolve(savedId); };
        t.onerror = function () { reject(t.error); };
        t.onabort = function () { reject(t.error); };
      });
    });
  };

  /* Metadata only — never the file content. */
  window.sbomRecentList = function () {
    return openDb().then(function (db) {
      return new Promise(function (resolve, reject) {
        var req = db.transaction(ENTRIES, 'readonly').objectStore(ENTRIES).getAll();
        req.onsuccess = function () {
          resolve((req.result || []).map(function (x) {
            x.hasContent = hasContent(x);
            return x;
          }).sort(newestFirst));
        };
        req.onerror = function () { reject(req.error); };
      });
    });
  };

  window.sbomRecentGet = function (id) {
    return openDb().then(function (db) {
      return new Promise(function (resolve, reject) {
        var req = db.transaction(CONTENT, 'readonly').objectStore(CONTENT).get(id);
        req.onsuccess = function () { resolve(req.result ? req.result.content : null); };
        req.onerror = function () { reject(req.error); };
      });
    });
  };

  /* Forgets one entry — its metadata and its content. */
  window.sbomRecentRemove = function (id) {
    return openDb().then(function (db) {
      return new Promise(function (resolve, reject) {
        var t = db.transaction([ENTRIES, CONTENT], 'readwrite');
        t.objectStore(ENTRIES).delete(id);
        t.objectStore(CONTENT).delete(id);
        t.oncomplete = function () { resolve(true); };
        t.onerror = function () { reject(t.error); };
        t.onabort = function () { reject(t.error); };
      });
    });
  };

  window.sbomRecentClear = function () {
    return openDb().then(function (db) {
      return new Promise(function (resolve, reject) {
        var t = db.transaction([ENTRIES, CONTENT], 'readwrite');
        t.objectStore(ENTRIES).clear();
        t.objectStore(CONTENT).clear();
        t.oncomplete = function () { resolve(true); };
        t.onerror = function () { reject(t.error); };
        t.onabort = function () { reject(t.error); };
      });
    });
  };
})();
