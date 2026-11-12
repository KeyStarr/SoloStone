# transport-invariants

"Transport" is the stream of messages `NativeMatch` sends to the client to describe a game. This suite plays many games
and checks that the client would end up with the right picture after every step. It is the longest suite (about 45
minutes).

## How it works

1. **A recorded game.** A replay file holds the two decks, the seed, the list of chosen move numbers and a hash of the
   legal-move list at each step. Games are seeded random bot games, not human games. "How a recorded game works" in the
   [testing guide](../../docs/TESTING-GUIDE.md) explains the record and replay steps, which goldens share.
2. **A fake client.** `NativeMatch` sends its messages to a fake client session (`NativeSession.cs`, `ClientModel.cs`)
   that keeps the picture a real client would build from them.
3. **A comparison after every step.** `Invariants.Compare` checks the engine's state against the fake client's picture:
   zones, hidden cards and the tags the screen needs; `Invariants.CompareMana` does the same for mana. Any difference is a violation; `--strict` makes a violation
   fail the run. Other checks (`HistoryShape.cs`) look at the shape of the messages themselves, such as privacy leaks and
   effects split across blocks.

The suite also holds focused proofs for single mechanics (Rewind, Imbue, Locations, Dark Gift, quests, trades, ...)
and many self-checks of the checker.

## Replays

`replays/` holds games pinned so that a fixed bug stays fixed. Each file stores its decks, so changes to the deck
lists in the registry do not change an old replay. The step-by-step ones also pin the card pool; the `transport-debt`
games do not (they are replayed from the seed on the current pool).

| Folder | Guards |
|---|---|
| `meta-pre-be/` | Six games that broke the engine, the client picture or the message shape when the card pool was cut back to the earlier card meta: a Secret activated twice (`ice-barrier`, an engine crash), a held card that transforms (`TIME_876e`), a held card that changes form and keeps its earlier buffs (`CORE_BT_416`, `EDR_226`, `TIME_704`), and a Deathrattle of an exhausted Location (`TLC_100t3`). |
| `private-return/` | Ten games for hidden-card privacy: a card that returns anonymously after being burned, and the client's picture after the full-game reset message. The client must never learn a card it has no right to see |
| `transport-debt/` | Three games (`helper-accepted`, `runtime`, `rewind-actor`) that once left the fake client out of step with the engine. They store no action list: each is replayed from its seed and must finish with no violation. `rewind-actor` must also go through at least one Rewind and restore the discarded timeline |
| `deathrattle/` | One game that checks that an empty Deathrattle, a granted Deathrattle and Lifesteal stay distinct in the messages, and that its history shape matches the accepted baseline |

17 of these replays (`meta-pre-be`, `private-return`, `deathrattle`) also run their engine half in the `engine-proof`
suite, which needs no client: legal-move hashes and chosen moves at every step, and a state, history and random-number
fingerprint for the 8 that store one. A replay that drifts after an intended rules change cannot be renewed from CI. A
maintainer with a client renews it here, after the same per-game review as a golden (see "Goldens" in
[TESTING.md](../../docs/TESTING.md)).

## Running

`python3 tests/run.py --suite transport-invariants --client "/path/to/Hearthstone"`. It needs the card export built
locally from a client (`data/carddb/<build>/cards.json.gz`), so it skips on most machines (see "Data built locally" in
[TESTING.md](../../docs/TESTING.md)). Packet logs go to `sim/out/transport/`, which git ignores. Slices (run the built
`TransportInvariants.dll` directly; `run.py` passes no arguments): `--meta-pre-be-only [name]`, `--private-return-only`, `--deathrattle-replay-only`, `--transport-debt-only [name]`.
