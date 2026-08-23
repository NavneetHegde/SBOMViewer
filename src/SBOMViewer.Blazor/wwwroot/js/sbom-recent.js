/*
 * Recent SBOM storage (IndexedDB).
 *
 * Keeps the most recent MAX_ENTRIES uploads so they can be re-opened without the user finding
 * the file again. Everything stays on this machine — nothing is sent anywhere.
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
  var MAX_ENTRIES = 2;

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

  function newestFirst(a, b) { return b.savedAt - a.savedAt; }

  /*
   * Saves one file, evicting an earlier copy of the same name and then the oldest entries so at
   * most MAX_ENTRIES remain. All reads and writes happen inside a single transaction — awaiting
   * between steps would let the transaction go inactive.
   */
  window.sbomRecentSave = function (name, format, content) {
    return openDb().then(function (db) {
      return new Promise(function (resolve, reject) {
        var t = db.transaction([ENTRIES, CONTENT], 'readwrite');
        var entries = t.objectStore(ENTRIES);
        var contents = t.objectStore(CONTENT);

        entries.getAll().onsuccess = function (e) {
          var all = e.target.result || [];

          var sameName = all.filter(function (x) { return x.name === name; });
          var others = all.filter(function (x) { return x.name !== name; }).sort(newestFirst);

          // Re-uploading a file replaces its old entry; beyond that, drop the oldest so the
          // incoming file fits within MAX_ENTRIES.
          var drop = sameName.concat(others.slice(Math.max(0, MAX_ENTRIES - 1)));
          drop.forEach(function (x) { entries.delete(x.id); contents.delete(x.id); });

          var add = entries.add({
            name: name,
            format: format || '',
            size: content.length,
            savedAt: Date.now()
          });
          add.onsuccess = function () {
            contents.put({ id: add.result, content: content });
          };
        };

        t.oncomplete = function () { resolve(true); };
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
        req.onsuccess = function () { resolve((req.result || []).sort(newestFirst)); };
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
