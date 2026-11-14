# Legacy invocation baseline

These four text fixtures were recorded **before the PR 3 transport existed**, from
`TriggerManager.cs` at `a3266db4f713215fe7076e53a0e1b8a5d28221ec` (file SHA-256
`b0f002f6b5b0bcde3c5fab6445a0cb17395026dc48d48f6f8b43a18447ed5c52`).
They are checked fixtures, not regenerated expectations.

On 2026-10-20, only the four `history=` hashes were renewed for spell TARGET/DAMAGE
metadata and public deck-destruction reveals. Comparing baseline and changed engines
preserved every history entry in order, with only those annotations added; callback
traces and final states matched exactly for all four games and their clones. The
archived `clone=` hashes remain unchanged and are excluded by `LegacyContract`.

On 2026-10-21, only the four `history=` hashes were renewed for optional retail
block fields, instance-local trigger/deathrattle headers, and pre-move public
reveals. All four callback traces matched byte-for-byte with the history/clone
hash lines excluded. A separate simulator audit replayed all 405 source golden
games with identical actions, legal-task hashes, final states, winners and turns;
only history hashes changed. Archived `clone=` hashes remain unchanged.

Recording used temporary instrumentation at each original delegate invocation:

```csharp
void RecordLegacy(string channel, TriggerHandler snapshot, IEntity sender)
{
    if (snapshot == null) return;
    if (LegacyTrace == null) { snapshot(sender); return; }
    int order = 0;
    foreach (TriggerHandler handler in snapshot.GetInvocationList())
    {
        LegacyTrace(channel, handler, order++);
        handler(sender);
    }
}
```

Each `X?.Invoke(sender)` (and the non-null Zone/Overload calls) was temporarily
replaced with `RecordLegacy(nameof(X), X, sender)`. GameStart retained its null
sender. Existing Zone wrappers and Overload metadata assignments were untouched.
`LegacyTrace` was a temporary manager property bound to `EventTraceProof.Recorder`.
No bus or production adapter participated. The temporary instrumentation and
fixture-writing switch were removed after recording.

`EventTraceProof.Run` defines decks, action ordering, policy seeds (1, 19, 42,
123), and the clone boundary after action 30. Each line contains channel,
zero-based invocation-list position, callback target identity/method, and owning
entity fields. Identity is assigned on first observation using reference equality;
entity IDs distinguish otherwise identical card handlers. `--- CLONE ---` starts
an independently continued exact clone with its own recorder. This compares old
clone behavior with new clone behavior, without asserting known-broken source/clone
parity. The last two lines hash full canonical histories plus final entity states.

Both the instrumented legacy engine and the new engine ran these games with tracing
enabled and disabled; histories/states matched. Normal verification compares the
complete recorded text byte-for-byte. The separate dispatch proof covers all 28
channels, typed payloads, null sender, nested calls, duplicate/compound subscriptions,
removal during dispatch, callback exceptions, Zone task isolation, and nested
Overload metadata. Its 200 mutation seeds per trace mode compare against actual
C# multicast delegates, not another implementation of the bus algorithm.

Run from the repository root:

```sh
python3 tests/run.py --suite engine-proof
```

On a restricted sandbox, set `NUGET_HTTP_CACHE_PATH` to a writable temporary
directory. No production dependency or warning policy changes are required.

On 2026-10-21, only the four `history=` hashes were renewed for shared damage,
healing, target ranges, attack markers, fatigue and source sub-block annotations.
The simulated actual traces matched every callback line for all four games and
their clones; archived `clone=` hashes remain unchanged. The separate golden
audit compared 405 games against HEAD with zero non-history differences.

On 2026-10-21, the merge of history batches A (`ba8add9`) and B (`655a806`)
renewed only the four `history=` hashes. Regenerated callback traces (including
clone callbacks) match `main`; all archived `clone=` lines remain unchanged.
The six golden suites retain their schemas and ordered rosters: a per-field audit
of all 405 games against `main` found zero non-history differences, including
42,547 actions and 42,547 legal-move hashes. Recording temporarily replayed each
existing fixture through `goldens record`; the recorder source was then restored.
Queued and immediate TRIGGER blocks do not emit header TARGET; deathrattle AoE
also retains TRIGGER without POWER target ranges. Kronn contributors and inline
foreign-source wrappers explicitly use EffectIndex=-1 with blank EffectCardId
to attribute hits without selecting the contributor card's own trigger effect.

On 2026-10-21, resolved random/selected TRIGGER target occurrences and keyworded
Poisonous destruction (including combat) renewed three `history=` hashes.
Regenerated callback lines, including clone callbacks, match `main` byte-for-byte;
archived `clone=` hashes remain unchanged. The six source golden suites were
audited against `main`: all 405 games, 42,547 actions and 42,547 legal-move hashes
preserve every non-history field. Four suites needed new simulator-recorded
history hashes; the other two were unchanged. Task selections distinguish
resolved TRIGGER targets from event headers and fixed-set IncludeTask/AoE.

Reviewer follow-up records custom resolved selections for Gorishi Colossus,
Diabolus Rex, Kronn contributors and Wisp missiles. Cannonmaster's direct damage
loop already uses RandomTask; no registered custom healing loops were found.
All 405 source golden games were replayed through the simulator, checking all
42,547 action indices and legal-move hashes. Against `main`, every non-history
game field remains identical; suite-level `engineSha` build provenance is excluded.
Four regenerated event traces match every callback line against `main`, preserve
archived `clone=` lines and match the renewed `history=` lines above. These fixes
require no additional golden or event-trace hash changes.

On 2026-10-21, shared Poisonous damage resolution renewed 39 golden games in
modern (38 history-only) and core-2026 (one outcome correction). All 405 source
games were audited against the isolated pre-change engine. Modern states,
winners, turns, action indices and legal-task hashes match; complete histories
and decisions match after removing Poisonous blocks and their lethal flags.
New blocks include TARGET followed by POISONOUS metadata (type 22), including
lethal hits, and resolve before ordinary damage reactions. The Core game
core-2026-core-2026-1-vs-core-2026-1-s1-random previously let Pit Snake (LOE_010)
poison a counter-damaged hero. Excluding hero victims preserves its first 100
actions/legal hashes and extends it to 111 actions: winner 2/turn 25 becomes
winner 1/turn 28. The [per-game first-divergence audit](../../goldens/audits/2026-10-21-poisonous-all-damage.md)
is tracked with the goldens. The simulator's goldens record path
was copied temporarily under tests to retain existing fixture rosters, schemas
and replay pool policies; only that recording copy was adapted and then removed.
All four event invocation traces and their history/archived clone hashes remain
unchanged.

On 2026-10-21, inline split-hit activations renewed only the four `history=`
hashes. Arcane Missiles now emits its hits inside its original POWER block.
Against isolated HEAD, all four live games and their independently continued
clones preserve final states, winners, exact RNG state/draw counts, callback
lines, and every retained history entry in order. Only redundant missile
POWER wrappers are deleted. Archived `clone=` hashes stay unchanged. The
[split-hit golden audit](../../goldens/audits/2026-10-21-split-hits-inline.md)
records per-game state/winner and old/new history hashes, including the 13
history-only changes across the six source golden suites.

On 2026-10-22, live-polish integration renewed only the four `history=`
hashes. All callback lines, including clone callbacks, remain byte-identical;
archived `clone=` hashes remain unchanged. Fresh baseline-engine runs reproduced
each checked-in `history=` hash. Current and baseline live/clone states differ only by
cleared positive spell ZONE_POSITION tags (seed 1: 7/11; 19: 6/7;
42: 14/12; 123: 15/14). Exact RNG states and draw counts match all eight runs.

Every history entry matches in order after accounting for the reviewed changes:
normal draw SHOW_ENTITY now reports HAND (only when followed by the same entity's
HAND move; burned cards retain DECK), spell plays clear ZONE_POSITION to zero,
and the terminal spell's GRAVEYARD tag moves after the game-end tags in four
traces (seed 1 live/clone, 42 clone, 123 clone). No other entry changes.
Causes: c07e4247 (spell position), dbc014c5/7dda88b9 (draw reveals), and the
eff17e83/f63d9678 deferred-spell terminal settlement changes.
Scratch instrumentation was removed. See the
[live-polish audit](../../goldens/audits/2026-10-22-live-polish.md); raw
traces, exact RNG and per-history diffs remain under ignored
`sim/.build/live-polish/events-{baseline,current}/`.

| Seed | Old history prefix | New history prefix |
|---|---|---|
| 1 | `391e81a967cb` | `722264996b85` |
| 19 | `5b43d10f5c2d` | `79615719c06b` |
| 42 | `84f818c3a8b8` | `e8491c89b61c` |
| 123 | `4c82b65ffcbf` | `07ece0a50150` |

On 2026-11-14, the overdraw tile fix (#91) renewed only the seed 123 `history=`
hash (`63155f472219` to `4015876433c6`). Its live game overdraws one card, and
the only new history entry is retail's `META_DATA BURNED_CARD` (type 14) naming
the burned card at the end of the burn's block, after the reveal. Canonical
histories plus final states, dumped from `main` and from the change for all four
games and their clones, are byte-identical except for that one added line.
Seeds 1, 19 and 42 and all four clone histories are unchanged from `main`, all
callback lines match, and the archived `clone=` hashes remain unchanged.
