# Contributing to SoloStone

Pull requests are welcome, and so are issues. SoloStone is a small project: a maintainer reads every pull request, but
not always quickly.

## Issues

When you open an issue, please include:

- the SoloStone version (the launcher window shows it) and your Hearthstone client version;
- what you did, what you expected, and what happened;
- for a card or rules bug, the card's name or ID and what it should do.

Please do not post: your Battle.net details, passwords, tokens, or anything from your account. Logs in
`%LOCALAPPDATA%\SoloStone\reports` can include your Windows user name and folder paths; remove them first.
Please do not attach Hearthstone files, screenshots of game art, or card art.

Security problems go to [SECURITY.md](SECURITY.md), not here.

## Pull requests

1. Fork the repository and branch from `main` (`feature/<name>` or `fix/<name>`; see [docs/GIT.md](../docs/GIT.md)).
2. Make one change per pull request. Say what it fixes or adds and why.
3. Run the tests that cover your change: `python3 tests/run.py` ([docs/TESTING.md](../docs/TESTING.md)). CI runs the
   same suites (with `--ci`), and the pull request needs them green.
4. Check the files you changed against the public rules before you push:
   `python3 tools/release_scan.py --source . --skip-git --allow-no-private-config --strict --trust-public-rules --public-rules tools/release_scan.public.json`.
   CI runs the same scan on the whole tree and on each commit ([scan.yml](workflows/scan.yml)), using the scanner and
   rules from `main`. The owner reviews changes to `.github/` and to the scanner files ([CODEOWNERS](CODEOWNERS)).
5. A maintainer reviews it, and it is squash-merged into `main`.

Do not include Hearthstone files, libraries, art, sound or decompiled code, in any form. A pull request that does is
closed, and the files must be removed from your branch history.

## Licence of your contribution

SoloStone is licensed under the GNU Affero General Public License version 3 ([LICENSE](../LICENSE)). A contribution
is licensed the same way.

Two folders also carry an additional permission: `plugin/` and `installer/` may be linked with the Hearthstone client
libraries and the mod loader, and the result conveyed, without those libraries being offered under the AGPL
([LICENSE-ADDITIONAL-PERMISSION.md](../LICENSE-ADDITIONAL-PERMISSION.md)). **By contributing to `plugin/` or
`installer/`, you give your contribution under the AGPL together with that additional permission.** Without it the
plugin could not be distributed for use with the closed client libraries. If you cannot agree to that, please open an
issue instead.

Files under `engine/` keep the licence of the engine they come from.

## Chat

[Discord](https://discord.gg/e45QgKHmC2)
