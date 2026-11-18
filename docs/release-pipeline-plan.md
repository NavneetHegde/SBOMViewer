# Release Pipeline — One Path to Production, With Rollback

> Phase 0 implemented except the Azure portal cleanup. Written 2026-11-15 after the v3.2.7 release.

## Context

Today a change reaches production through two pull requests and three workflows: feature → `release/*` (CI), push to `release/*` (E2E, version bump commit, staging deploy, auto-PR), then `release/*` → `main` (production deploy, GitHub release).

It works when every step succeeds. The v3.2.7 release showed what happens when one does not:

- `RELEASE_PAT_SBOM` had expired, so the `release` job failed at checkout. No version bump, no staging deploy, no auto-PR.
- The release PR (#45) and the `v3.2.7` tag were created by hand.
- Production deployed a build that reports **3.2.6**. The workflow tried to create release `v3.2.6`, found it existed, and skipped without failing.

Current state: `Directory.Build.props` says 3.2.6, the latest tag is `v3.2.7` (on a release-branch commit, not a main commit), and production serves `sbomviewer_3.2.6.json`.

### What already exists

- `ci.yml` — build and 218 unit tests on every PR. Reliable. **Does not run E2E**, despite what `CLAUDE.md` says.
- `release-staging.yml` — the only place E2E runs, which is after the feature PR has merged.
- `azure-static-web-apps-sbomviewer.yml` — production deploy on push to `main`. Has succeeded every time. Also runs on PRs to `main`, creating preview environments that have hit the Azure free-tier limit ("maximum number of staging environments").
- `azure-static-web-apps-yellow-coast-02589630f.yml` — legacy. Its secret no longer exists; it fails on every push and PR to `main`.
- Rulesets on `main` and `release/*` — a PR is required, with zero approvals, no required status checks, and admin bypass always on.
- No GitHub Environments, so no deployment approval gates.
- `CLAUDE_CODE_OAUTH_TOKEN` secret — present, unused by any workflow.
- The E2E suite is black-box and takes its target from `BASE_URL`, so it can be pointed at staging or production unchanged.
- `Home.razor` reads the version from the assembly and links `sbomviewer_{version}.json` in the footer, so the version must be known at build time.

## Requirements

- One pull request per change. Merging it is the code approval.
- One click to approve production, made with the staging site in front of you.
- The files approved on staging are the files that go to production — no rebuild in between.
- A tag exists if and only if that version went live, and always points at a `main` commit.
- A bad release can be taken out of production in about a minute, and out of `main` by an ordinary reviewed PR.
- No long-lived personal access token anywhere in the path.
- A failed step fails loudly. Nothing is skipped silently.
- An agent can open PRs and request deployments, but cannot approve either.

## Approach

### Target flow

```
feature PR ─► main          required checks: build, unit, E2E
     │ squash merge
     ▼
  compute version ─► build once ─► artifact (wwwroot + SBOM)
     ▼
  deploy to "staging"  ─► smoke test against staging
     ▼
  ⏸ "production" environment — required reviewer
     ▼
  deploy same artifact ─► smoke test against sbomviewer.com
     ▼
  tag vX.Y.Z ─► GitHub release with the artifact zip attached
```

`release/*` branches are retired. They add a second PR, a bump commit and a PAT, and give no isolation in return because only one line of development is ever active.

### Versioning

- `Directory.Build.props` keeps **major.minor only** (hand-edited for a minor or major bump). Local builds get a `-local` suffix.
- The pipeline computes the patch: highest existing `vMAJOR.MINOR.*` tag plus one, or `.0` if there is none. It passes `-p:Version=X.Y.Z` to `dotnet publish` and to the CycloneDX SBOM step.
- The tag is created **after** the production smoke test passes. A run that is superseded or rejected at the gate never consumed a version number.
- No bump commit, no `[skip ci]`, no `RELEASE_PAT_SBOM`.
- Existing tags are left alone, including `v3.2.7` on its release-branch commit. Tags are never moved or deleted.

### Gates

| Gate | Mechanism | Who |
|------|-----------|-----|
| Code | Squash-merge the PR; required checks must be green | You |
| Production | `production` environment with a required reviewer | You |
| Rollback | Same `production` environment | You |

Squash-only merging is enforced in the `main` ruleset, so one change is one commit and one `git revert` removes it.

### Rollback

A separate `rollback.yml`, manually triggered, input `version` (defaults to the release before the current one):

1. Download that release's artifact zip from GitHub Releases.
2. Deploy it to production through the `production` gate. No build, no tests.
3. Run the smoke test and confirm the footer reports the rolled-back version.
4. Open a **revert PR** against `main`: `git revert` of every commit between the rolled-back tag and the bad tag.

The revert PR then travels the normal pipeline and ships as a new patch version. Until it merges, `main` still holds the bad code, so the rollback workflow also posts a warning on any open PRs — merging one would redeploy the bad change.

To bring the feature back: fix it on a branch that starts with a revert of the revert.

### What rollback cannot undo

Data already written to the user's browser. If a release changes the IndexedDB layout used by the recents store, the previous version meets the newer data after a rollback. `RecentSbomStore` is best-effort, so the expected result is "no recents" rather than a failure, but:

- Storage-format changes ship alone, never bundled with a feature.
- A storage change must be readable by the previous release, or must fail into the best-effort path. Add an E2E test that proves it.

This applies directly to feature 3 of the November release (recents identity and caps).

### Agentic layer

Built on the same gates, after the pipeline is stable.

1. **Request** — an issue labelled `feature-request`. A workflow using `anthropics/claude-code-action` implements it on a branch and opens a PR.
2. **Review** — CI runs; a second Claude job posts a review comment. You approve and merge.
3. **Staging** — deploys automatically; the staging URL is posted back to the issue.
4. **Production** — GitHub requests your approval. Approving deploys, tags, releases and closes the issue.
5. **Proposal loop** — a scheduled run that reads `docs/feature-enhancements/`, open issues and recent failures, and opens at most one new issue. It never opens a PR on its own.

Guard rails:

- The agent's token has no permission to edit `.github/workflows/` or `Infra/`, and cannot approve PRs or deployments.
- `CODEOWNERS` covers `.github/` and `Infra/` (the ruleset already demands code-owner review, but no `CODEOWNERS` file exists).
- Admin bypass on `main` is narrowed so that required checks apply to everyone.

## Tasks

### Phase 0 — Repair (one small PR, current flow)

- [x] Set `<Version>` to `3.2.7` in `Directory.Build.props`. The resulting release will be `v3.2.8`, the first one where props, tag and production agree.
- [x] Delete `azure-static-web-apps-yellow-coast-02589630f.yml`.
- [x] Remove the `pull_request` trigger and `close_pull_request` job from the production workflow.
- [x] Remove `2>/dev/null || echo "…skipping"` from the release-creation and PR-creation steps.
- [ ] In the Azure portal, delete leftover preview environments.

### Phase 1 — Catch failures before merge

- [ ] Add the Playwright steps to `ci.yml` (copy from `release-staging.yml`).
- [ ] Tag a small subset of E2E tests `[Category("Smoke")]`: page loads, Blazor boots, `/?sample=1` opens the sample, footer shows a version.
- [ ] Add the CI job as a required status check in the `main` ruleset. Replace `paths-ignore` in `ci.yml` with an in-workflow path check, otherwise docs-only PRs wait forever on a check that never runs.

### Phase 2 — Environments

- [ ] Create `staging` and `production` GitHub Environments. `production`: required reviewer, deployments restricted to `main`.
- [ ] Move the Azure deploy token from a repository secret to environment secrets.
- [ ] Decide whether staging gets its own Static Web App (see open decisions).

### Phase 3 — Single pipeline

- [ ] New `release.yml` on push to `main`: compute version → build once → upload artifact → staging → smoke → production gate → deploy → smoke → tag → release with zip attached.
- [ ] Concurrency: a newer merge supersedes a run still waiting for approval; a production deploy in flight is never cancelled.
- [ ] Change `Directory.Build.props` to major.minor plus a `-local` default.
- [ ] Set the `main` ruleset to squash-only.
- [ ] Delete `release-staging.yml`, the old production workflow, the `release` ruleset and the `RELEASE_PAT_SBOM` secret. Leave old `release/*` branches in place as history.
- [ ] Update `CLAUDE.md` and `AGENTS.md`: versioning, CI/CD table, branch strategy.

### Phase 4 — Rollback

- [ ] `rollback.yml` as described above.
- [ ] Rehearse it: roll production back one version and forward again, on a day nothing is at stake.
- [ ] Add a short `docs/runbook-rollback.md`: when to roll back, the one command, what to check after.

### Phase 5 — Agent workflows

- [ ] Issue-to-PR workflow and PR review workflow.
- [ ] `CODEOWNERS`; narrow admin bypass.
- [ ] Automatic rollback *request* when the production smoke test fails.
- [ ] Scheduled proposal loop, limited to opening issues.

## Verification

- **Phase 0** — one release through the current flow ends with props, tag, GitHub release and footer all reading `v3.2.8`; no red runs on the PR.
- **Phase 1** — a PR that breaks an E2E test cannot be merged.
- **Phase 3** — merge a trivial change: staging updates without action, production waits for approval, the tag appears only after approval, and the release carries a zip. Reject one run at the gate and confirm no tag was created and the next run reuses the number.
- **Phase 4** — the rehearsal: production reports the older version within about a minute; the revert PR exists and passes CI.
- **Phase 5** — an agent-authored PR that touches `.github/workflows/` is blocked.

## Open decisions

1. **Separate Static Web App for staging?** Staging is currently a named environment inside the production app, so one deploy token can write to both. A separate free-tier app gives the `staging` job a token that cannot touch production. Recommended before phase 5; optional before that. An unused `AZURE_STATIC_WEB_APPS_API_TOKEN_STAGING` secret suggests this existed once.
2. **Direct pushes to `main` for docs.** `README` edits currently go straight to `main` through admin bypass. Removing the bypass means a PR for every docs change. Recommended: keep the bypass until phase 5, then narrow it.
3. **PRs opened by workflows do not trigger CI** when created with the default `GITHUB_TOKEN`. This affects the revert PR in phase 4. Options: have `rollback.yml` dispatch `ci.yml` explicitly on the revert branch, or use a GitHub App token. To be settled while building phase 4.

## Risks

- **Environment approval on this repository's plan** — required reviewers are available on public repositories; confirm in settings before phase 2.
- **Version collision** — two runs computing the same patch. Prevented by the concurrency rule; covered by the phase 3 verification.
- **Tests that assert a version string** — check `HomePageTests` before changing `Directory.Build.props` in phase 3.
