# Runbook — Rolling Back a Bad Release

Use this when the version live on sbomviewer.com is broken and the fix is not minutes away. Rolling back is cheap and reversible; prefer it over debugging against live users.

## When to roll back

- The release's production smoke test failed (the `smoke-production` job in `release.yml` is red).
- The site does not load, the dashboard does not render, or a core flow (upload, sample, scan, compare) is broken.
- Anything that a one-line fix would not resolve within the hour.

Not for: cosmetic issues, or a problem that also exists in the previous version — rolling back would not fix those.

## Roll back

1. Start the rollback. Empty `version` means "the release before the latest":

   ```bash
   gh workflow run rollback.yml --ref main
   # or a specific version:
   gh workflow run rollback.yml --ref main -f version=3.2.11
   ```

   Or in GitHub: **Actions → Rollback — Redeploy a Previous Release → Run workflow**.

2. Approve the `production` deployment when GitHub asks. The run summary shows which version goes live.

3. Wait for the `smoke` job (about 3 minutes). Green means sbomviewer.com is serving the older version and its footer shows it.

Production is fixed at this point. Nothing was rebuilt: the zip attached to that version's GitHub release is deployed as-is.

## Then fix `main`

**`main` still contains the bad code. The next merge would ship it again.**

The rollback opens an issue titled *"Rolled back vX → vY: revert pending"* with a **Create the revert PR** link. The branch `revert/vX` holds one commit reverting every code change shipped in the bad release.

1. Follow the link and create the PR. CI runs on it.
2. Squash-merge it. `release.yml` ships it as a new patch version through staging and your approval as usual.
3. Close the issue.

Until that revert is merged, do not merge other PRs.

If the issue says the automatic revert hit a conflict, revert by hand:

```bash
git switch -c revert/vX origin/main
git revert --no-commit $(git rev-list --no-merges vY..vX)
git commit -m "revert: roll main back from vX to vY"
```

## Bring the feature back

Fix it on a branch that starts by reverting the revert commit, then add the fix on top. It ships as a normal release.

## Versions

Versions only move forward. The bad release keeps its tag and GitHub release; nothing is deleted or moved. After a rollback from v3.2.12 to v3.2.11, the revert ships as v3.2.13.

## Limits

- **Only releases made by `release.yml` can be targets** — v3.2.11 and later. Older releases have no site zip attached.
- **Data in visitors' browsers is not rolled back.** If the bad release changed the IndexedDB layout used by the recents store, the older version meets the newer data. `RecentSbomStore` is best-effort, so the expected effect is "no recents", not a crash — but storage-format changes should ship on their own and stay readable by the previous version.
- **Rehearse after pipeline changes.** Running the rollback with the *latest* version redeploys it unchanged and skips the revert — a safe way to exercise approval, deploy and smoke.
