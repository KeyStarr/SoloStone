# Git guidelines

How SoloStone's branches and commits are named. For maintainers: outside contributions are issues only for now
([CONTRIBUTING.md](../.github/CONTRIBUTING.md)).

## Branches

| Branch | For | Starts from | Ends in |
|---|---|---|---|
| `main` | What was last released; always buildable | | |
| `feature/<name>` | New behaviour, e.g. `feature/arena-mode` | `main` | `main` |
| `fix/<name>` | A bug fix, e.g. `fix/legend-rank-count` | `main` | `main` |
| `release/<version>` | Getting a release ready: version, notes, signed manifest, e.g. `release/0.1.1` | `main` | `main`, tagged `v<version>` |

- Names are lowercase, with hyphens between words.
- One topic per branch. Delete the branch once it's merged.
- Each release is tagged `v<version>` on `main`, e.g. `v0.1.0`. The launcher downloads updates from that tag.

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
