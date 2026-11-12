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
also the `client` tier when your client is set up (below). "The suites" lists the `ci` tier; "The client suites" lists the
`client` tier.

## Run them

For the `ci` tier you need no Hearthstone files.

| Need | Detail |
|---|---|
| .NET SDK | **10.0.402**. `global.json` pins it and refuses any other version. |
| Python | 3.10 or newer, on your `PATH` as `python3` (or `python` on Windows). The runner uses only the standard library. |
| Git | The installer build tools read the source revision, so use a Git checkout. |
| Mono | Only for the `fx-harmony` suite, which patches under Mono as the game does: `mono` on your `PATH` (Linux and macOS; on Windows the suite runs without it). Without it the suite is skipped, and CI fails on a skip. |
| Internet | Once, to restore NuGet packages. |

```text
python3 tests/run.py
```

It builds every suite in Release mode (the engine is built from `engine/`, never from a prebuilt DLL), runs it, and
prints one PASS or FAIL line per suite. The exit code is 0 only if everything passed. A first run takes about 15 to 20
minutes on a fast Mac (`engine-proof`, 5 to 6 minutes, and `launcher`, about 5 including its package restore, are the longest).

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

Three checks in the `ci` tier read your client's own library, so they skip on a machine without one and run on a machine
with one (see "The client tier"): the pinned section-loader code of the native shop (`shop`), the pinned sideboard tag
(`recipe-picker`) and the pinned sub-spell hooks (`fx-harmony`). Their `SKIP` line says what is missing.

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
(built locally from your client and never committed, such as the card export in `data/carddb/`) is missing. The summary
line counts every skip, for example `34 of 40 suites passed, 6 skipped (progression-standard-batches, engine-proof-client,
standard-native-client, native-bindings, native-match, transport-invariants)` on a machine without a client. A skip is
not a pass: look at the reason.

Besides the client libraries, some suites need local files that git ignores:

- `references/bepinex/BepInEx.dll` and `0Harmony.dll` (step 1 of [BUILDING.md](BUILDING.md)) for `native-match` and
  `native-bindings`; `native-match` also builds the plugin, which needs them.

#### Data built locally

These suites read data that is exported from a Hearthstone client and so is never committed (git ignores these paths):

| File | Read by |
|---|---|
| `data/carddb/37.0.0.254477/cards.json.gz` (the client's card export) | `engine-proof-client`, `native-match`, `transport-invariants`, `progression-standard-batches`; optional for `engine-proof` |
| `data/carddb/37.0.0.254477/set-overrides.json` | `engine-proof-client`, `progression-standard-batches` |
| `data/fx/empty-playeffect-37.0.0.254477.tsv` | `native-match` |

The tools that export these files from a client are not in this repository yet. Until they are, those suites are
**skipped on any machine that does not already have the files**, and the runner prints which file is missing. A skip is
not a pass: a change that touches what such a suite covers still needs the maintainer's run (see "Not here yet").

With a client set up, `fx-harmony` also builds the plugin (into `plugin/bin/`, and the engine into `engine/SabberStoneCore/bin` and `obj`; git ignores them) so that it can check the
compiled hooks against your client. That needs `references/bepinex/BepInEx.dll` and `0Harmony.dll` too (step 1 of
[BUILDING.md](BUILDING.md)); if they are missing, `fx-harmony` fails instead of skipping the check.

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
| `npc-world-distribution` | The mistake distribution of the opponent skill tiers: 200 seeded trials per tier on six fixtures stay inside their priced bounds (the `--passivity-distribution` run of the `npc-world` program, about a minute; the default `npc-world` run leaves it out) |
| `ai-audit` | The dated AI audit reproduction of 2026-10-31 that `ai` does not reach (`--audit C1`; the second audit case runs in `ai`) |
| `ai-golden` | Opponent AI decisions on recorded positions (lethal, replies, planning, mulligan) and the three golden decks |
| `ai-golden-audit` | The dated AI audit reproductions of 2026-10-31 for planning, replies, lethal and mulligan (the `ai-golden` program under `--audit`) |
| `parallel-groups` | The card lists behind each offline card pool, and the group proof checks |
| `tier-presentation` | The rank tier reward screen steps (with stand-in game classes) |
| `deck-ui` | Deck layout geometry, button chrome and handheld rules of the native screens |
| `projection` | How menu and match state is projected onto the native screens, and which zero-tag differences between the engine and the client count as equal |
| `ladder` | The ranked ladder profile code: results and rewards, milestones, ledger, placement and casual mode |
| `ladder-extended` | The older ladder-program checks, in the same program under `--extended`: the economy, pack and deck-library checks. The ledger, milestone-ledger and rating-placement checks run in `ladder`. Fixtures for saves no build wrote (rule-0 or rule-1 ranked history) are deleted |
| `recipe-picker` | The recipe picker and recipe guide of the native shop |
| `progression` | Profile, rewards, quests, achievements, ranks and settings logic behind the native screens (the program's no-argument run) |
| `progression-extended` | The older progression fixtures, in the same program under `--extended`: full premium fixtures and class rewards, the Core schedule reconciliation, Death Knight rules, hero drafts, test loaners and NPC records. Each check runs in this suite or in `progression`, never both |
| `progression-standard-batches` | The Standard test loaners and batch manifests against the client's card export (about 14,000 checks). `client` tier: it needs `data/carddb/<build>/cards.json.gz` and `set-overrides.json`, built from your client and never committed, and is skipped without them. It needs no client library |
| `shop` | The native shop: catalogue, purchases, deck tiles and art maths |
| `native-match-logic` | The native match checks that need no client: the opponent turn runner, presentation and memory, rematch, the quest bot, 36 showcase games, fair-view privacy, coin-flip control, debug scenario lane paths and files, the match event log budget, NPC pet seats and the controller admission invariant. Slices: `--npc-runner-only`, `--npc-presentation-only`, `--rematch-only`, `--quests-only`, `--showcase-only`, `--npc-pets-only`, `--coin-flip-only`, `--debug-scenario-only`, `--fair-view-only`, `--match-event-log-only`, `--controller-admission-only`; any other argument fails |
| `native-bindings-logic` | Plugin logic that needs no client: the crafting, matchmaking, rank, home, strings and status-routine hooks (built from extracts of the plugin sources), and the plugin source-text pins (achievement toast, reward UI and format lock code must contain, or must not contain, given text) |
| `fx-harmony` | The effect patches applied by the real Harmony library under Mono. Needs `mono` |
| `fire-fx-scene` | The fire effect scene helper |
| `dormant`, `kindred`, `reborn` | Engine rules for Dormant, Kindred and Reborn cards |
| `engine-proof` | Card-by-card engine proofs (effects, resolution, snapshots, definitions) and the engine rule proofs (Imbue, Dormant, Rewind, History, Location, Decision, pools), the canonical hash and replay policy checks, the Shadow of Demise trial recipes, the engine half of the pinned transport replays (`tests/transport-invariants/replays`: legal-move hashes at every step and, for the 8 replays that store one, the state/history/RNG fingerprint, on the pinned pool snapshot; a replay that drifts after an intended rules change cannot be renewed from CI: a maintainer with a client renews it through `transport-invariants`, after the same per-game review as a golden), and the opponent planner on a recorded turn-10 position. Without the card export built from a client, the check of each card against that export is skipped and printed, and the card's scenario still runs. See "Engine proofs and native flow checks" |
| `standard-native` | Native Standard-format flow checks that need no client: quest definitions, playability policy, loaners, definition audit, input trace binding |
| `goldens` | Recorded games replay to the same result on the current engine (see below) |
| `goldens-rerecord` | `goldens rerecord` restores only the changed games and leaves the rest of a golden file byte-identical |
| `fuzz` | 200 random games: no crash, the same result twice, no broken invariant |
| `launcher` | The launcher and installer logic. The runner builds the release launcher first, so its scans run. Some checks skip without a Hearthstone client, and so does the scan of the release plugin (that build needs the client's libraries; `installer/test-all.sh` builds and scans it) |
| `installer-python` | The installer build tools (`installer/stage.py`, `installer/loader_inputs.py`). Not on Windows: these tests use unix paths and tools, so the runner skips them there and says so |

## The client suites

These suites compile or load your client's libraries. Run one with `python3 tests/run.py --suite NAME`.
`engine-proof-client` and `standard-native-client`, the client halves of two `ci` suites, are in the next section.

| Suite | What it covers |
|---|---|
| `native-bindings` | The plugin's bindings: every client method it patches exists in your client libraries, the built plugin's references, and the pack hook IL. Its client-free checks are in `native-bindings-logic` |
| `native-match` | The native match: plugin match logic on recorded games, and the recipe editor of the built plugin. Its checks that need no client run in `native-match-logic` |
| `transport-invariants` | Engine-to-client transport: invariants over recorded and random games. It is the longest suite (about 45 minutes) |

Apart from the data built locally (above), what they read is in the repository: `tests/fixtures/playtest/` (recorded
matches), `research/`, and the card, pool, shop, progression and ladder files under `data/`. `transport-invariants` writes
its packet logs to `sim/out/transport/` (git ignores it).

## Engine proofs and native flow checks

`engine-proof` and `standard-native` each have two halves built from the same sources. The half that needs nothing from a
client is in the `ci` tier. The other half is a `client` suite of its own, run with `--client DIR`:

| `ci` suite | `client` suite | What the client half adds |
|---|---|---|
| `engine-proof` | `engine-proof-client` | Proofs that compare the engine with your client's data (the card definitions file, hero powers, the independent derivation of the offline pools, the printed class domains), and the native transport halves of 11 engine proofs (the same proof runs again with its packet checks compiled in). It also checks that `NativeMatch.TransportCardId` in the plugin equals its copy in `tests/shared/NativeMatchTransportId.cs`, which the suites without a client link instead of `NativeMatch` |
| `standard-native` | `standard-native-client` | The groups that build and send native packets: Trade, live snapshots, location projection, proof packet games |

`engine-proof-client` requires the export (it fails, never skips, a comparison). The identity comparisons of the whole card corpus run in
`engine-proof`, so a maintainer run needs `engine-proof` with the export present as well as `engine-proof-client`.

The card export (`data/carddb/37.0.0.254477/cards.json.gz`) is optional for `engine-proof`: with it, each card is also
compared with the export, exactly as before; without it, the run prints one `SKIP ...` line for each kind of
comparison and a `SKIPPED n x ...` total at the end. The comparisons that need the client itself (the independent pool derivation,
hero powers) are skipped in `engine-proof` even when the export is there, and run in `engine-proof-client`. Nothing is skipped silently. The client half builds with
`-p:ClientTransport=true`, which defines `CLIENT_TRANSPORT`, adds the client's libraries and links the plugin's native match.

## You changed X, run Y

| You changed | Run |
|---|---|
| A card script or any rule in `engine/` | `goldens`, `fuzz`, `cards`, `engine-proof`, `dormant`, `kindred`, `reborn`. The AI suites too, since opponents play on the engine: `ai`, `ai-golden`, `npc-world` |
| `facts/` (the card file) or the card pools in `data/pools/` | Everything. It changes what the engine loads |
| Opponent AI (`plugin/Npc/`, `plugin/Bot*.cs`, `plugin/LocalBot.cs`) | `ai`, `ai-audit`, `ai-golden`, `ai-golden-audit`, `npc-world`, `npc-world-distribution`, `native-match-logic`, `engine-proof` |
| Other plugin code `engine-proof` and `standard-native` compile (`plugin/Local*.cs`, `NativeUIProjection.cs`, `StandardProofNative.cs`) | `engine-proof`, `standard-native`, and your own judgement for the rest |
| Opponent decks, skills or personalities (`data/npc/`) | `npc-world`, `ai-golden`, `native-match-logic` |
| Shop, recipes or card mastery (`data/shop/`, `data/progression/`, `plugin/LocalShop.cs`) | `cards`, `recipes`, `shop`, `recipe-picker`, `progression` |
| The profile, rewards or economy (`plugin/Local*.cs`) | `progression`, `progression-extended`, `shop`, `ladder`, `ladder-extended`, `native-match-logic`, `native-bindings-logic` |
| Native screens (`plugin/Native*.cs`, `plugin/Deck*.cs`) | `projection`, `deck-ui`, `shop`, `fx-harmony` |
| Packs (`data/packs/`, `plugin/FairPack*.cs`) | `fair-packs`, `cards` |
| Native screens, the match flow or the client calls the plugin patches (`plugin/Native*.cs`, `plugin/client-bindings.txt`) | `native-bindings-logic`; with a client also `native-bindings`, `native-match`, `transport-invariants` |
| Ranked ladder (`plugin/LadderRules.cs`) or rank rewards | `ladder-rules`, `tier-presentation`, `ladder`, `ladder-extended` |
| The launcher, installer or their scripts (`installer/`) | `launcher`, `installer-python` |
| The shared test code (`tests/shared/`, `tests/run.py`) | Everything |

Test folders are named after their suite (`tests/ai-golden`, `tests/fair-packs`, `tests/dormant`, ...). Code that two suites compile
lives in `tests/shared/` (linked, never copied); test data that is not a suite's own is in `tests/fixtures/`.

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
change why each one changes. Re-recording just to make the test pass hides the bug it found.

To absorb an intended engine change, use `rerecord`:

```text
HsSim.dll goldens rerecord tests/goldens/core-2026.json --dry-run
HsSim.dll goldens rerecord tests/goldens/core-2026.json
HsSim.dll goldens rerecord tests/goldens/core-2026.json --only <game-id> [<game-id>...]
```

It replays every game (or just the `--only` ones) and re-records only the games that no longer match. Each one is run
again as the same case (suite, deck pair, seed, policy). Games that still match stay byte-identical, and games are never
added or removed. A record pins the card pool and the deck's id, not the deck's cards, so a changed deck list or card
shows up as a changed game, not as a quiet success.

Every re-recorded game is classified and printed:

- `shape only`: the actions and legal moves are the same (so turns and winner too) and only the history hash moved.
  Accepted without a flag.
- `STATE`: as above, but the final state hash moved. The legal-move hashes cover the options offered, not the board
  numbers, so a rules change that alters stats or tags without changing any offered option (most likely on the last
  turn) looks exactly like this, and a regression can hide in it. These are refused (exit 1, nothing written) unless
  you pass `--allow-state-change` (or `--allow-behaviour-change`, which implies it), which prints a warning per game.
  To review one, replay the game before and after (old engine/golden against the new build) and compare the final board:
  every minion's and hero's stats, tags and health must differ only where your change says they should.
- `BEHAVIOUR`: the game itself changed (different actions or legal moves, turns, winner, or a pool/Dark Gift version
  field). The line shows how many actions changed and where they first differ. These are refused (exit 1, nothing
  written) unless you pass `--allow-behaviour-change`, which prints a warning per game. A behaviour change needs a
  reviewer's eye on each listed game: say in your change why it changed.

`--dry-run` prints the same and writes nothing. Exit code 0 is success, 1 is a refusal or failure (a game could not be
re-recorded, a behaviour or state change without its flag, a re-recorded game that still does not match, or a file not in the
form `goldens record` writes); nothing is written then. 2 is a usage error.

Schema 3 files (`core-2026`, `offline-standard-pools-v1`) are re-run on the card pool their record pins. Schema 2 files
(`classic-source`, `modern`, `core-reprints`) record no pool, so their games are re-run unpinned, under the current
pool and Dark Gift rules, and stored in the schema 2 form. They replay under the legacy rules, so the re-check is what
guards them: a re-recorded game is replayed from the text that would be written, and nothing is written if any one still
mismatches. Rerecord also refuses a case that can no longer be built or whose identity (id, decks, seed, policy)
changed. It prints `N re-recorded (S shape only, T state, B behaviour), M unchanged, all K match`.

`goldens record` is for **creating a new suite**. It regenerates the whole file from the current deck list and card pool
and replaces it: every game can change and games for newly added decks appear. Do not use it to absorb one engine
change. The commands that made the current files are:

```text
HsSim.dll goldens record tests/goldens/classic-source.json --suite classic --seeds 0..4 --policy random
HsSim.dll goldens record tests/goldens/modern.json --suite modern --seeds 0..9 --policy random
HsSim.dll goldens record tests/goldens/core-reprints.json --suite core-reprints --seeds 0..2 --policy random
HsSim.dll goldens record tests/goldens/core-2026.json --suite core-2026 --seeds 0..1 --policy random
HsSim.dll goldens record tests/goldens/offline-standard-pools-v1.json --suite offline-standard-pools-v1 --seeds 1..3 --policy random
```

`HsSim.dll` is the file built above. Recording also saves the card pool it used in `tests/goldens/pools/` (the `.xml`
and its `.xml.sha256`); commit both with the golden. `rerecord` never adds a pool.

## Not here yet

Some test suites are not in this repository. The `client` tier runs on your machine only; GitHub cannot run it, and this
repository ships no client file.

- **The offline guard and save-flow tests.** They spell out the paths of the maintainer's test machine, so they stay
  private. The profile, rewards and achievements logic is in the `progression` suite above.
- **Checks against maintainer-only files.** A few checks in `progression` and `projection` compare the plugin with the
  maintainer's lab scripts and notes. Without those files they print `SKIP ... maintainer-only` and the suite still passes.
- **Live tests.** These drive a real game on a real test machine: clicks, drags, matches and installs. They need that
  machine and are not in this repository.
- **Generator tests.** The tools that make the card file and the pack tables from the game's own data are private,
  and so are their tests.

- **Checks you switch on by hand.** Three checks stay out of the runner on purpose. `GOLDEN_CALIBRATE=1` makes the `ai-golden`
  program play the headless calibration games of every opponent profile and fail if a
  turn's 95th-percentile think time passes 1.5 seconds, so its verdict depends on the speed of the machine.
  `NPC_ENGINE_NETSTANDARD_OUTPUT=<path of a netstandard2.1 build of the engine>` adds one check that the client's engine
  build carries the same source stamp; it needs a second engine build. The `ai-golden` program's `--lethal-benchmark`,
  and the report and renew options of `npc-world` and `ladder-rules`, are measuring tools with no pass or fail.
- **Replay fixtures for the maintainer's AI tool.** The ten position files and ten match journals in `tests/ai/` are
  inputs to `tools/hs ai hard-fixture`, which is not in this repository; nothing here reads them. Each position
  file holds the public state, the seed and an `expect` block (allowed, forbidden and required moves), so a runner for
  them would be a real check: it is not written yet.
- **`tests/goldens/standard-spike1.json`**, when it arrives with the Standard engine proofs, is replayed inside
  `engine-proof` and is on purpose not in the `goldens` suite.

A change you make here is checked by the suites above, but not by those. The maintainer runs them before a release.
If your change touches the plugin's native code, say so in your pull request.
