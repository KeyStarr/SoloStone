# Working in the SoloStone repository

For people and coding agents alike. Be direct and brief.

## Build and test

- Build and test with one command: `python3 tests/run.py`. CI runs the same suites with `--ci`. It needs the .NET SDK that `global.json` names and Python 3.10 or newer, and no Hearthstone file (the `fx-harmony` suite also needs `mono` on `PATH`).
- `python3 tests/run.py --client <your Hearthstone folder>` is for the suites and checks that need your own client (no suite yet, three checks inside `shop`,
  `recipe-picker` and `fx-harmony`; see [docs/TESTING.md](docs/TESTING.md)). A suite that cannot run is skipped with a printed reason and counted; CI treats
  a skip as a failure.
- [docs/TESTING.md](docs/TESTING.md) says what each suite covers and which to run for which change.
  [docs/BUILDING.md](docs/BUILDING.md) builds the plugin, the launcher and the installer.
- Run the suites that cover what you changed before you open a pull request. Do not re-record a golden file to make a
  test pass; read [docs/TESTING.md](docs/TESTING.md) first.

## Branches, commits and pull requests

- Work on a named branch (`feature/<name>` or `fix/<name>`), never on `main`. Naming and commit style: [docs/GIT.md](docs/GIT.md).
- Open a pull request. A maintainer reviews it, CI (`tests`, `scan`) must be green, and it is squash-merged.
  Run the scan before you push: `python3 tools/release_scan.py --source . --skip-git --allow-no-private-config --strict --trust-public-rules --public-rules tools/release_scan.public.json`.
- Contributions to `plugin/` and `installer/` come with the permission in `LICENSE-ADDITIONAL-PERMISSION.md`; see [.github/CONTRIBUTING.md](.github/CONTRIBUTING.md).
- Commit under your own name and an email you are happy to have public. Never rewrite published history, force-push,
  or skip the checks (`--no-verify`).

## Hard rules

- **No Blizzard files.** Commit no client file, library, art, sound, decompiled code, screenshot of game art, or anything
  extracted from a client. Your own client's libraries stay in the ignored `references/` folder.
- **No money, of any kind.** Nothing here takes payment or earns income, and no feature is paid.
- **No servers.** Nothing in this project hosts a server, an online mode or matchmaking.
- Do not commit credentials, keys, tokens, account data, or personal paths and machine names.
- `private/`, `references/`, `evidence/` and the locally built data in `.gitignore` are never committed.
- Card data lives in `facts/`, one file per meta. Do not add data that came from a client.

## Where things are

`engine/` (rules, modified SabberStone, AGPL), `plugin/` (runs inside the game), `installer/` (launcher and setup),
`facts/` and `data/` (card facts, opponents, pools, shop, packs, progression), `tests/` and `sim/` (the public suites).
