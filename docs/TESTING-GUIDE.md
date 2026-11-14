# How SoloStone is tested

A short guide: why the tests exist, how they are built, how to use them and how to keep them healthy. Commands,
options, timings and the full list of suites are in [TESTING.md](TESTING.md).

One word first: **native** means the real Hearthstone client's own screens and messages.

## What the tests protect

| Part | What breaks | Caught by |
|---|---|---|
| The engine (`engine/`) plays the game | A card or rule does the wrong thing | Card scenarios, engine proofs, recorded games |
| The plugin (`plugin/`) runs your profile, shop, ladder, rewards and opponents | Wrong gold, ranks, rewards or packs; an unfair or weak opponent | Logic suites and AI suites |
| The bridge: the plugin hooks the real game and tells it what to show | A Hearthstone patch removes a method we hook; the screen shows something the engine did not do | The client suites |
| The launcher and installer (`installer/`) | A broken install, launch or client check | `launcher`, `installer-python` |

## Architecture

```text
            ┌──────────── ci tier: GitHub and your machine, no Hearthstone files ────────────┐
  engine    │ cards, dormant, kindred, reborn, engine-proof    card and rule scenarios        │
  games     │ goldens, goldens-rerecord, fuzz                  recorded and random games      │
  plugin    │ progression, ladder, shop, fair-packs,           plugin logic, fake surroundings│
            │ native-match-logic, native-bindings-logic, ...                                  │
  opponents │ ai, ai-golden, npc-world, and their audits       fair play, decisions, decks    │
  install   │ launcher, installer-python                                                      │
            └─────────────────────────────────────────────────────────────────────────────────┘
            ┌──────────── client tier: your machine only ─────────────────────────────────────┐
  bridge    │ native-bindings, native-match, transport-invariants,      load your client's    │
            │ engine-proof-client, standard-native-client               libraries             │
  data      │ progression-standard-batches       reads card data built locally from a client  │
            └─────────────────────────────────────────────────────────────────────────────────┘
                         one command for both: python3 tests/run.py
```

The ideas behind it:

- **Real code, fake surroundings.** A suite compiles the plugin's own `.cs` files (its project in `tests/` lists them)
  together with **stand-ins**: small hand-written fakes of the Unity, BepInEx and game types that code names. A stand-in
  has only what the plugin calls, and its drawing calls do nothing. The shared ones are in `tests/shared/`; some suites
  have their own (for example `tests/recipe-picker/GameStandIns.cs`, `tests/tier-presentation/NativeBoundaries.cs`).
  Nothing checks a stand-in against the real client, so a stand-in can be wrong without any test noticing.
- **The engine builds from source.** `run.py` passes `-p:EngineSource=true`, so every suite uses `engine/` as you
  changed it. Many projects fail without it, because they look for a prebuilt DLL that is not in the repository.
- **Recorded games pin behaviour.** Goldens replay recorded games and must reproduce the same moves, options and final
  state; fuzz plays random games looking for crashes and broken rules. See the next section and "Goldens" in
  [TESTING.md](TESTING.md).
- **The client tier tests the bridge.** These suites compile against or load your own client's libraries, so they can
  check what stand-ins cannot: that the hooked methods exist, that the plugin's match code works against the real
  types, and that every message to the client is right (zones, hidden cards, Secrets, Rewind). Two suites,
  `engine-proof` and `standard-native`, are built twice: once for each tier ([TESTING.md](TESTING.md), "Engine proofs
  and native flow checks"). `native-match` and `native-bindings` have client-free twins that hold their checks that need
  no client (`native-match-logic`, `native-bindings-logic`). Short READMEs sit next to the suites:
  [native-match](../tests/native-match/README.md), [transport-invariants](../tests/transport-invariants/README.md) and
  [native-bindings](../tests/native-bindings/README.md).
- **No Blizzard files in the repository.** Client libraries are copied into `references/game/` on your machine (git
  ignores it). Card data built from a client is never committed.

### How a recorded game works

Goldens and the transport replays (`tests/transport-invariants/replays/`) record games the same way:

1. **Start.** A record names the two decks, a seed and the card pool. The pool is frozen: schema 3 goldens and the
   step-by-step transport replays keep a pool snapshot or its hash (older goldens record no pool), so a later pool edit
   cannot change an old game. The three `transport-debt` replays store only decks and seed, and are played fresh.
2. **Each step.** The engine lists the legal moves in a fixed order. Recording saves the number of the chosen move
   (its index in that list) and a hash of the whole move list.
3. **Replay.** The replay rebuilds the start, lists the moves again and compares the hash with the recorded one. If
   they differ, it stops at that step: the rules changed there. If they match, it plays the recorded index.
4. **End.** A golden compares a hash of the options and choices offered at every step, and its final state hash, with
   the recorded ones. A transport replay compares its step count and findings, and, for the 8 that store one, a
   fingerprint of the final state and random-number state. Neither pins the rest of the event history: every
   presentation fix moved that hash in most games, so all engine changes conflicted on it. The history-shape rules and
   the client picture in `transport-invariants`, and the card-level history proofs, check it instead.

Most recordings are seeded random bot games (the bot picks a random legal move), not human games. That makes them cheap
to make and to repeat, and they reach odd cases a person would not play. They are not a check of good play.

### What a suite looks like

Each suite is a small console program that calls `Check(condition, "what should be true")` many times. In most suites
the first false check stops the suite; a few collect every failure and fail at the end. `tests/run.py` builds each
suite, runs it and prints PASS, FAIL or SKIP with the reason, plus the end of a failing suite's output. Known limits,
with open issues: no standard test framework, and one test is not one concern
([#45](https://github.com/KeyStarr/SoloStone/issues/45)); some suites copy plugin code at build time or check source
text instead of calling a module ([#44](https://github.com/KeyStarr/SoloStone/issues/44)).

## Use

- **Before you send a change**, run the suites it touches ("You changed X, run Y" in [TESTING.md](TESTING.md)), or
  everything. To dig into one suite: `python3 tests/run.py --suite NAME -v --artifacts DIR` shows its output and keeps
  its log.
- **On every pull request** GitHub runs the whole `ci` tier and fails on any failure or skipped suite. A red check is a
  real failure until shown otherwise. The check's output holds the end of the failing suite's output; GitHub keeps no
  other log.
- **With a client**, `--client "/path/to/Hearthstone"` adds the client tier. What a contributor can really run today:
  - `native-bindings` (it also needs BepInEx, [BUILDING.md](BUILDING.md) step 1) and `standard-native-client`;
  - the other client suites (`native-match`, `transport-invariants`, `engine-proof-client` and
    `progression-standard-batches`) need card data exported from a client with tools that are not public yet. They skip,
    with the reason, on every machine except the maintainer's.

  Changes to the native screens, the match flow or the hooks still get the maintainer's full client run, and live
  tests on a real machine, before release.
- **A skip is not a pass.** Read the reason.

## Maintenance

| When | Do |
|---|---|
| You add a check | Put it where the suite's normal run reaches it, then prove it bites: break the code it guards, watch it fail, undo. A check behind an early `return`, or in a file nobody calls, never runs, which is worse than no check |
| You add a suite | A project under `tests/<name>/`, one `Suite(...)` line in `tests/run.py`, a row in both tables of [TESTING.md](TESTING.md) ("The suites" and "You changed X, run Y"). A `ci` suite must pass on Ubuntu with no client and skip nothing: CI picks it up on its own and fails on a skip. Mark client needs with `needs_client` / `needs_local` |
| The plugin starts using a new Unity, BepInEx or game member | Add only that member to the stand-in the suite compiles with (there are several). Get its shape from the real client: no test compares the two |
| You add or remove a Harmony hook | Regenerate `plugin/client-bindings.txt` (next section). Nothing in CI does it |
| A rules change changes a golden | Re-record only when the change is meant, and say why each changed game changes ([TESTING.md](TESTING.md), "Goldens") |
| A transport replay drifts | Same review as a golden. A maintainer with a client renews it; the re-record tool for these replays is not public yet ([transport-invariants](../tests/transport-invariants/README.md)) |
| A new Hearthstone version | Maintainer work today: the client build is pinned in `installer/tested-client.json` (embedded in the launcher), in `tests/run.py`, in many test files and in the locally built data, whose export tools are not public. `native-bindings` and `transport-invariants` are the first suites to run |
| A test needs a new file built from the client | Add it to "Data built locally" in [TESTING.md](TESTING.md) and to the suite's `needs_local`, so machines without it skip with the reason instead of failing |

### Client bindings list

At startup the plugin checks that the running client still has every method it hooks or calls. The list it checks is
`plugin/client-bindings.txt`. It is generated, not written by hand. The generator is `tool bindings` in
`installer/SoloStone.Launcher.Tests` (`ToolMode.cs`, `BindingScan.cs`): it reads the built plugin, finds every client
member it refers to (compiled references and reflection lookups) and looks each one up in `references/game`. It needs
your client's libraries in `references/game` and a built plugin. The committed list comes from the public installer
build (`-p:Installer=true`, which produces `SoloStone.Plugin.dll`): the default build also compiles lab-only patches
that have no rows, and a `launcher` check fails if they appear. Run it as
`dotnet run --project installer/SoloStone.Launcher.Tests -- tool bindings --references references --manifest plugin/client-bindings.txt --extra plugin/client-bindings.extra.txt PLUGIN.dll... --write`.
Without `--write` it only compares the file with what the builds need. No suite and no CI step runs either form against the real plugin. The `launcher` suite only spot-checks the
committed file (some required rows present, lab-only rows absent, and with a client the shipped rows hold), so a stale
list can stay green until someone regenerates it. Add or remove a hook, regenerate, and commit the file.

Keep results deterministic: fixed seeds, no network, and no result that depends on the clock. A test that sometimes
fails gets fixed or removed, never re-run until green.
