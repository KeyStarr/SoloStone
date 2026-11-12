# Pre-Black-Empire pool regression evidence

Scope: the pre-Black-Empire meta change; active policy `offline-standard/2`,
meta `violet-hold`. No pool members, BE implementations, or gameplay rulings changed.

## Repeat installation

The supplied `standard-batch-1-14` versus `standard-batch-1-27`, seed 1 random,
failed at step 66, task `6:2:38::-1:-1`, attempting to activate Ice Barrier twice.
Held spell progress sends the hand play through `BeginDecisionSpell`; its repeat
enters `ResolveSpell` with `repeat=true`, while duplicate admission checked only
`automatic=true`. The shared admission boundary now checks both paths. Quest
admission uses `QuestRules.CanInstall`, including legacy and modern quests. A
reused active entity stays armed in SECRET; failed new Secrets still reveal in
graveyard, but recasting an active Secret does not add a public SHOW.

This preserves the ordinary play path's existing duplicate-fizzle behavior and
[approved Tyrande assumption](../../data/rules-evidence/cast-twice/w08-tyrande-charges.yaml).
The regression retains the original 67 action indices and legal hashes, verified
unchanged before extending it through successful completion. Six immutable replays
in `replays/meta-pre-be` run in focused and full transport, and can run alone using
`TransportInvariants.dll --meta-pre-be-only [case-name]`.

## History findings

Decoded original packets using pinned `PegasusGame.PowerHistory.Deserialize`.
Packet and entry numbers below use the detector's history-packet count and zero-based entries.

| Bucket | Evidence and decision |
|---|---|
| `HAND_BUFF_FX/TIME_876e` | Replay `20-vs-38-s3`, step 99, packet 101: TRIGGER 38 belongs to recurrence 116, held target 17. Entries 39–40 retire Crystalline attachment 134; entry 41 sets HEALTH=5 while the visible identity is still TIME_852, then CHANGE_ENTITY 42 supplies TIME_858's base 5/5. This is a definition reset, not a buff. Publish replacement ATK/HEALTH in CHANGE_ENTITY, while preserving the character's internal replacement values. No baseline exemption. |
| `HAND_BUFF_FX/CORE_BT_416` | Replay `11-vs-15-s7`, step 101, packet 104: CHANGE_ENTITY 8 replaces held 95 with Genn form CATA_615t (6/5); entries 32–33 restore the prior +4/+5 layer (10/10). Existing `RetainedHandTransform` condition proves the amounts and same enclosing activation. Accept only that condition. |
| `HAND_BUFF_FX/EDR_226` | Replay `21-vs-22-s3`, step 75, packet 79: CHANGE_ENTITY 7 replaces held 114 with CATA_615t (6/5); entry 30 restores existing +4 Health (9). Same typed condition, no new buff. |
| `HAND_BUFF_FX/TIME_704` | Replay `37-vs-24-s9`, step 66, packet 68: CHANGE_ENTITY 8 replaces held 109 with CATA_615t (6/5); entry 28 restores existing +2 Health (7). Same typed condition, no new buff. |
| `SPLIT_EFFECT_BLOCK/TLC_100t3` | Replay `38-vs-23-s5`, step 96, packet 98: POWER 1–5 summons entity 135; DAMAGE 2 at entry 6 exhausts location 122; DEATHS 9–18 retires it; separate Deathrattle TRIGGER/keyword 217 at 19 deals 5 damage to hero 4 (22/24). Add only `sibling-POWER under=PLAY header=differs meta=DAMAGE`, still requiring `ExhaustionDeathrattle`. Existing targeted-summon variant remains. |

Replay labels abbreviate the numbered `standard-batch-1` decks; exact IDs and
actions are in the six regression fixtures. Original packets and decoder outputs
remain ignored under `sim/`.
Baseline decisions follow [existing distinct-activation acceptance](chillfallen-baron-shape-evidence.md)
and the already-reviewed CATA_721/Genn and TLC_100t1/TLC_100t3 entries. They claim
consumer-contract consistency, not new server-origin retail observations.
Synthetic streams cover each added source with and without its required condition;
ordinary untargeted buffs and non-exhaustion splits remain errors.

## Same-class audit and stale policy

Audited `ResolveSpell` callers, ordinary/decision completion, automatic task/context
casts, duplicate/full Secret admission, legacy/modern Quest installation, and all
three `TransformHeld` callers (Shapeshifter, Mirrex, Shadow of Demise). Engine probes
cover both repeat mechanisms (Tyrande and Electra), with/without held countdown,
original/clone/snapshot states, retained active identity and one countdown increment.
The existing composed-recurrence proof also guards Crystalline stat cleanup.
Transform reset follows the existing approved
[Shapeshifter](../../data/rules-evidence/hand/shapeshifter-copy-state.yaml) and
[Mirrex](../../data/rules-evidence/hand/mirrex-enchantments.yaml) contracts.

All 64 stale entries are retained, including BE_550. The baseline is global, with
no per-meta partition; [documented policy](history-shape-cache.md) treats unseen
reviewed shapes as diagnostics, not invalid entries. No classifier weakening or
stale-entry pruning. Dependency closure was recomputed unchanged before renewing
its source hash. No golden acceptance or renewal is authorized here.

Failing-first exits (logs were kept locally, not in the repository): original
crash replay exits 2, each of the five history cases exits 1. Engine regressions
abort with signal 6 (shell 134) before their fixes. No Windows/client visual test.

## Final verification

- All six regressions pass in final focused strict transport run: exit 0,
  no transport failures/violations or new history buckets. Original crash was exit 2;
  each history replay was exit 1. Completed Ice Barrier fixture has 114 actions,
  with all original 67 actions and legal hashes unchanged.
- EngineProof repeat, automatic-cast, transform-history and full held-form groups:
  exit 0 each. Standard sweep: 1,304/1,304 cards at three seeds, exit 0.
- Classifier tests: 4/4, exit 0; typed negative acceptance controls also pass in focused transport.
- Six golden suites: 405/405 identical, exit 0 each; no golden file changes.
- Exactly one final full `offline_pools --check` without the transport cache:
  exit 0. Strict transport completed 20,084 games, 1,098,197 checkpoints, zero
  violations/failures, 1,304 registered cards covered, zero new shapes.
  All 412 remaining shape observations are in 22 previously explained/accepted
  buckets. Stale diagnostics now number 65 (the fixed reset also stops sampling
  DINO_407e's old hand-buff ambiguity); all original 64 entries remain preserved.
  Catalog current: Standard 1,118, Past 33, Inactive 131. Full transport wall 2729.940s.
- Dependency graph unchanged; source pin renewed. HEAD unchanged;
  no staging, commit, delegation, Windows or PowerShell checks. Exact native visual
  timing remains unverified; no new retail capture or rule approval is claimed.

## 2026-11-02 transport gate fixture migration

`4c44957fe` intentionally added source-class fallback for an empty Discover pool
(see `data/rules-evidence/discover-pools/empty-hero-pool-source-class.yaml`,
STILL-UNPINNED, owner-approved). Both Rogue stress games cast Mage Alter Time
(`TIME_857`), whose past Arcane pool was previously empty. It now asks for two
Discover answers. The saved action sequence therefore needs two extra decisions.

Renewal preserves the original pinned decks, pool snapshot, seed, every original
action in order, turn count, winner and empty transport-findings digest. The two
new answers select the first offered card. Later entity references shift by six
new offer IDs; matching task keys were translated by that offset. No gameplay
code, classifier baseline or golden was changed.

| Fixture | Unchanged action/legal-hash prefix | Actions before/after | Turns / winner | Regression witness |
|---|---|---|---|---|
| TIME_876e | 179 checkpoints | 186 / 188 | 43 / player 1 | Original Shapeshifter transform at step 99 remains before any divergence |
| TLC_100t3 | 77 checkpoints | 97 / 99 | 24 / player 2 | Original exhaustion Deathrattle at step 96 is now step 98, same `sibling-POWER under=PLAY header=differs meta=DAMAGE` and `ExhaustionDeathrattle` condition |

The TIME_876e row is as of 2026-11-02; the fixture was re-recorded on 2026-11-12 (138 actions, 35 turns, player 1; last section).

`MetaPreBeProof` now explicitly requires that TLC_100t3 witness, so a renewal
that merely avoids the activation cannot pass. Both old fixtures pass against
main `878919628`; current integration first diverges immediately after Alter
Time at steps 179 and 77 respectively. The four other fixtures remain unchanged.

The separate Felhunter deathrattle replay also needed a history-only fingerprint
renewal after `2c8b3e5c2` corrected location activation SubOption. Against main
`878919628`, canonical final state and RNG are byte-identical; all legal hashes,
actions, turns, winner and findings remain unchanged. The complete history diff
is four POWER headers for location entities 254 and 193: `Index=0` becomes
`Index=-1`. EffectIndex remains 0. No other entry changes.

- Combined fingerprint: `c9b6f7efe6211474157101695459e3ea1c135994d9ee57a0e30ec86966e035ae` to `43a81aac87ac23073ec7e15fe32bafe4b0019e3477d4a272acf98937194c27c4`.
- Final-state SHA-256, both: `f61ec4e63eb840464737e3959dd269edf5c6eae80c4ea3bc9806e90468d92378`.
- RNG SHA-256, both: `54a449e59a3e877444633eca16d6174bf2a90d6f22d27675cc13ca169c446238`.
- History SHA-256: `ed777e6ff1108b8bbd34cd6c95b99f23a12ffd18f2defb88e0d5d43cbcc6265e` to `46b2037d9baa843c660fbb40a4c070c42bdaf058a25deab8903b6aa2336b6296`.

Only that transport replay fingerprint was changed. `tests/goldens` and GoldenTests
expectations were not renewed.


## 2026-11-02 Shatter presentation fingerprint renewal

On integrated `03d758be7`, only `replays/private-return/reset-36-vs-31-s5.json`
changed among all eight fingerprint-pinned transport replays (seven private-return
cases and Felhunter). The introducing commit is `4a333192de875e3930ca63b0eaf953fe00940d37`.
An isolated copy of the integrated source, with only that commit's three product
files reversed, reproduces every original fingerprint while retaining the
`cb610761b` hand-target projection change. The normal current build reproduces
all original fingerprints except this one. A second isolation run reverses only
the `cb610761b` NativeMatch change while keeping Shatter enabled: all eight
cases retain byte-identical state, history, RNG and replay metadata versus the
normal current build. Thus tag-1508 projection is ruled out directly. See the intentional
[Shatter presentation recipe](../../docs/design/shatter-presentation.md).

The full canonical final-state strings (58,344 UTF-8 bytes) and RNG strings
(112 bytes) compare byte-for-byte equal. Every one of the 72 recorded legal
hashes and action indices verifies through `Runner.Run(verify: true)` in both
versions. The complete replay record is unchanged except for its fingerprint:
pinned decks, pool snapshot, seed, actions, legal hashes, 22 turns, player-1
winner, and empty findings SHA-256
`e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`.
Both runs report zero transport violations. All seven other pinned cases retain
byte-identical canonical state, history and RNG.

- Combined fingerprint: `a2871e39d13bd3a59f7282ac9678cef7a4f952e72200039d62fb97502b1893be` to `72ddb0252ec362d291c69efcabd1a1c0cba4446e1f69bdaf0d6ce3fc4c884f38`.
- Final-state SHA-256, both: `8d8c0ff2dd560ab35f1e70bd265afbd5aea10877ae82efe693be0eaeec4d0843`.
- RNG SHA-256, both: `85041ac703d54b78d64c8d0afc8e9c3115f66ff1d162ec784f9fb60d991ae1b1`.
- History SHA-256: `e064f18b697bee8f024e8e95e565c2f4ed178ca01e52da7cf4055f297f9a889f` to `76d9f3db1f8b7a0d5f12ff112298dbf06d47fc27d36b1709dd8adfc74b344efa`.
- Canonical history: 480,109 to 480,888 UTF-8 bytes; 3,453 to 3,461 entries.
  Both decision-trace suffixes are empty and byte-identical.

Complete field/entry audit below uses zero-based canonical engine-history entry
numbers, not serialized outbound packet indexes. Parent 75 is `CATA_134`;
replacement halves 76/77 are `CATA_134t`/`CATA_134t2`; controller 3 owns hero 6.

| Before entry | After entry | Complete change |
|---|---|---|
| insertion before 532 | 532 | `PowerHistoryBlockStart`: `BlockType=5` (TRIGGER), `Source=3`, `EffectCardId=""`, `EffectIndex=-1`, `Index=-1`, `Target=0`, `TriggerKeyword=null`, `PowerType=5` |
| 532 | 533 | FULL entity 76: only `Entity.Tags[49]` (ZONE) changes `3` (HAND) to `6` (SETASIDE) |
| 533 | 534 | FULL entity 77: only `Entity.Tags[49]` (ZONE) changes `3` (HAND) to `6` (SETASIDE) |
| insertion before 534 | 535–536 | SUB_SPELL_START: `Source=6`, `SpellPrefabGuid=0384874a085f04440af9f66618dfb8bc`, `Targets=[75]`, `RequirePublicOrigin=false`, `PowerType=11`; balanced SUB_SPELL_END (`PowerType=12`) |
| insertion before 534 | 537–538 | SUB_SPELL_START: `Source=6`, `SpellPrefabGuid=ad8ed082d78aad5478b1d0e682159ab4`, `Targets=[76,77]`, `RequirePublicOrigin=false`, `PowerType=11`; balanced SUB_SPELL_END (`PowerType=12`) |
| insertion before 538 | 543 | TAG_CHANGE: `EntityId=76`, `Tag=49` (ZONE), `Value=3` (HAND), `PowerType=4` |
| insertion before 545 | 551 | TAG_CHANGE: `EntityId=77`, `Tag=49` (ZONE), `Value=3` (HAND), `PowerType=4` |
| insertion before 546 | 553 | `PowerHistoryBlockEnd`, `PowerType=6`, closes new lifecycle TRIGGER |

Exactly eight inserted rows and two changed snapshot fields; every other field
and entry, including original relative ordering and all later history, is
byte-identical after alignment. This replay splits once and never recombines:
there are no new combine pulses, no FX_DATANUM_1/1436 changes and no tag-1508
changes. Staging replacements in SETASIDE allows the separated-spawn override to
be installed before HAND entry, with the explicit hero-source conceal pulse on
the old parent. This is the intentional presentation change, not a new gameplay
or RNG outcome. Only this fingerprint was renewed; no classifier, engine/plugin
source, or GoldenTests expectations were changed.

Audit instrumentation and complete canonical artifacts remain outside tracked
source (scratch folder of the audit run).

Final verification (completed 2026-11-03): the maintainer's full transport run PASS, exit 0, real cache miss `447e319e682a`: 20,975 games, 1,159,762
checkpoints, zero transport violations/buckets/failures. History shape remains
637 observations in 23 accepted buckets, zero new buckets, 69 stale diagnostics
retained. Stage wall 3,437.339 s; total gate wall 3,446.110 s. Separate
The maintainer's golden-tests node PASS, exit 0, **298/298**
unchanged. No device validation or commit.


## 2026-11-04 SDN1 progress fingerprint renewal

Integrated `1a743e2cd` against the last strict base `07d67d443` (which reproduces every
pinned fingerprint). Seven of the eleven fingerprint-pinned replays went stale: six private-return
(`reset-13-vs-31-s0`, `reset-31-vs-44-s6`, `reset-34-vs-31-s5`, `reset-35-vs-31-s5`,
`reset-36-vs-31-s5`, `reset-8-vs-31-s9`) and the Felhunter deathrattle replay. `reset-36-vs-31-s8`
and the meta-pre-be and transport-debt replays are unchanged. The cause is `05236b28e`
(Code Violet `JAIL_735` and Timethief Rafaam `TIME_005` publish `TAG_SCRIPT_DATA_NUM_1` as progress
instead of remaining). Method: both trees were built with a temporary, uncommitted per-step trace
(task key, state SHA, RNG SHA, history length) plus full canonical state/history/RNG dumps, and the
same replays were run in both.

Per game, old versus new:

- Task keys, turns, winner and action counts are identical; every RNG capture is identical at every step;
  every recorded legal hash verifies in both trees.
- Final canonical state differs only by tag `2` on the SDN1 cards: one line (the Code Violet copy) in
  six games, eight lines (three Rafaam and five Code Violet copies) in `reset-35-vs-31-s5`.
  Every other state line is byte-identical.
- Canonical history, with tag-2 changes and tag-2 entity tags of the Code Violet/Rafaam entities masked,
  is row-for-row identical in all seven. The unmasked difference is only those rows (the initial
  `3` row disappears because progress starts at the default `0`; values map old to new as
  `3-n` for Code Violet, `9` to `0` for Rafaam). No privacy, hand/zone or Shatter-split row changed.
- The Shatter rework, generated-shuffle scope and face tiebreak do not touch these seven games.
  (`seed-5`, which pins no fingerprint, does gain the intended SUB_SPELL scope rows.)

| Replay | Old to new combined fingerprint |
| --- | --- |
| reset-13-vs-31-s0 | `2ef86883...329a` to `d8f9d50b...fea9` |
| reset-31-vs-44-s6 | `f09781cc...d112` to `090af2c0...a718` |
| reset-34-vs-31-s5 | `d59d67e3...c7bd` to `67ea09e3...b3d8` |
| reset-35-vs-31-s5 | `a82145ea...de94` to `ee8481c2...0aa3` |
| reset-36-vs-31-s5 | `72ddb025...4f38` to `6ac3f967...1251` |
| reset-8-vs-31-s9 | `9c43d3ae...fcf2` to `0d2b4b4b...70c6` |
| deathrattle Felhunter `21-vs-25-s4` | `43a81aac...27c4` to `5eeebb3d...9304` |

The new value is `sha256(canonical state + canonical history + RNG)`, recomputed from the dumps; the same
computation reproduces every old stored value on the base tree and the unchanged `reset-36-vs-31-s8`
fingerprint on the current tree. Only the `engineFingerprint` field of each JSON changed. `tests/goldens`
were not renewed.

Independent check (second worker): reverting only the engine part of `05236b28e` (`CodeViolet.cs`,
`MugZee.cs`, `TimethiefRafaam.cs`) on `1a743e2cd` and running `--private-return-only` and
`--deathrattle-replay-only` against the pre-renewal JSON reproduced all seven old fingerprints
(exit 0, history-shape `new=0`), so that commit is the sole cause. The revert was undone.

## 2026-11-12 fixtures after main's engine fixes (PR 3c)

Three replay fixtures no longer matched the current engine. Each cause was found by replaying the previous record on the
engine and reverting single commits (review of PR 3c; the engine-only fingerprint parts were compared, no client files):

| Fixture | Before | Cause | Action |
|---|---|---|---|
| `meta-pre-be/TIME_876e` | diverges at action 100 (legal hash) | `7f5de0c` (#32), `CastSpell.cs` only | re-recorded, same decks, seed, policy and pinned pool: 188 to 138 actions, 43 to 35 turns, winner still player 1 |
| `deathrattle/...21-vs-...25-s4` (Felhunter) | diverges at action 127 | Bloated Leech `6f7836b` + `9b2d7ef` (#23/#34) alone | re-recorded in 36d0e4b (259 to 190 actions, 52 to 41 turns, winner 2 to 1), then fingerprint renewed (below) |
| Felhunter and `private-return/reset-36-vs-31-s8` | same actions, fingerprint drift | `7f5de0c` (#32), `Attack.cs` only (ATTACKING and DEFENDING reach the client after the attack triggers) | fingerprint renewed, every action kept |

Reverting any one of `9db9649`, `9ff3bf5`, `f6cf092` (and, for Felhunter, `7923f07` or `d701ddf`) alone does not change
these results. For the renewals the engine-only parts were compared: final state and RNG are unchanged, only the history
differs; reverting just `Attack.cs` of `7f5de0c` restores the old history hash.

- Felhunter fingerprint: `73c55933...a43f` (36d0e4b) to `069c4d6c...cfcf`. The original, pre-PR record had `25cbe9e8...a3e`.
- reset-36-vs-31-s8: `83876546...0608` to `209445cd...5c9e`.

Witnesses. In both re-recorded games the prefix before the divergence is byte-identical (actions and legal hashes 0..99
and 0..126), so each original regression moment is still there: for TIME_876e the held Shapeshifter (entity 17,
TIME_852) turns into TIME_858 at step 99, and the Felhunter lifecycle (EDR_891, played at step 23, dies at step 50) comes
before step 127. `MetaPreBeProof` now requires the TIME_876e witness (CHANGE_ENTITY 17 to TIME_858 at step 99, and no
HAND_BUFF_FX finding), so a later re-record that drops it cannot pass. `DeathrattleReplayProof` pins the one finding the
Felhunter game produces (an accepted SPLIT_EFFECT_BLOCK of TLC_100t1, exhaustion Deathrattle) and fails on any other,
info findings included.

Corrections to commit messages already on the branch (the history is kept): the f2818c6 message calls TIME_876e a focused
game; it is not (`focused` is false in the old and the new record), the card is played because the pinned prefix is
unchanged. The 36d0e4b message names the Bloated Leech, Deathlord and Collapsing Star fixes; only the Bloated Leech pair
is needed.

Behaviour to confirm once (it is `7f5de0c`'s rule, not this change): in the new TIME_876e game a Rat Trap given to the
opponent by Faceless Enigma on that player's own turn counts the Faceless Enigma played just before the Secret existed.
That fits a per-turn "cards played" counter.
