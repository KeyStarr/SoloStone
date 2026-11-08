# CORE Death Knight skips

## Slice c1

- `CORE_RLK_087` Asphyxiate (`111317`): **STILL-UNPINNED**. Which highest-Attack enemy minion is destroyed when several tie? Random tie selection is plausible, but not established by the pinned export or qualifying rules evidence. No script registered.
  - Pinned export `37.0.0.254477`: cost 3, 0/0 spell, Shadow, no runes; text: “Destroy the highest Attack enemy minion.”
  - Tier A [official card library](https://hearthstone.blizzard.com/en-gb/cards/79503-asphyxiate) pins printed effect only. [Core inventory](https://outof.games/realms/hearthstone/cards/30877-asphyxiate?tab=community) confirms current identity but adds no tie rule. Searches on 2026-10-20 found no qualifying tie observation; wiki page retrieval returned 403.
  - Needed: current-client replay showing tied highest-Attack candidates, or explicit authoritative tie rule covering this effect. No implementation based on a guess.

## Slice c2

- `RLK_048` Anti-Magic Shell (`78342`): registration metadata blocked, not an unresolved gameplay rule. Required exported enchantment `RLK_048e` (`78557`) has `cardSet.id = 0` (`INVALID`), no `CARD_SET` tag, and no entry in pinned `set-overrides.json`. `tools/gen_card_overlay.py` rejects it with `ValueError: missing client set: RLK_048e`. Choosing its presentation set needs a pinned metadata override; that file and generator are outside this slice's allowed scope. No script or registration shipped. Printed +1/+1/Elusive rule itself is clear. Coordinator must pin the enchantment's set before this card can land.

## Fix round 1

- `CORE_CATA_007` Consumption: **STILL-UNPINNED**, not registered. Missing rule: does “Draw a card for each that dies” force a Death Phase before drawing, including Deathrattles (e.g. Tick and Tock emptying the hand)? Public research found no qualifying official statement, server trace, or two independent agreeing sources. See `data/rules-evidence/deathknight/consumption-timing.yaml`. Do not infer cleanup timing from the printed text or the engine.
