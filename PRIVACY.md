# Privacy

SBOM Viewer is a browser application. There is no account, no login, and no server of ours that
your files go to — the app is static files served from a CDN, and all parsing, diffing and rendering
happens in your browser.

That said, "runs in your browser" is not the same as "makes no network requests", and this page is
specific about the difference.

## Your SBOM file is never uploaded

The document you open is read by your browser and never sent anywhere. This is true for viewing,
comparing, license analysis, exporting, and vulnerability scanning.

## What is sent, and when

| When | What is sent | To whom |
|---|---|---|
| Every page load | Page view, referrer, coarse location, device/browser info | Google Analytics |
| You click **Scan for Vulnerabilities** | Package **names, versions and ecosystems** | [OSV.dev](https://osv.dev) |
| A scan finds vulnerabilities | The **CVE identifiers** found | [FIRST.org EPSS](https://www.first.org/epss/) |
| A scan finds vulnerabilities | Nothing — a file is downloaded *from* GitHub | raw.githubusercontent.com |
| You click **Feedback** | Nothing automatically — GitHub opens with a pre-filled form you review and submit yourself | GitHub |

Notes on each:

- **Vulnerability scanning is opt-in.** Nothing is sent to OSV.dev or EPSS unless you press the
  button. Package names and versions are necessarily sent, because that is what a vulnerability
  lookup is. Your SBOM file itself is not.
- **The CISA KEV catalogue** is downloaded from CISA's GitHub mirror. That is a download, so nothing
  about your SBOM is transmitted — but like any HTTP request it reveals your IP address to GitHub.
- **The Feedback link** pre-fills a GitHub issue with the app version, the SBOM *format* (e.g.
  "CycloneDX 1.6") and your browser's user-agent string. It never includes your file name or any
  document contents. You see the whole thing in GitHub's form and can edit or delete any of it
  before submitting. Nothing is sent unless you submit it.

As with any web request, the services above can see your IP address and user-agent. That is inherent
to HTTP, not something the app adds.

## Analytics

The site uses **Google Analytics 4** (`G-WW1BML3K6H`) to count page views. This sets cookies
(`_ga`, `_ga_*`) in your browser and sends Google the usual analytics payload: page URL, referrer,
approximate location derived from IP, and device and browser information. No SBOM data of any kind
is included.

**Currently there is no consent prompt, and analytics load automatically.** Adding one is planned —
see `docs/feature-enhancements/user-feedback-plan.md`. Until then, you can block it the same way you
would on any site: a content blocker, tracker-blocking DNS, or your browser's built-in protection.
Blocking it does not affect any feature of the app.

Google's practices are covered by the [Google Privacy Policy](https://policies.google.com/privacy).

## What is stored on your device

Nothing here leaves your machine, and nothing is readable by us.

| Where | What | Why |
|---|---|---|
| IndexedDB | Your **two most recent SBOM files**, in full | So you can reopen them without finding the file again |
| localStorage | Theme, font scale | So the UI looks the same next visit |
| Cookies | Google Analytics identifiers | Analytics, as above |

The recent-files store holds complete file contents. It is local to your browser and can be emptied
at any time with **Clear** on the home screen, or by clearing site data in your browser.

If you use a shared or public machine, be aware that recent files persist until cleared.

## Third parties

- [OSV.dev](https://osv.dev) — Open Source Vulnerabilities, run by Google's OSS security team
- [FIRST.org](https://www.first.org/epss/) — EPSS exploit-probability scores
- [GitHub](https://github.com) — hosts the CISA KEV mirror, this repository, and issues
- [Google Analytics](https://policies.google.com/privacy)
- Microsoft Azure Static Web Apps — hosts the site

## Questions

Open an issue: <https://github.com/NavneetHegde/SBOMViewer/issues>

---

*This describes SBOM Viewer as it currently behaves. If you find anything here that does not match
what the app actually does, that is a bug worth reporting — please open an issue.*
