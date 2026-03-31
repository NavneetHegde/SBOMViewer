# Full Agentic Development Workflow

## Overview

This document describes the end-to-end agentic development workflow for SBOMViewer. It
automates the complete lifecycle from receiving a requirement through to a production
deployment on `main`. The workflow is designed to run with minimal human intervention,
escalating to the user only when automated recovery has been exhausted.

The workflow is invoked via the `/full-agentic` Claude Code command (`.claude/commands/full-agentic.md`).

---

## Inputs / Arguments

| Argument | Required | Description |
|---|---|---|
| `requirement` | Yes | Free-text description of what to build or fix |
| `branch_type` | Yes | `feat` or `fix` |
| `base_release_branch` | No | An existing remote release branch, e.g. `release/3.1`. If omitted or not found on remote, a new release branch is created from `main`. |

**Example invocations:**

```
/full-agentic Add CycloneDX 1.8 format support | feat | release/3.1
/full-agentic Fix vulnerability badge color on dark theme | fix
/full-agentic Add export-to-CSV feature | feat
```

---

## Version Resolution

The current version is read from `Directory.Build.props` at the repo root:

```xml
<Version>X.Y.Z</Version>
```

When creating a new release branch, the naming convention is `release/X.Y` (major.minor
only, no patch). The patch component is managed by CI/CD (`release-staging.yml`), not
by this workflow.

For example, if `Directory.Build.props` contains `<Version>3.0.6</Version>`, a new
release branch would be named `release/3.0`. If `release/3.0` already exists on remote,
the next minor is used: `release/3.1`.

---

## Step-by-Step Workflow

### Step 1 — Branch Setup

**Goal:** Establish a clean feature branch rooted in the correct release branch.

```
START
  │
  ├─ base_release_branch provided?
  │     │
  │     ├─ YES → Does it exist on remote?  (git ls-remote --heads origin <branch>)
  │     │              │
  │     │              ├─ YES → Use it as RELEASE_BRANCH
  │     │              └─ NO  → Fall through to "create new release branch"
  │     │
  │     └─ NO → "create new release branch"
  │
  └─ Create new release branch
         Read version from Directory.Build.props
         Derive candidate: release/X.Y
         If release/X.Y already exists on remote → use it
         If not:
           git fetch origin main
           git checkout -b release/X.Y origin/main
           git push -u origin release/X.Y

  ─── Release branch is now established ───

  Derive feature branch name from requirement:
    feat → feat/<kebab-case-summary-of-requirement>
    fix  → fix/<kebab-case-summary-of-requirement>
    Max 40 characters after the prefix. Lowercase, hyphens only.

  git fetch origin <release-branch>
  git checkout -b <feature-branch> origin/<release-branch>

RESULT: On <feature-branch>, tracking origin/<release-branch>
```

**Rules:**
- Never branch from `main` directly for feature work.
- Always fetch remote before branching to ensure latest state.
- If the feature branch name already exists locally, append a short timestamp suffix.

---

### Step 2 — Implementation

**Goal:** Analyse the requirement and implement the changes.

1. Re-read `CLAUDE.md` to refresh architecture context.
2. Identify the affected files based on the requirement.
3. Implement code changes, following the coding conventions in `CLAUDE.md`.
4. Add or update unit tests in `tests/SBOMViewer.Blazor.Tests/` to cover the change.
5. If the change affects UI rendering, note which E2E test file would need updating.
6. Do NOT modify `Directory.Build.props` — version bumping is CI/CD's responsibility.
7. Do NOT commit yet — Step 3 handles commits after tests pass.

---

### Step 3 — Test, Commit, Push, and PR Loop

**Goal:** Gate the feature branch on passing unit tests, then create a reviewed PR.

**Max attempts: 5**

```
attempt = 1

LOOP:
  Run unit tests:
    dotnet test tests/SBOMViewer.Blazor.Tests

  Tests PASS?
  │
  ├─ YES:
  │    Group all changes into logical atomic commits (see commit-atomic.md)
  │    Use conventional commit message format: feat|fix|test|refactor|chore(scope): message
  │    git push -u origin <feature-branch>
  │    Create PR targeting <release-branch>  (gh pr create --base <release-branch>)
  │    PR title: conventional commit style summary
  │    PR description: What changed / Why / How to test
  │    Run PR review checklist (see pr-review.md):
  │      - Scan for debug code, hardcoded secrets, TODO blockers, unused imports
  │      - Confirm tests pass
  │    If review passes: approve and merge
  │      gh pr merge --squash --auto
  │    Poll until state = MERGED
  │    EXIT LOOP → proceed to Step 4
  │
  └─ NO:
       attempt += 1
       attempt > 5?  → HUMAN-IN-LOOP ESCALATION (see below)
       Analyse failing tests — identify root cause
       Apply targeted fix (do not re-apply a fix already tried)
       Continue LOOP
```

**Human-in-Loop Escalation — Step 3:**

Stop all execution and present this summary to the user:

```
ESCALATION: Unit test failures after 5 attempts

Feature branch:  <branch-name>
Release branch:  <release-branch>
Requirement:     <original requirement>

Failing tests:
  <list each failing test class::method>

Last error output:
  <paste last dotnet test stderr/stdout>

Fix history:
  Attempt 1: <description of fix applied>
  Attempt 2: <description of fix applied>
  ...

Please advise how to proceed:
  A) Provide guidance — I will apply your fix and retry
  B) Skip a specific failing test (confirm it is unrelated to this change)
  C) Abandon this feature branch
```

---

### Step 4 — Release Branch Integration

**Goal:** Confirm the release branch is stable after the PR merge.

```
git fetch origin <release-branch>
git checkout <release-branch>
git pull origin <release-branch>

Run unit tests:
  dotnet test tests/SBOMViewer.Blazor.Tests

Run E2E tests (if environment supports a local server):
  dotnet build SBOMViewer.slnx -c Release
  dotnet publish src/SBOMViewer.Blazor -c Release --output publish_output
  npx serve -s publish_output/wwwroot -l 5000 &
  dotnet test tests/SBOMViewer.E2E.Tests -c Release --no-build -e BASE_URL=http://localhost:5000

  Note: If the serve process cannot start, run unit tests only and note
  "E2E skipped — will be validated by ci.yml on push."

All tests PASS?
│
├─ YES → proceed to Step 5
│
└─ NO:
     attempt = 1
     LOOP (max 5):
       Analyse failures
       Apply fix directly on <release-branch>
       git commit -m "fix(<scope>): <stabilisation description>"
       Run dotnet test tests/SBOMViewer.Blazor.Tests
       PASS? → Run full suite again → PASS? → EXIT LOOP → Step 5
       attempt += 1
       attempt > 5? → HUMAN-IN-LOOP ESCALATION (see below)
```

**Human-in-Loop Escalation — Step 4:**

```
ESCALATION: Release branch unstable after 5 fix attempts

Release branch:  <release-branch>
Merged PR:       <PR URL>

Failing tests:
  <list each failing test name>

Last test output:
  <paste dotnet test output>

Fix history:
  Attempt 1: <what was committed>
  ...

The release branch is currently unstable. Please advise:
  A) Provide guidance — I will apply the fix and retry
  B) Revert the merged PR: gh pr revert <PR number>
  C) Proceed to main merge despite failures (not recommended)
```

---

### Step 5 — Merge to Main

**Goal:** Promote the stable release branch to `main`, triggering production deployment.

```bash
git fetch origin main
git checkout main
git pull origin main
git merge --no-ff origin/<release-branch> -m "chore: merge <release-branch> to main"
git push origin main
```

**If push fails (remote ahead by CI automation only):**
```bash
git pull --rebase origin main
git push origin main
```

**If push fails for substantive reasons:** Stop and ask the user to review.

On success, report:
- Commit SHA on main
- Pipelines triggered: `azure-static-web-apps-sbomviewer.yml`
- Expected CI actions: patch bump in `Directory.Build.props`, production deploy to `www.sbomviewer.com`, GitHub release creation

---

### Step 6 — Documentation Save

Append a run summary to `docs/full-agentic.md` under `## Run History`.
Commit with `[skip ci]` to prevent re-triggering the production deploy.

```bash
git add docs/full-agentic.md
git commit -m "docs: record full-agentic run for <requirement> [skip ci]"
git push origin main
```

---

## Retry Logic Summary

| Step | Max Attempts | On Exhaustion |
|---|---|---|
| Step 3 — unit test gate | 5 | Human-in-Loop escalation |
| Step 4 — release branch stability | 5 | Human-in-Loop escalation |

Attempt counter resets between steps. Each attempt must apply a distinct fix — do not repeat the same change.

---

## Hard Escalation Conditions (immediate, any step)

Stop and inform the user immediately — do not wait for retry exhaustion:

1. `git push origin main` fails for a non-obvious reason
2. A PR merge fails due to unresolvable merge conflicts
3. `Directory.Build.props` cannot be read or parsed
4. Remote repository is unreachable (`git fetch` fails with network error)
5. Test failure is caused by an unavailable external service (e.g., OSV.dev API down) — this is an environment issue, retrying will not help
6. `gh` CLI is not authenticated (`gh auth status` fails)

---

## CI/CD Interaction

These pipelines fire automatically through git operations — no manual triggers needed:

| Git Action | Pipeline | Effect |
|---|---|---|
| PR opened against `release/*` | `ci.yml` | Build + unit tests + E2E |
| Push to `release/*` | `release-staging.yml` | RC version, staging deploy, auto-PR to main |
| Push to `main` | `azure-static-web-apps-sbomviewer.yml` | Patch bump, production deploy, GitHub release |

---

## Existing Commands Referenced

| Command | Used In |
|---|---|
| `.claude/commands/new-branch.md` | Step 1 — branch creation pattern |
| `.claude/commands/commit-atomic.md` | Step 3 — atomic commit grouping |
| `.claude/commands/pr-review.md` | Step 3 — pre-merge review checklist |
| `.claude/commands/push.md` | Step 3 — push pattern |
| `.claude/commands/test-all.md` | Step 4 — full suite on release branch |

---

## Run History

<!-- Appended automatically by /full-agentic runs -->
