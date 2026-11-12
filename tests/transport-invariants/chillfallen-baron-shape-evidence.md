# Chillfallen Baron: distinct battlecry and deathrattle draws

2026-10-22.

## Decision

Accept only `RLK_708/SPLIT_EFFECT_BLOCK` detail
`sibling-POWER under=PLAY header=differs meta=TARGET`.
The finding compares two complete, distinct activations under one PLAY:
Baron's battlecry and its deathrattle after another minion kills it. This is
the same oracle ambiguity already accepted for `RLK_223` and `CATA_725`.
No producer or oracle code changes are needed. Other shapes remain unaccepted.

## Reproduction and packet review

From the repository root, before editing the baseline, a strict transport run replayed the single reported game (`--strict --replay <game report>`).

Exit **1**. One game, zero transport violations, zero failures. History-shape:
3 events in 2 buckets (2 existing accepted events plus 1 Baron event),
`baselined=1 new=1 stale=0`. Baron example: step 57, history packet 60.
The supplied full-corpus report has three Baron events with this one detail.

Decoded the supplied representative `.packets.jsonl` with the pinned
`PegasusGame.PowerHistory.Deserialize`; the decoder and the readable packet were kept as scratch files. Packet numbers count history packets, as the oracle does.
Relevant packet-60 entries (zero-based indices):

| Entries | Direct block / effect |
| --- | --- |
| 1 | PLAY source 53 (`RLK_708`), target 0 |
| 17–23 | POWER source 53, target 0, EffectIndex 0, blank EffectCardId, keyword 0; one TARGET Data=0 Info=[53]; draw 50 to HAND; closes |
| 24–32 | TRIGGER source 43 (`EDR_815`, Corpse Flower), target 53; TARGET Data=43 Info=[53], DAMAGE Data=3 Info=[53], DAMAGE tag 53=3; closes |
| 33–39 | DEATHS; source 53 moves to GRAVEYARD (ZONE=4); closes |
| 40–46 | TRIGGER source 53, target 0, EffectIndex 0, blank EffectCardId, keyword 217 (DEATHRATTLE); one TARGET Data=0 Info=[53]; draw 73 to HAND; closes |
| 49 | PLAY closes |

Both draw TARGETs are direct preludes in their respective activations. Neither
draw contains DAMAGE/HEALING metadata, a duplicate TARGET, or another effect's
damage. Corpse Flower's own TRIGGER contains its entire damage effect. Hidden
draw snapshots remain unnamed. `header=differs` reflects POWER versus TRIGGER
and deathrattle keyword 217; `sibling-POWER` names the earlier sibling, not the
flagged deathrattle's type. `SameActivation` deliberately allows mixed
POWER/TRIGGER pairs to detect queued continuations; it cannot separate this
legitimate pair, so the exact-shape baseline is the narrow existing mechanism.

Retail `176014_opponent_sideboard.power.log:7506–7516` has a complete battlecry
POWER with TARGET Data=0 Info=[source 47] before its draw; line 7477 identifies
47 as `RLK_708` in SHOW_ENTITY. The local trace is
under `<scratch>`.
This supports the battlecry self TARGET, not retail parity for the deathrattle;
the latter's consumer-contract evidence remains in
`../native-match/draw-spell-impact-evidence.md`.

## Validation

All commands ran from the repository root. Logs and bulk outputs stayed untracked.

| Check | Exit | Result |
| --- | --- | --- |
| Original strict replay, before baseline edit | 1 | Expected new Baron bucket; zero transport violations/failures |
| Strict transport run, after edit | 0 | Fresh cache miss; 9,131 games, zero violations/failures; 860 Standard cards played; 779.799s |
| Full history-shape report | 0 | 264 events in 26 baselined buckets, `new=0 stale=0`; Baron count=3, exactly the reviewed detail |
| `dotnet build tests/native-match/NativeMatchTests.csproj -c Release --artifacts-path sim/.build/baron-native -p:EngineSource=true -p:NuGetAudit=false -p:UseSharedCompilation=false --nologo -v quiet -m:1` | 0 | Two existing obsolete `Game.Clone` warnings, zero errors |
| `dotnet sim/.build/baron-native/bin/NativeMatchTests/release/NativeMatchTests.dll` | 0 | Full native-match contract; 280 draw cases and 24 draw-entry cases pass |
| `git diff --check` | 0 | No whitespace errors |

Logs: `transport-full.log`, `native-build.log`, `native-match.log`.
Original replay reports retained as `red-history-shape.json` and
`red-replay-report.json`. Native-match retained its existing printed Forge
fixture skip: no registered Forge definition.

Existing transport self-checks passed, including exact baseline shapes,
fresh/stale classification, and malformed split blocks. The real replay above
supplies the failing-first baseline check; the full corpus supplies the green
check without another redundant replay. Only baseline configuration and this
report changed; emission and gameplay remain unchanged.

No delegation, Windows, full gate, landing, golden renewal, or runtime
acceptance was performed. Native visual completion remains outside this
headless baseline review.
