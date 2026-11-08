# Batch 6 remaining skips

- DINO_402 (Bat Mask): `Actions/Copy.cs` copies `Character.CopyInternalAttributes` and enchantment records, but not arbitrary native keyword tags. A runtime probe set a buffed Yeti to 1/1, granted Divine Shield with Hand of Protection, then used `Generic.Copy(..., Zone.PLAY)`: the 1/1 stats and Taunt survived, but Divine Shield did not. The earlier additive-enchantment replay explanation was incorrect. Arbitrary masked copies need shared keyword/ability-copy support; rebuilding that machinery inside this module is outside the brief.
- DINO_405 (Hatching Ceremony): must distinguish this turn end from the next owner turn end. This requires a multi-turn counter, explicitly excluded by the worker brief; the pinned export only supplies the final +2/+2 enchantment.
- DINO_407 (Mirrex): continuously copies another card's abilities in hand, requiring ongoing ability-copy behavior explicitly excluded by the worker brief.
- TLC_230 (TREEEES!!!): summon-all-then-attack versus summon-and-attack ordering is not pinned. The two sequences differ with deaths and summon triggers; the brief requires skipping ambiguous ordering.
- TLC_240 (Tyrannogill): random Bonus Effects can grant Rush. Effect-granted Rush survives Silence in the current engine, explicitly excluded by the brief.
- TLC_428 (Hot Spring Glider): parent says "Your next Murloc"; the sole pinned controller enchant TLC_428e says "this turn". These disagree across turn end. No pinned duration/stacking rule resolves the conflict; choosing either would assert unsupported behavior.

## Assumptions for implemented omissions

Flight of the Firehawk draws typed minions with no shared printed type (including secondary types); typeless minions do not qualify. Torga uses mechanic tag 4028, not a text search or referenced tag, and draws a minion sharing any printed type with the Kindred card. Diabolus Rex selects a lone outer minion once, matching the existing distinct-target pattern in Bitter End.

## Reviewer regression checks

- Supported omissions restored with modules, registrations and seed-1–3 proofs: DINO_138, TLC_102, TLC_222, TLC_248, TLC_439. All remaining slice omissions have the blockers above; none remain "not attempted".
- Ravenous Flock: restoring `ComplexTask.Create(SummonTask, RemoveEnchantmentTask)` fails `5 occupied slots still remove one-shot enchantment`. A separate run starting at seven occupied slots fails `7 occupied slots still remove one-shot enchantment`. The final proof checks five, six and seven occupied slots, exact token counts, detached enchantments and no later repeat.
- Mechanized Magma: replacing `CAST_SPELL` with `AFTER_CAST` fails `Fireball grants stats before damage so fresh Magma survives at three Health`. The final proof requires the fresh minion to remain on board as 6/9 with 3 Health after Fireball.
- Removing the five restored collectible registrations fails each card's `registered Standard module` proof. All temporary mutations and the Bat Mask probe were restored after running; no probe module or token was retained.
