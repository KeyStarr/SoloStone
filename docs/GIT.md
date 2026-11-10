# Git guidelines

How SoloStone's branches and commits are named, and how a change gets into `main`. Pull requests are welcome
([CONTRIBUTING.md](../.github/CONTRIBUTING.md)).

## Branches

| Branch | For | Starts from | Ends in |
|---|---|---|---|
| `main` | The latest accepted work; always buildable, and changed only by a squash-merged pull request | | |
| `feature/<name>` | New behaviour, e.g. `feature/arena-mode` | `main` | `main` |
| `fix/<name>` | A bug fix, e.g. `fix/legend-rank-count` | `main` | `main` |
| `release/<version>` | Getting a release ready: version, notes, signed manifest, e.g. `release/0.1.1` | `main` | `main`, tagged `v<version>` |

- Names are lowercase, with hyphens between words.
- One topic per branch. Delete the branch once it's merged.
- Each release is tagged `v<version>` on `main`, e.g. `v0.1.0`. The launcher downloads updates from that tag.
- Outside contributors work on a branch in their own fork. Maintainers branch in the main repository.

## Pull requests, squash merges and sign-off

- Every change reaches `main` through a pull request. **Nothing is pushed to `main` directly**, and history on `main`
  and on tags is never rewritten or force-pushed.
- `tests` (`python3 tests/run.py --ci`), `dco` and `scan` must pass before a merge.
- A pull request is **squash-merged**: one commit on `main` per pull request. The pull request title becomes the commit
  subject, so write it as a commit subject (below).
- Every commit carries a `Signed-off-by:` line ([Developer Certificate of Origin 1.1](https://developercertificate.org/)),
  which `git commit -s` adds. The `dco` check fails the pull request otherwise. GitHub's squash message can drop the
  sign-off lines, so the maintainer copies every `Signed-off-by:` line of the pull request's commits into the squash
  message before merging.
- To fix a missing sign-off on your branch: `git rebase --signoff origin/main`, then `git push --force-with-lease` to
  your own fork's branch. Never force-push `main`.
- Commit under one identity per person, with an email that person is happy to have public.

## Commits

```text
<type>: <change>; <change>; <change>

Optional: why, when a change isn't obvious from the diff.
```

| Type | For |
|---|---|
| `feat` | New behaviour |
| `fix` | A bug fix |
| `docs` | Documentation only |
| `release` | Version, manifest, release notes |

Examples:

```text
feat: Arena draft screen; 3-card picks; run rewards
fix: Legend rank counts every NPC with a Legend medal; rank text no longer overlaps
release: 0.1.1

The manifest seq goes to 2; 0.1.0 installs pick the update up on their next launch.
```

- The subject line is short, in the present tense, with no full stop at the end.
- Separate several changes with `;`. If it no longer fits on one line, split it into more commits.
- A body is optional. Write one only when the reason for the change would surprise a reader.
- A sign-off line ends the message, after a blank line:

```text
fix: Legend rank counts every NPC with a Legend medal

Signed-off-by: Your Name <you@example.com>
```
