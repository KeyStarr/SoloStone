# native-match

"Native" means the real client's own screens and messages. This suite checks what the plugin **tells the client** when
the engine does something, and how the plugin handles what the client sends back.

## What it tests

- **Outgoing messages.** The engine plays a game, `NativeMatch` turns it into the client's own messages (entities, tag
  changes, zone moves, hidden cards, effects to show), and the checks look at those messages: the right card shown, the
  right tags, nothing hidden shown, in the right order.
- **Incoming input.** The plugin's handling of what the client sends: choices (Discover and similar), targets, mulligan
  picks, and plays and attacks.

## What it does not test

Rendering. Nothing here draws a frame or clicks a real button. Whether the screen looks right, animates and answers
clicks is checked only by live tests on a real machine, which are not in this repository (see "Not here yet" in
[TESTING.md](../../docs/TESTING.md)).

## Why it needs your client

The suite compiles against the client's real message and entity types (`references/game/`). A hand-written stand-in
would only be our own guess of their shape, and copying Blizzard's types into the repository is not allowed. So it is a
`client` tier suite: run it with `python3 tests/run.py --suite native-match --client "/path/to/Hearthstone"`. It also
needs BepInEx (it builds the plugin) and data built locally from a client that the public tools cannot make yet; the
runner prints what is missing and skips. Commands, requirements and numbers are in [TESTING.md](../../docs/TESTING.md).

## Main groups

- **Powered-up glow** (`PoweredUpGlowTests*.cs`, `powered-up-inventory.tsv`, `powered-up-inventory/`). A card in hand
  glows when its bonus condition is met (combo, outcast, and many card-specific ones). These tests check that the glow
  tags go out at the right moments. The TSV files list every live card identity with a class code (a pre-play condition kind, or none). The suite fails
  on a duplicate, stale, unclassified or uncovered entry (see the README in `powered-up-inventory/`).
- **Card mechanics on screen** (`*History.cs`, `*NativeProof.cs` and similar). One file per mechanic or card family
  (Discover, Rewind, Secrets, shuffles, Locations, hero powers, Imbue, ...). Each plays it on the engine and checks the
  messages that reach the client.
- **Opponent turn presentation** (`Npc*Tests.cs`, `AiSequencingTests.cs`, `AiRecoveryTests.cs`). How an opponent's turn
  is shown: its actions, pauses, pets, memory and premium cosmetics.
- **Match flow** (`CoinFlipTests.cs`, `StartUniverseTests.cs`, `ResultSettlementTests.cs`, `PlayerTurnTimerTests.cs`,
  cosmetics and recipe-editor tests, and others). Start, coin flip, turn timer, concede and result settlement.

## Where the fixtures come from

- Games are played on the engine from seeds and decks written in the test code. `fixtures/invisible-greenwing.json` is
  one stored game (seed, decks and pool hash).
- `powered-up-inventory.tsv`, `powered-up-inventory/` and `entity-text-builders.tsv` are committed lists. The text
  builder list was made from the client's card data with a tool that is not in this repository.
- Some checks read data built locally from a client and never committed: the card export `data/carddb/<build>/cards.json.gz`
  and `data/fx/empty-playeffect-<build>.tsv` (see "Data built locally" in [TESTING.md](../../docs/TESTING.md)).

## Related

- The checks of the native match that need no client are in the `native-match-logic` suite (turn runner, rematch,
  quest bot, showcase games, fair-view privacy, coin-flip control, event log budget, pet seats). Some slices moved there;
  asking `native-match` for one fails with a message.
- This project still references the `transport-invariants` project, to reuse a handful of its helpers, so building one
  builds the other. That is open as [issue #55](https://github.com/KeyStarr/SoloStone/issues/55).
