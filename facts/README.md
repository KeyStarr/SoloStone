# Per-meta card files

> **Card data: not ours.** Card names, card text and card numbers in this folder belong to Blizzard Entertainment.
> They were taken from the Hearthstone game client (version 37.0.0). SoloStone claims no rights in them, and none of
> this repository's licences (AGPL, CC BY) cover them. They are here only so the rules engine can show and play the
> cards. See [NOTICE](../NOTICE) section 5. The locale files described below are the same: Blizzard's card text.

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

- other locales (their card text is in the locale files below; their names are not shipped)
- string tags other than name and text: artist, flavor, how-to-earn, targeting arrow text
- `version`, `name` and `cardID` attributes
- `build` and `setOverridesSha256` on the XML roots

The current file has no `reqs` or `entourage` rows because neither XML has any. Those come from the card scripts.

## Locale files

`violet-hold-2026-09.<locale>.json`, one per non-English client locale: `deDE esES esMX frFR itIT jaJP koKR plPL ptBR ruRU thTH zhCN zhTW`. They hold card text only, so SoloStone can show its frozen text in the client's language. English stays in the main file. The plugin embeds all thirteen, uncompressed (about 12 MB), and reads only the running client's locale, the first time a card's text is needed. A card a locale file does not translate shows the English text.

```text
{ "schemaVersion": 2, "meta": "<label>", "locale": "<locale>", "baseSourceBuild": "38377",
  "overlaySourceBuild": "<client build>", "overlayLocaleSource": "hsdata <tag>", "overlayLocaleSha256": "<hex>",
  "excluded": [ "<id>", ... ], "missing": [ "<id>", ... ], "tokenMismatch": [ "<id>", ... ],
  "text": { "<id>": [ "<key>", "<raw text>" ], ... } }
```

- `text` maps card ID to the raw text in the client's own format: the same `$`, `#`, `[x]`, `{0}` and `|4(...)` markers as the English text, with every `@` variant kept. Nothing is filled in, so the client builds the final string as it does for English.
- `key` is the FNV-1a 32 hash (8 lowercase hex digits) of the UTF-8 of the English text the translation was made from, trimmed of whitespace at both ends. The plugin hashes the engine's current card text the same way (without its ` @spelldmg` suffix) and uses the translation only when the two agree. The engine rewrites about 20 cards after load, so a translation of the card file's text would say something else: these cards show English. A later engine edit falls back the same way, and a card file regenerated without its locale files fails the generator's `--check` and the launcher tests.
- Every row of the card file that has English text has an entry, except the IDs in `excluded` (the source's English differs from the card file's: today none), `missing` (the source has no text in this locale: 16 or 17, all enchantments, tokens or modes outside the Standard collection) and `tokenMismatch` (the translation has other `$N` or `#N` numbers than the English: 1 to 4 per locale, e.g. BE_854 and JAIL_515 in ruRU). Those show English. Overlay snapshot rows (private token definitions) are not listed: the base card's text is what plays.
- Sources: base cards, the localized text of the same card definitions as the card file (build 38377); overlay cards, HearthSim's `hsdata` card definitions for the 37.0.0.254477 client build (`overlayLocaleSha256` is the SHA-256 of that `CardDefs.xml`). A base card whose text exists only in English there takes its other languages from the same `hsdata` file. A translation is kept only when the source's English text equals the card file's, apart from whitespace at either end.

The generator is private tooling: it takes the `hsdata` file as an input path and refuses any other file. Like the English text, the translations are frozen: a newer client may word a card differently.
