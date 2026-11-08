# Per-meta card files

> **Card data: not ours.** Card names, card text and card numbers in this folder belong to Blizzard Entertainment.
> They were taken from the Hearthstone game client (version 37.0.0). SoloStone claims no rights in them, and none of
> this repository's licences (AGPL, CC BY) cover them. They are here only so the rules engine can show and play the
> cards. See [NOTICE](../NOTICE) section 5.

One JSON file per meta. The engine embeds the file and loads every card from it, in file order. A file is frozen once shipped, so a meta reads and plays the same on any client.

The file is generated from the client's card definitions by private tooling that is not in the public repository (`tools/gen_card_facts.py`, run after `tools/gen_card_overlay.py`; `--check` fails on a stale file).

## Schema, version 1

```text
{ "schemaVersion": 1, "meta": "<label>",
  "cards":   [ <row>, ... ],
  "overlay": { "schemaVersion": 1, "sourceBuild": "<client build>", "exportSha256": "<hex>", "cards": [ <row>, ... ] } }
```

| Row field | Required | Meaning |
|---|---|---|
| `id` | yes | Card ID |
| `dbf` | yes | Numeric card ID. Not unique: two pairs of base cards share one |
| `name` | overlay rows | English name. Absent means no name |
| `text` | overlay rows | Raw English text. Absent means no text, which is not the same as `""` |
| `textHash` | with `text` | First 16 hex digits of the SHA-256 of `text` as UTF-8, for the client drift check. The loader does not read it |
| `tags` | yes | `[tag, value]` integer pairs in source order. A list, so a duplicate tag still fails at load as it did from XML |
| `refs` | no | Referenced tags, same shape. The first value per tag wins |
| `reqs` | no | `[requirement, parameter]` pairs in order |
| `entourage` | no | Generated-card IDs in order |
| `originalId`, `originalDbf` | no | Overlay only: the card this one reprints |
| `snapshot` | no | Overlay only: a private token definition, never added to the card list |

`cards` and `overlay.cards` keep their source order: it sets the order of the card list, and so of random pools and the first card per `dbf`. Overlay rows get the overlay rules in `CardLoader.Build`.

## What is left out

The engine never reads these, so the generator drops them:

- other locales
- string tags other than name and text: artist, flavor, how-to-earn, targeting arrow text
- `version`, `name` and `cardID` attributes
- `build` and `setOverridesSha256` on the XML roots

The current file has no `reqs` or `entourage` rows because neither XML has any. Those come from the card scripts.
