# Testing SoloStone

SoloStone is an unofficial offline single-player mod for Hearthstone®. It requires your own legally obtained Hearthstone client. Not affiliated with, endorsed by, or sponsored by Blizzard Entertainment. Hearthstone is a trademark or registered trademark of Blizzard Entertainment, Inc.

This page says which tests are in this repository, how to run them, and which ones are not here yet. Run every command
from the repository root.

## Two tiers

| Tier | Needs | Where it runs |
|---|---|---|
| `ci` | Nothing from Hearthstone. Every suite builds from this repository alone. | Your machine and GitHub, with the same command |
| `client` | **Your own Hearthstone client**: the suite loads its libraries, and some also need data built locally from it | Your machine only. GitHub cannot run it, and this repository ships no client file |

`python3 tests/run.py --list` shows each suite with its tier. A plain `python3 tests/run.py` runs the `ci` tier, and
also the `client` tier when your client is set up (below). Every suite listed under "The suites" is in the `ci` tier.

## Run them

For the `ci` tier you need no Hearthstone files.

| Need | Detail |
|---|---|
| .NET SDK | **10.0.402**. `global.json` pins it and refuses any other version. |
| Python | 3.10 or newer, on your `PATH` as `python3` (or `python` on Windows). The runner uses only the standard library. |
| Git | The installer build tools read the source revision, so use a Git checkout. |
| Internet | Once, to restore NuGet packages. |

```text
python3 tests/run.py
```

It builds every suite in Release mode (the engine is built from `engine/`, never from a prebuilt DLL), runs it, and
prints one PASS or FAIL line per suite. The exit code is 0 only if everything passed. A first run takes about six
minutes; the launcher suite is the longest, mostly because of its package restore.

| Option | What it does |
|---|---|
| `--list` | Lists the suites and what each covers |
| `--suite NAME` | Runs only that suite. Repeat the option for more |
| `--no-build --artifacts DIR` | Runs again without building. `DIR` is the folder of an earlier run with `--artifacts DIR` |
| `--artifacts DIR` | Keeps the build output and one log per suite in `DIR` (the default is a temporary folder) |
| `--dotnet PATH` | Uses this `dotnet` program (the default is `DOTNET`, then `PATH`) |
| `-v` | Shows each suite's output while it runs |

GitHub runs the same suites with `python3 tests/run.py --ci` on every push to `main`, on every pull request, and when a
maintainer starts it by hand ([tests.yml](../.github/workflows/tests.yml)). With `--ci` the runner leaves the `client`
tier out and **a skipped suite fails the run**, so a whole suite cannot silently stop running in CI. (A suite can still
skip single checks inside itself, as `launcher` does without a client; those show in its own output.)

### The client tier

```text
python3 tests/run.py --client "/path/to/Hearthstone"
```

`--client` takes your Hearthstone folder, or its `Managed` folder. It copies the libraries the plugin references (the
13 files in step 1 of [BUILDING.md](BUILDING.md)) into `references/game/`, which git ignores, then runs everything.
Nothing is committed and nothing leaves your machine. Later runs find the copied files, so `--client` is needed only
after your client changes. Use the client version named in `installer/tested-client.json`; another one can fail
in ways that are not bugs.

A `client` suite is **skipped, with the reason printed**, when `references/game` is incomplete or when data it needs
(built locally from your client and never committed) is missing. The summary line counts every skip, for example
`16 of 17 suites passed, 1 skipped (installer-python)`. A skip is not a pass: look at the reason.

| Option | What it does |
|---|---|
| `--client DIR` | Copies your client's libraries into `references/game`, then runs all suites |
| `--ci` | What GitHub runs: only the `ci` tier, and any skip fails the run. Cannot be combined with `--client` |

## The suites

| Suite | What it covers |
|---|---|
| `cards` | Card catalog and pool counts, rarity rules, and card scenarios on the engine (damage, auras, deathrattles, secrets) |
| `fair-packs` | Pack opening: fair rolls, pity rules and the frozen pack results |
| `ladder-rules` | Ranked ladder rules (stars, floors, rewards) and a simulated climb through a synthetic opponent world |
| `recipes` | The shipped shop deck recipes validate and follow the recipe rules |
| `npc-world` | Opponent decks, skills, personalities, matchmaking and the opponent population |
| `ai` | Opponent AI fairness: it may not see hidden information |
| `ai-golden` | Opponent AI decisions on recorded positions (lethal, replies, planning, mulligan) and the three golden decks |
| `parallel-groups` | The card lists behind each offline card pool, and the group proof checks |
| `tier-presentation` | The rank tier reward screen steps (with stand-in game classes) |
| `fire-fx-scene` | The fire effect scene helper |
| `dormant`, `kindred`, `reborn` | Engine rules for Dormant, Kindred and Reborn cards |
| `goldens` | Recorded games replay to the same result on the current engine (see below) |
| `fuzz` | 200 random games: no crash, the same result twice, no broken invariant |
| `launcher` | The launcher and installer logic. Some checks skip without a Hearthstone client |
| `installer-python` | The installer build tools (`installer/stage.py`, `installer/loader_inputs.py`). Not on Windows: these tests use unix paths and tools, so the runner skips them there and says so |

## You changed X, run Y

| You changed | Run |
|---|---|
| A card script or any rule in `engine/` | `goldens`, `fuzz`, `cards`, `dormant`, `kindred`, `reborn`. The AI suites too, since opponents play on the engine: `ai`, `ai-golden`, `npc-world` |
| `facts/` (the card file) or the card pools in `data/pools/` | Everything. It changes what the engine loads |
| Opponent AI (`plugin/Npc/`, `plugin/Bot*.cs`, `plugin/LocalBot.cs`) | `ai`, `ai-golden`, `npc-world` |
| Opponent decks, skills or personalities (`data/npc/`) | `npc-world`, `ai-golden` |
| Shop, recipes or card mastery (`data/shop/`, `data/progression/`, `plugin/LocalShop.cs`) | `cards`, `recipes` |
| Packs (`data/packs/`, `plugin/FairPack*.cs`) | `fair-packs`, `cards` |
| Ranked ladder (`plugin/LadderRules.cs`) or rank rewards | `ladder-rules`, `tier-presentation` |
| The launcher, installer or their scripts (`installer/`) | `launcher`, `installer-python` |
| The shared test code (`tests/shared/`, `tests/run.py`) | Everything |

If you are not sure, run everything.

## Goldens

A golden file holds a set of recorded games. Each game is a list of action numbers ("take legal move 28, then 3,
then 0, ...") plus a hash of its full event history and a hash of its final state. The golden files contain no card
data and no game text.

`goldens` replays each recorded game on the current engine and compares both hashes. If one differs, some rule now
does something different in that game. That is either a bug you just introduced or a deliberate rules change.

The five files are in `tests/goldens/`:

| File | Games | Covers |
|---|---|---|
| `classic-source.json` | 320 | Every pair of the eight starter decks, five seeds each |
| `modern.json` | 40 | The modern-mechanics proof decks |
| `core-reprints.json` | 12 | Cards reprinted into Core |
| `core-2026.json` | 18 | The 2026 Core set |
| `offline-standard-pools-v1.json` | 12 | The offline Standard card pools |

The last two also need a frozen copy of the card pool they were recorded with, in `tests/goldens/pools/`. Replays use
that copy, so a later pool edit cannot change an old game.

To check one file by hand, build the simulator and run it:

```text
dotnet build sim/HsSim/HsSim.csproj -c Release --artifacts-path /tmp/hssim -p:NuGetAudit=false
dotnet /tmp/hssim/bin/HsSim/release/HsSim.dll goldens check tests/goldens/modern.json
```

**Only re-record a golden when you meant to change the rules.** Look at every game that changed first, and say in your
change why each one changes. Re-recording just to make the test pass hides the bug it found. The commands that made the
current files are:

```text
HsSim.dll goldens record tests/goldens/classic-source.json --suite classic --seeds 0..4 --policy random
HsSim.dll goldens record tests/goldens/modern.json --suite modern --seeds 0..9 --policy random
HsSim.dll goldens record tests/goldens/core-reprints.json --suite core-reprints --seeds 0..2 --policy random
HsSim.dll goldens record tests/goldens/core-2026.json --suite core-2026 --seeds 0..1 --policy random
HsSim.dll goldens record tests/goldens/offline-standard-pools-v1.json --suite offline-standard-pools-v1 --seeds 1..3 --policy random
```

`HsSim.dll` is the file built above. Recording replaces the file. It also saves the card pool it used in
`tests/goldens/pools/` (the `.xml` and its `.xml.sha256`); commit both with the golden.

## Not here yet

Some test suites are not in this repository.

- **Suites that need your own Hearthstone client** (the `client` tier above). They load the game's own libraries to
  check the plugin's native screens, hooks and message handling: the engine proofs per card, native match and shop
  flows, profile, progression, ladder and pack screens, and the checks that every client call the plugin binds to still
  exists. The runner already supports them (`--client`); the suites themselves are added in a later change. This
  repository ships no client file, and CI cannot run them.
- **The tests for the plugin's game profile and offline guard.** They spell out the paths of the maintainer's test
  machine, so they stay private for now. Part of them (achievements and rewards) could move out into a public suite later.
- **Live tests.** These drive a real game on a real test machine: clicks, drags, matches and installs. They need that
  machine and are not in this repository.
- **Generator tests.** The tools that make the card file and the pack tables from the game's own data are private,
  and so are their tests.

A change you make here is checked by the suites above, but not by those. The maintainer runs them before a release.
If your change touches the plugin's native code, say so in your pull request.
