# User Feedback — Ratings, Events, and a Way to Talk Back

## Context

There is no login, so there is no user list, no email, and no way to ask anyone anything. The only
signal today is Google Analytics 4 page views (`index.html:61`, `G-WW1BML3K6H`) — which tells us how
many times a page loaded and nothing about whether the app was useful.

Two different questions are hiding inside "get user feedback", and they need different mechanisms:

- **What do people actually use?** Needs volume and zero friction. Nobody fills in a form to tell you
  they clicked Compare.
- **Is it any good, and what is missing?** Needs depth. A handful of thoughtful replies beats a
  thousand star ratings.

A single "rate us ★★★★★" widget serves neither well, which is why this plan is three separate
things rather than one.

### The constraint that shapes everything

The app's pitch is that your SBOM does not go anywhere. That claim appears in three places
(`DynamicSbomViewer.razor:18` and `:830`, `Compare.razor:86`). For a security tool, that credibility
*is* the product — a feedback feature that erodes it costs more than it returns.

But the claim is already looser than the copy admits, and this plan should not pretend otherwise:

- GA4 already sends page views to Google on every visit.
- The vulnerability scan posts **package names and versions** to `api.osv.dev`, then CVE ids to
  `api.first.org`, and downloads the KEV catalogue from GitHub.
- **`DynamicSbomViewer.razor:18` says "No data leaves your machine" on the scanning overlay — while
  the scan is in the middle of sending package coordinates to OSV.dev.** That is simply inaccurate,
  and it is on screen at the exact moment it is least true.

So the honest framing is not "we never talk to the network" but **"your SBOM document is never
uploaded"**. That is still a strong, true, differentiating claim. Fixing that copy is a prerequisite
of this work, not a nice-to-have: adding a feedback channel while an inaccurate privacy claim sits on
screen makes the inaccuracy worse.

## Recommendation

Do these in order. **Tier 0 blocks tier 1. Tiers 1 and 3 are worth doing regardless. Tier 2 is
conditional** — see below.

| Tier | What | Effort | New infra |
|---|---|---|---|
| 0 | Consent banner + `PRIVACY.md` | ~1 day | None |
| 1 | GA4 custom events | Hours | None |
| 2 | In-app rating widget + endpoint | Days | Azure Function + Table Storage |
| 3 | Pre-filled GitHub issue link | Under an hour | None |

### Why Tier 2 is conditional

In-app feedback widgets typically see **1–2% response rates**, and lower on developer tools where
the audience is allergic to being interrupted. If the app is getting a few hundred sessions a month,
that is single-digit responses — not enough to learn from, and not worth a storage account, an
abuse-prone public endpoint, and a GDPR surface.

**Ship Tier 1 first and read the numbers.** The event volume tells you whether Tier 2 will produce a
usable sample. Building the widget first is guessing.

---

## Tier 0 — Consent (decided: keep GA4, add a banner)

**Decision taken.** GA4 stays; a consent banner gates it. The alternative considered and rejected was
replacing GA4 with cookieless analytics (Plausible/Umami), which would have removed the consent
question rather than managing it. Recorded here so the trade-off is not rediscovered later.

**This is not a tier-1 prerequisite because tier 1 creates the problem — it is one because the
problem already exists.** GA4 sets `_ga` cookies on load today, with no prior consent. Under the
ePrivacy Directive (Art. 5(3)) — which is separate from, and additional to, GDPR — storing
non-essential information on a device requires consent *before* it happens, and analytics cookies are
not "strictly necessary" under most DPA guidance. That gap is live at v3.2.5 with zero product
analytics. Tier 1 does not create it; it just raises the stakes.

*Not legal advice. Worth a real opinion if the exposure matters.*

### Requirements a banner has to actually meet

Half-built cookie banners are worse than none — they carry the UX cost and still fail. The
non-negotiables:

1. **Prior consent.** GA must not set cookies before a choice is made. Today `index.html:61` loads
   `gtag.js` immediately, so this is the substantive change.
2. **Reject as easy as accept.** Equally prominent "Reject" and "Accept" — same size, same styling,
   same screen. A prominent Accept beside a buried "Manage preferences" is the specific pattern EU
   regulators have fined people for.
3. **No implied consent.** Not from scrolling, not from continuing to use the app, no pre-ticked
   boxes.
4. **Revocable.** A footer link to change the choice later, next to the `PRIVACY.md` link.
5. **Expiry.** Persist the choice; re-ask after ~6 months if refused, ~12 if granted.
6. **Non-blocking.** The banner must not gate use of the app. Someone who ignores it entirely should
   be able to upload and scan normally — they simply stay un-measured.

### Where the code has to live

**In `index.html`, as plain HTML/JS — not as a Blazor component.** Two reasons, and the first is
disqualifying:

- Blazor WASM takes seconds to boot. A banner that waits for it would appear well after the page is
  interactive, and any `gtag` call before that has already happened. The gate has to run in the same
  script block that currently loads GA.
- It matches the existing convention — `sbomGetTheme`, `sbomSetFontScale` and friends are already
  flat globals in `index.html`, and the banner can read the stored theme so it does not flash white
  on a dark page.

Use **Google Consent Mode v2**: push `default` as denied *before* `gtag.js` loads, then `update` on
accept.

```js
gtag('consent', 'default', {
  analytics_storage: 'denied', ad_storage: 'denied',
  ad_user_data: 'denied',      ad_personalization: 'denied'
});
```

Store the choice in `localStorage` under `sbom-consent` as `{ state, timestamp }`, alongside the
existing `sbom-theme` and `sbom-font-scale` keys.

**When consent is denied, send nothing at all** — no cookieless pings either. Consent Mode's denied
state still transmits modelled pings, and while those arguably fall outside Art. 5(3) since nothing
is stored on the device, "we send nothing unless you say yes" is a sentence that can be written in
`PRIVACY.md` without a footnote. On a tool that trades on privacy claims, the defensible-in-one-line
version is worth more than the extra data. `sbomTrack` must therefore check consent, not just rely on
Consent Mode.

### What this does to the numbers

Recording the consequence so the data gets read correctly later, not to reopen the decision:

measured traffic ≈ (visitors who do not block `google-analytics.com`) × (visitors who accept).
For a developer and security audience that plausibly lands somewhere around **15–25%**, and it is
**self-selected** — people who accept analytics are not a random sample of people who use the app.

Practical consequence: treat tier-1 output as a **floor and a ratio**, never a population count.
"Compare is used about a tenth as often as Scan" survives this. "We have 412 users" does not, and
neither does "nobody uses Compliance" — a feature used exclusively by consent-refusing enterprise
users would look identical to a dead one.

This matters most for the tier-2 go/no-go, which keys off observed volume. Divide accordingly: a few
hundred *measured* sessions a month may be a few thousand real ones.

## Tier 1 — GA4 custom events

GA4 is already loaded and configured, so this is a `gtag('event', …)` call at a handful of existing
call sites. No new dependency, no new privacy category, no consent-banner question that is not
already open.

Events worth firing, and **only these fields**:

| Event | Parameters | Answers |
|---|---|---|
| `sbom_loaded` | `format` (e.g. `CycloneDX_1_6`), `component_count_bucket` | Which formats matter? Are real documents big? |
| `scan_run` | `package_count_bucket`, `vuln_count_bucket`, `kev_count_bucket` | Is the flagship feature used? Does it find anything? |
| `compare_run` | `baseline_format`, `current_format`, `cross_format` (bool) | Was cross-format diffing worth building? |
| `export_used` | `surface`, `format` | Which of the nine export combinations earn their keep? |
| `tab_view` | `tab` | Is Compliance dead weight? |
| `recent_reopened` | — | Did the IndexedDB store pay off? |

**Bucket, never report raw.** `component_count_bucket` is `"1-50"`, not `1247`. A precise component
count plus a precise vuln count is a weak fingerprint of a specific document; buckets answer the same
product question without that risk.

**Never send**: file names, component names, purls, CVE ids, licenses, or any document content.

Honest limitation, and it needs stating up front rather than discovering it later: **this audience
blocks analytics heavily.** Developers and security engineers run uBlock Origin, Pi-hole and
tracker-blocking DNS at rates far above general web traffic. GA will undercount, plausibly by half.
Treat these numbers as **relative** — "Compare is used a tenth as much as Scan" is trustworthy;
"we had exactly 412 users" is not.

## Tier 2 — In-app rating

### When to ask

**Not on load, and not on a timer.** Ask immediately after a completed valuable action, when the user
has just experienced the thing you are asking about:

- a scan finishes and results render,
- a diff renders,
- an export downloads.

Ask **once per install**, dismissible, and never again if dismissed. A second prompt to someone who
already said no is how a tool gets uninstalled.

### What to ask

Skip the five-star scale — it produces an average hovering around 4.2 that never changes and never
tells you what to do. Ask two things:

1. **"Was this useful?"** — thumbs up / down. One click, so it actually gets answered.
2. **Optional free text** — "What would make it better?"

And one question that directly addresses the gap in the request, because it is unobtainable any
other way:

3. **"What do you use SBOM Viewer for?"** — optional, short. Compliance evidence? Pre-release checks?
   Vendor review? This is the "we don't know our users" answer, and no amount of event data produces it.

### Where it goes

**Azure Static Web Apps managed functions.** The deploy workflow already has the field —
`azure-static-web-apps-sbomviewer.yml:61` is `api_location: ""` — so this is populating a config
value rather than re-architecting anything. Managed functions are available on the current **Free**
tier (`Infra/main.bicep:5`), writing to Table Storage, which for this volume is effectively free.

Rejected alternative: a third-party form service (Tally, Formspree, Google Forms embed). Less work,
but it routes user input through a vendor the app has never mentioned, on a tool whose entire
positioning is "your data doesn't go to random places". Not worth the credibility trade for a few
saved hours. A **plain link out** to such a form is fine — see Tier 3 — because the user visibly
leaves the app to do it.

### Anonymous identity

Generate a random GUID once, store in `localStorage`, send with each submission. This is **not** a
login and not PII — it exists so you can tell fifty ratings from fifty people apart from fifty
ratings from one frustrated person. It must be clearable from the UI, alongside the existing recents
Clear control.

### Payload, in full

```json
{ "installId": "<random guid>", "rating": "up", "comment": "...", "useCase": "...",
  "appVersion": "3.2.5", "context": "post_scan", "sbomFormat": "CycloneDX_1_6" }
```

**Show the user this.** A collapsible "what gets sent" disclosure next to the submit button, listing
the actual fields. On a tool that makes privacy claims, being visibly specific costs one small
component and buys the credibility the rest of the app trades on.

### Abuse

A public unauthenticated write endpoint on a free tier, so this is not optional:

- cap comment and use-case length server-side (say 2000 chars) and reject over-long bodies outright;
- rate-limit per install id **and** per IP — a client-generated id is trivially forged, so it cannot
  be the only control;
- **never render submitted text anywhere in the app.** There is no moderation and no reason to
  display it; keeping it write-only removes stored-XSS as a category rather than mitigating it;
- store the IP hashed or not at all — raw IPs turn a feedback table into personal data.

## Tier 3 — Talk to us (do this first, it is nearly free)

The audience is developers. They already know how to file a good bug report, and they will write far
more than a textarea will ever capture. A footer link (`Home.razor:60-69`, beside the existing
version and Download SBOM) opening a **pre-filled GitHub issue**:

```
https://github.com/NavneetHegde/SBOMViewer/issues/new?labels=feedback
  &title=Feedback:%20
  &body=<prefilled: app version, detected format, browser/UA>
```

No infra, no storage, no personal data, no abuse surface, no consent question — and the highest
quality feedback of the three, from exactly the people most able to give it. Prefilling the version
and format alone removes the most common round-trip on any bug report.

This is the best value-per-hour in the plan. It should ship whether or not anything else does.

> **Implemented.** `Services/FeedbackLink.cs` builds the URL; a `Feedback` link sits in the footer
> (`Home.razor`). The body carries app version, SBOM format label and browser string — never the
> file name or any document content, since issues are public and a file name alone can name an
> employer's unreleased product. The user reviews everything in GitHub's form before submitting.
>
> **Outstanding:** the `feedback` label does not exist on the repository. GitHub silently ignores an
> unknown `labels` parameter, so the link works — but incoming reports will not be auto-labelled
> until someone runs `gh label create feedback`.

## Files

| File | Change |
|------|--------|
| `Components/DynamicSbomViewer.razor` | **Fix** the inaccurate "No data leaves your machine" copy (`:18`, `:830`) |
| `wwwroot/index.html` | Tier 0 — consent-mode defaults before `gtag.js`, banner markup, `sbomConsent*` helpers; and `sbomTrack(name, params)` wrapping `gtag`, gated on consent |
| `wwwroot/css/app.css` | Banner styling, theme-aware like the rest |
| `Pages/Home.razor` | Footer links — `PRIVACY.md`, and "Cookie settings" to revoke |
| `Services/AnalyticsService.cs` | New — typed event calls + bucketing, so no component builds a raw payload |
| Call sites | `SbomLoader`, `DynamicSbomViewer`, `Compare` — one `Track` call each |
| `Pages/Home.razor` | Tier 3 footer link |
| `Components/FeedbackPrompt.razor` | New — Tier 2 widget |
| `Services/FeedbackService.cs` | New — install id + POST, best-effort like `RecentSbomStore` |
| `api/` + `Infra/main.bicep` + `azure-static-web-apps-sbomviewer.yml` | Tier 2 only — function, storage, `api_location` |
| `PRIVACY.md` | New — what is sent, to whom, why. Linked from the footer |

## Verification

**Tier 0 — check in DevTools → Application → Cookies, not by reading the code.** The failure mode
here is a banner that looks right and gates nothing.

- On a first visit with no choice made: **no `_ga` cookie exists** and no request goes to
  `google-analytics.com`. This is the whole feature; if it fails, nothing else matters.
- Reject: still no cookie, still no request. Reload — still none, and the banner does not reappear.
- Accept: cookie appears, events flow. Reload — no banner, events still flow.
- Revoke via the footer after accepting: cookies cleared, requests stop.
- Reject, then hand-age the `sbom-consent` timestamp past 6 months: the banner returns.
- The app is fully usable with the banner ignored — upload, scan and compare all work while
  un-measured.
- The banner renders in the correct theme on a hard refresh with no white flash on a dark page, and
  it appears *before* Blazor finishes booting.
- `sbomTrack` no-ops when consent is absent or denied — verify by calling it from the console.

**Tier 1 onwards:**

- Confirm no event carries a file name, component name, purl, CVE id or license — assert this in a
  unit test over `AnalyticsService`, not by reading the call sites, so a later contributor adding a
  parameter trips a test rather than a reviewer's memory.
- Confirm counts are bucketed, never raw.
- Load with an ad-blocker enabled: every `gtag` path must no-op silently. `sbomTrack` has to tolerate
  `gtag` being undefined — for a meaningful share of this audience it will be.
- Feedback POST failure must be silent and must never block or break the UI, matching the
  best-effort contract `RecentSbomStore` already follows.
- Submit twice from one install and confirm the rate limiter rejects the second.
- Submit a 1MB comment and confirm rejection.
- Confirm the prompt appears at most once per install, and never again after dismissal.
- Re-read every privacy string in the app against what the network tab actually shows. The claim and
  the behaviour have already drifted apart once.

## Open questions

- **Re-prompt intervals.** 6 months after refusal and 12 after consent are conventional rather than
  prescribed. Pick deliberately; re-asking too often is its own dark pattern.
- **Whether a banner is needed for non-EU visitors.** Geo-gating it is possible but needs a
  reasonably accurate signal, and SWA Free gives no server-side geo. Showing it to everyone is
  simpler and more defensible; the cost is a banner for visitors who did not need one.
- **Tier 2's endpoint is a separate consent question.** The rating widget posts data the user typed
  on purpose, which is a different basis from analytics cookies — but the install-id GUID in
  `localStorage` is device storage, so it needs the same Art. 5(3) look. Settle it when tier 2 is
  actually greenlit, not now.
