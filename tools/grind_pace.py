#!/usr/bin/python3
"""Monte Carlo collection pace for SoloStone; Python 3 standard library only.

Rules audited against FairPackRules.cs, LocalCrafting.cs, LocalEconomy.cs,
LocalProfile.cs, LocalCardCatalog.cs, LocalShop.cs, LocalLadderMilestones.cs and
LocalAchievements.cs. Runtime reads data files only; no repo modules are imported.

TODO (known gap, 2026-11-12): this models a player who puts every gold into packs and crafting. The expected player
mostly buys shop decks (often buying a recipe, crafting some cards with dust and buying the discounted rest from the
shop) and buys many cosmetics. Neither is simulated, so the hours below are not the expected player's pace: deck buys
may finish a collection faster, cosmetics slow it down. Add a deck-buy strategy and a cosmetics spend before using
these numbers to tune rewards.

Assumptions (also printed and included in JSON):
* KNOWN GAP: no shop deck or recipe purchases and no cosmetics spending are simulated (see the TODO above); the
  results are a packs-and-crafting baseline, not the expected player's pace.
* Use the policy's active meta and FairPackRules eligibility manifest. Target is
  craftable identities in its expansion/Standard pack union, at playset (2, or 1
  legendary), weighted by normal craft cost. Core, Free, starter, welcome-only,
  noncollectible, premium definitions and noncraftable card types are excluded.
  Core/starter cards require no completion work, regardless of class level.
* Start empty; at t=0 receive 10 Standard packs, 500 gold, 400 dust. Welcome-only
  identities are outside target. Ignore welcome's 2500-gold deck kit, ladder's
  3200-gold kit and Legend deck grants: this models packs/crafting, not deck buys
  (whose missing copies cost 20/50/200/800 gold).
* Open available free packs, spend gold on packs, then craft each game's affordable
  missing copies, cheapest rarity first, card ID breaking ties (also at t=0).
  Choose each purchased set pack by greatest missing target craft value; tie by
  policy order. Use Standard if cheaper than that set, or no set is available.
* Five rarity rolls, slot zero Rare-or-better, and 10-first-legendary / 40-legendary
  / 10-epic pity follow FairPackRules, with independent counters per pack type.
  Duplicate protection uses lifetime acquisition across qualities, including
  crafted/token cards, and limits repeat identities within each pack to playset.
* Golden/signature probabilities follow rule 2, signature eligibility uses the
  union of mastery metas. All qualities count as normal copies for completion.
  Only extras above combined playset are sold: normal at 20/50/200/800 dust;
  pack golden AND signature at LocalCrafting.PremiumDust 200/400/800/1600.
  Never sell a needed premium for profit; non-disenchantable drops yield no dust.
* Every game has independent win probability w (default 0.50); all outcomes are
  eligible (4+ turns, no concede). RewardV6 pays gold U[40,70] AND dust U[10,30]; losses pay
  one third of that same roll. Round each loss currency before reward_scale,
  then round scaled currencies, half away from zero as in the game. Ties ignored.
* All achievements are eventually earned: 2100 gold, 3550 dust and 30 Standard
  packs spread linearly over A games; whole packs unlock at cumulative integers.
  This is a timing assumption, not a simulation of achievement requirements.
* One ladder climb only. Milestones occur at ceil(games_to_legend * stars/150):
  26 Standard packs, 1 epic token, 4 legendary tokens through Legend. Source code
  has 3 legendary tokens before Legend and the fourth AT Legend, not 4 before.
  Tokens grant a missing target copy free; exhausted rarity tokens have no value.
* Hours are games/g (default 6 games/h, 10 min/game); opening/crafting is instant.
  X (one specific shipped meta deck bought from the shop by a new player) =
  median fresh-profile PurchasePrice / (expected match gold/h + 0.5 * expected
  match dust/h). Include recipe fee and missing copies, including locked Core;
  keep starter and level-1 Core ownership. Grant-only cards are unpriced, as in
  PurchasePrice, but block buying their decks until acquired. Exclude welcome,
  achievements, ladder milestones, rival gold and all packs from this metric.
  The old collection-value rate remains as collection_value_deck_equivalent:
  median full-craft dust / new target craft value per hour from welcome to 50%,
  including intervening grants and pack contents at full craft value.
* Retail reference replaces match income with live-game F2P income per hour of
  Ranked play with every quest/event/free pack claimed: 2 h/day (the calibration)
  45.7 gold, 0.352 Standard packs and 12.5 dust/hour; 1 h/day (sensitivity) 78.1
  gold, 0.705 packs and 25 dust/hour. Both keep
  the same SoloStone welcome, achievements and Standard-pace ladder grants.
* Report medians across independent seeded runs. Match income/h is its analytic
  recurring expectation; total income/h is realized gross currency through 100%
  completion, including welcome, earned achievements and extra-card dust, divided
  by that run's hours (then median). Packs/tokens are not currency income.

Usage (docs/design/2026-10-31-grind-tiers.md): /usr/bin/python3 tools/grind_pace.py --runs 20
       --root REPO --tier relaxed-6x:6:101 --json [FILE]
--json alone emits JSON instead of Markdown; --json FILE also saves JSON.
"""

import argparse
import collections
import gzip
import json
import math
import random
import statistics
from dataclasses import dataclass, field
from pathlib import Path


RARITIES = ("COMMON", "RARE", "EPIC", "LEGENDARY")
CRAFT = dict(zip(RARITIES, (40, 100, 400, 1600)))
NORMAL_DUST = dict(zip(RARITIES, (20, 50, 200, 800)))
PREMIUM_DUST = dict(zip(RARITIES, (200, 400, 800, 1600)))
WEIGHT_TOTAL = 1000000000
RARITY_WEIGHTS = {"LEGENDARY": 9505953, "EPIC": 41688853, "RARE": 200000000,
                  "COMMON": 748805194}
GOLDEN_WEIGHTS = dict(zip(RARITIES, (15000000, 20000000, 50000000, 120000000)))
SIGNATURE_WEIGHT = 166666667
FRACTIONS = (0.25, 0.50, 0.75, 0.90, 1.0)
DEFAULT_TIERS = ("relaxed-6x:6:101", "standard-2x:2:217", "grinder:0.75:440", "retail:0.4:217")
RETAIL_REFERENCES = {1: (78.1, 0.705, 25.0), 2: (45.7, 0.352, 12.5)}

# LocalEconomy.StarterCollection = these two LocalCardCatalog manifests.
STARTER_IDS = set("""
CS1_112 CS1_130 CS2_004 CS2_022 CS2_023 CS2_024 CS2_026 CS2_027 CS2_029
CS2_032 CS2_033 CS2_087 CS2_088 CS2_089 CS2_091 CS2_092 CS2_093 CS2_094
CS2_097 CS2_106 CS2_108 CS2_112 CS2_114 CS2_179 CS2_181 CS2_182 CS2_200
CS2_222 CS2_234 CS2_235 CS2_236 DS1_055 EX1_007 EX1_015 EX1_019 EX1_066
EX1_091 EX1_284 EX1_335 EX1_360 EX1_371 EX1_400 EX1_402 EX1_556 EX1_603
EX1_606 EX1_608 EX1_622 NEW1_011 NEW1_012
CS1_042 CS1_113 CS2_003 CS2_025 CS2_103 CS2_105 CS2_118 CS2_119 CS2_120
CS2_121 CS2_122 CS2_124 CS2_125 CS2_127 CS2_131 CS2_141 CS2_142 CS2_147
CS2_150 CS2_155 CS2_162 CS2_168 CS2_171 CS2_172 CS2_173 CS2_186 CS2_187
CS2_189 CS2_196 CS2_197 CS2_201 CS2_213 CS2_226 EX1_011 EX1_025 EX1_084
EX1_192 EX1_277 EX1_399 EX1_506 EX1_508 EX1_582 EX1_593
""".split())
WELCOME_IDS = set("""
CATA_EVENT_000 CATA_EVENT_001 CATA_EVENT_002 CATA_EVENT_110 CATA_EVENT_400
CATA_EVENT_401 CATA_EVENT_402 EDR_950 JAIL_EVENT_100 JAIL_EVENT_101 JAIL_EVENT_102
TIME_EVENT_300 TIME_EVENT_301 TIME_EVENT_997 TIME_EVENT_998 TIME_EVENT_999
TLC_EVENT_400 TLC_EVENT_402 BE_EVENT_100 BE_EVENT_101 BE_EVENT_102 BE_036
""".split())
# RecipeLibrary.BuildDeck exempts these engine FabledRules companions, not every noncollectible card.
FABLED_COMPANIONS = set("""
TIME_009t1 TIME_009t2 TIME_020t1 TIME_020t2 TIME_209t TIME_209t2 TIME_211t1 TIME_211t2
TIME_609t1 TIME_609t2 TIME_619t TIME_619t2 TIME_850t TIME_850t1 TIME_852t1 TIME_852t3
TIME_875t TIME_875t1 TIME_890t TIME_890t2 TIME_005t1 TIME_005t2 TIME_005t3 TIME_005t4
TIME_005t5 TIME_005t6 TIME_005t7 TIME_005t8 TIME_005t9
""".split())


@dataclass(frozen=True)
class Tier:
    name: str
    reward_scale: float
    games_to_legend: int
    fixed_gold_per_hour: float = None
    fixed_packs_per_hour: float = 0.0
    fixed_dust_per_hour: float = 0.0


def parse_tier(value):
    try:
        name, scale, games = value.split(":")
        tier = Tier(name.strip(), float(scale), int(games))
        if (not tier.name or not math.isfinite(tier.reward_scale)
                or tier.reward_scale <= 0 or tier.games_to_legend <= 0):
            raise ValueError()
        return tier
    except (ValueError, TypeError):
        raise argparse.ArgumentTypeError("tier must be name:positive_scale:positive_integer_games")


def reward_v4(rng, scale=1.0):
    """Reward conditional on an eligible win; expectation (41.25, 26.25)*scale."""
    kind = rng.randrange(4)
    if kind < 2:
        return rng.randint(40, 80) * scale, 0.0
    if kind == 2:
        return 0.0, rng.randint(40, 80) * scale
    return rng.randint(30, 60) * scale, rng.randint(30, 60) * scale


def reward_v5(rng, won, scale=1.0):
    """Same win roll; rounded third for a loss, then the runtime tier rounding."""
    roll = reward_v4(rng)
    return tuple(math.floor((v if won else math.floor(v / 3 + 0.5)) * scale + 0.5) for v in roll)


def reward_v6(rng, won, scale=1.0):
    """Both currencies on every eligible win; loss thirds before tier rounding."""
    return tuple(math.floor((v if won else math.floor(v / 3 + 0.5)) * scale + 0.5)
                 for v in (rng.randint(40, 70), rng.randint(10, 30)))


def reward_expectation(won, scale=1.0):
    """Exact expectation over the inclusive rolls, including both rounding steps."""
    def mean(low, high):
        return sum(math.floor((v if won else math.floor(v / 3 + 0.5)) * scale + 0.5)
                   for v in range(low, high + 1)) / (high - low + 1)
    return mean(40, 70), mean(10, 30)


def specific_deck_hours(catalogue, tier, win_rate=0.5, games_per_hour=6):
    """Recurring currency only; no random packs or one-time grants buy this deck."""
    if not catalogue.deck_costs:
        return None
    if tier.fixed_gold_per_hour is not None:
        gold, dust = tier.fixed_gold_per_hour, tier.fixed_dust_per_hour
    else:
        win, loss = reward_expectation(True, tier.reward_scale), reward_expectation(False, tier.reward_scale)
        gold, dust = ((w * win_rate + l * (1 - win_rate)) * games_per_hour for w, l in zip(win, loss))
    return catalogue.meta_deck_shop_gold / (gold + 0.5 * dust)


@dataclass
class PackCounters:
    opened: int = 0
    since_legendary: int = 0
    since_epic: int = 0
    first_legendary: bool = False


def roll_rarities(rng, counters):
    """Mirror FairPackRules.Generate's rarity phase and Guarantee ordering."""
    legendary = RARITY_WEIGHTS["LEGENDARY"]
    epic = legendary + RARITY_WEIGHTS["EPIC"]
    rare = epic + RARITY_WEIGHTS["RARE"]
    result = []
    for slot in range(5):
        roll = rng.randrange(WEIGHT_TOTAL)
        result.append("LEGENDARY" if roll < legendary else "EPIC" if roll < epic
                      else "RARE" if slot == 0 or roll < rare else "COMMON")

    def guarantee(rarity):
        if rarity not in result:
            slot = next((i for i, r in enumerate(result) if r in ("COMMON", "RARE")), 0)
            result[slot] = rarity

    if counters.since_legendary == 39 or (not counters.first_legendary and counters.opened == 9):
        guarantee("LEGENDARY")
    if counters.since_epic == 9:
        guarantee("EPIC")
    counters.opened += 1
    counters.since_legendary = 0 if "LEGENDARY" in result else counters.since_legendary + 1
    counters.since_epic = 0 if "EPIC" in result else counters.since_epic + 1
    counters.first_legendary |= "LEGENDARY" in result
    return result


def draw_pack(rng, quality_rng, pool, counters, acquired, signature_ids=()):
    """Return (identity, premium) tuples; caller commits acquired after whole pack."""
    rarities = roll_rarities(rng, counters)
    in_pack = collections.Counter()
    result = []
    for rarity in rarities:
        cap = 1 if rarity == "LEGENDARY" else 2
        bucket = pool[rarity]
        candidates = [cid for cid in bucket if acquired.get(cid, 0) + in_pack[cid] < cap]
        if not candidates:
            candidates = [cid for cid in bucket if in_pack[cid] < cap]
        cid = rng.choice(candidates)
        in_pack[cid] += 1
        signature = SIGNATURE_WEIGHT if rarity == "LEGENDARY" and cid in signature_ids else 0
        roll = quality_rng.randrange(WEIGHT_TOTAL)
        golden_limit = signature + (WEIGHT_TOTAL - signature) * GOLDEN_WEIGHTS[rarity] // WEIGHT_TOTAL
        premium = 3 if roll < signature else 1 if roll < golden_limit else 0
        result.append((cid, premium))
    return result


@dataclass
class Catalogue:
    meta: str
    pools: dict
    rarity: dict
    card_set: dict
    target: set
    premium_sellable: set
    signatures: set
    prices: dict
    ladder: list
    deck_costs: list = field(default_factory=list)

    @property
    def meta_deck_cost(self):
        return statistics.median(row["craft_dust"] for row in self.deck_costs) if self.deck_costs else None

    @property
    def meta_deck_shop_gold(self):
        return statistics.median(row["shop_gold"] for row in self.deck_costs) if self.deck_costs else None

    @property
    def target_value(self):
        return sum(CRAFT[self.rarity[cid]] * (1 if self.rarity[cid] == "LEGENDARY" else 2)
                   for cid in self.target)


def shop_deck_costs(root, definitions, core, active_sets, extra_ids):
    """Fresh LocalProfile.PurchasePrice, plus the separate craft-value benchmark.

    All 72 published lists' pending dependencies are now admitted on this branch;
    fail rather than silently price a fallback/withheld list if that changes.
    Noncollectible companions/modules are owned with their primary, never crafted.
    """
    recipes = {r["id"]: r for r in json.loads((root / "data/shop/recipes-v1.json").read_text())["recipes"]}
    products = json.loads((root / "data/shop/catalog-v1.json").read_text())["Products"]
    recipe_prices = {p["RecipeId"]: p["Price"] for p in products if p["Kind"] == "Recipe"}
    schedule = json.loads((root / "data/progression/core-2026-unlocks.json").read_text())
    # EmptyXp starts every class at level 1 and neutral combined level at 4.
    owned = dict.fromkeys(STARTER_IDS, 2)
    for card in schedule["cards"]:
        level = schedule["neutralBaseCombinedLevel"] if card["class"] == "NEUTRAL" else 1
        if card["threshold"] <= level:
            owned[card["cardId"]] = card["copies"]
    rows = []
    for product in products:
        if product["Kind"] != "Deck":
            continue
        recipe = recipes[product["RecipeId"]]
        needs = recipe.get("pending", {}).get("needs", [])
        if any(not need.startswith("rule:") and need not in extra_ids
               and definitions.get(need, {}).get("cardSet", {}).get("name") not in active_sets for need in needs):
            raise ValueError("Shop pending list needs runtime resolution: " + recipe["id"])
        copies = recipe["cards"] + [c for b in recipe.get("sideboards", []) for c in b["cards"]]
        cost = reference = 0
        shop_gold = recipe_prices[recipe["id"]]
        needed = collections.Counter()
        for row in copies:
            needed[row["id"]] += row["count"]
        for cid, count in needed.items():
            card = definitions[cid]
            # Free cards have no craft price; use Common's 40 only for this hypothetical reference.
            price = CRAFT.get(card["rarity"], 40 if card["rarity"] == "FREE" else 0)
            reference += count * price
            craftable = (card.get("collectible") and card["cardType"] in {"MINION", "SPELL", "WEAPON", "LOCATION"}
                         and cid not in core and not cid.upper().startswith("CORE_")
                         and cid not in STARTER_IDS and cid not in WELCOME_IDS
                         and card["rarity"] in CRAFT and not card.get("tags", {}).get("12", 0)
                         and (card["cardSet"]["name"] in active_sets or cid in extra_ids))
            if craftable:
                cost += count * price
            # RecipeLibrary owns Free cards and Fabled companions virtually. LocalShop
            # skips grant-only copies and unscheduled Core, but sells locked Core.
            is_core = cid in core or cid.upper().startswith("CORE_")
            if (cid not in FABLED_COMPANIONS and card["rarity"] != "FREE" and cid not in WELCOME_IDS
                    and (not is_core or cid in core)):
                shop_gold += max(0, count - owned.get(cid, 0)) * NORMAL_DUST[card["rarity"]]
        rows.append({"recipe": recipe["id"], "craft_dust": cost, "shop_gold": shop_gold, "all_priced_dust": reference})
    return rows


def deck_cost_summary(catalogue):
    return {"count": len(catalogue.deck_costs),
            "median": catalogue.meta_deck_cost,
            "median_shop_gold": catalogue.meta_deck_shop_gold,
            "min": min((r["craft_dust"] for r in catalogue.deck_costs), default=None),
            "max": max((r["craft_dust"] for r in catalogue.deck_costs), default=None),
            "all_priced_median": statistics.median(r["all_priced_dust"] for r in catalogue.deck_costs)
            if catalogue.deck_costs else None}


# Card tag numbers and enum values of the game's public card format (the same tags the engine reads from facts/).
TAG_SET, TAG_TYPE, TAG_RARITY, TAG_COLLECTIBLE = 183, 202, 203, 321
RARITY_NAMES = {1: "COMMON", 2: "FREE", 3: "RARE", 4: "EPIC", 5: "LEGENDARY"}
CARD_TYPE_NAMES = {3: "HERO", 4: "MINION", 5: "SPELL", 7: "WEAPON", 39: "LOCATION"}


def definitions_from_facts(root, manifest, meta, core):
    """The fields of the client card export that this model reads, built from the public per-meta card file (facts/).

    Used when the export (data/carddb/, built from a client and never committed) is absent. A set's name comes from the
    fair-pack manifest: its cards give the set's number (tag 183), and every other card of that number is in the set.
    Cards of the meta's extra list are EVENT and the unlock schedule's cards are CORE; anything else is OTHER.
    """
    files = sorted((root / "facts").glob(meta["id"] + "-*.json"))
    if not files:
        raise ValueError("No card file in facts/ for meta " + meta["id"])
    facts = json.loads(files[-1].read_text())
    rows = {}
    for row in facts["cards"] + facts["overlay"]["cards"]:
        rows.setdefault(row["id"], dict(row["tags"]))
    set_of = {c["id"]: name for name, s in manifest["sets"].items() for c in s["cards"]}
    name_of_number = {tags[TAG_SET]: set_of[cid] for cid, tags in rows.items() if cid in set_of and TAG_SET in tags}
    definitions = {}
    for cid, tags in rows.items():
        name = (set_of.get(cid) or name_of_number.get(tags.get(TAG_SET)) or ("EVENT" if cid in meta["extraCards"] else None)
                or ("CORE" if cid in core else "OTHER"))
        definitions[cid] = {
            "cardId": cid, "collectible": tags.get(TAG_COLLECTIBLE, 0) == 1, "rarity": RARITY_NAMES.get(tags.get(TAG_RARITY), "INVALID"),
            "cardType": CARD_TYPE_NAMES.get(tags.get(TAG_TYPE), str(tags.get(TAG_TYPE))),
            "tags": {str(k): v for k, v in tags.items()}, "cardSet": {"name": name}}
    return definitions


def load_catalogue(root):
    def read(relative):
        return json.loads((root / relative).read_text())

    policy = read("data/pools/offline-standard-v1.json")
    meta = next(m for m in policy["metas"] if m["id"] == policy["activeMeta"])
    manifest = read("data/packs/eligibility-v1.json")
    active_sets = set(meta["sets"]) - {"CORE"}
    sets = [s for s in manifest["knownSets"] if s in active_sets]
    if set(sets) != active_sets or not sets:
        raise ValueError("Active sets missing from fair-pack manifest")
    core = {c["cardId"] for c in read("data/progression/core-2026-unlocks.json")["cards"]}
    export = root / ("data/carddb/" + policy["clientBuild"] + "/cards.json.gz")
    if export.is_file():
        with gzip.open(export, "rt") as stream:
            definitions = {c["cardId"]: c for c in json.load(stream)["cards"]}
    else:
        definitions = definitions_from_facts(root, manifest, meta, core)
    signatures = set()
    for row in read("data/progression/card-mastery.json")["metas"].values():
        signatures.update(row["signature"])
    pools, rarity, card_set = {}, {}, {}
    for name in sets:
        buckets = {r: [] for r in RARITIES}
        for card in manifest["sets"][name]["cards"]:
            cid, r = card["id"], card["rarity"]
            if cid in card_set or r not in CRAFT or cid in meta["extraCards"]:
                raise ValueError("Invalid fair-pack identity: " + cid)
            if not definitions[cid].get("collectible") or definitions[cid]["rarity"] != r:
                raise ValueError("Card database disagrees with pack manifest: " + cid)
            rarity[cid], card_set[cid] = r, name
            buckets[r].append(cid)
        if any(len(buckets[r]) * (1 if r == "LEGENDARY" else 2) < 5 for r in RARITIES):
            raise ValueError("Pack cannot fill five same-rarity slots: " + name)
        pools[name] = {r: tuple(sorted(buckets[r])) for r in RARITIES}
    pools["STANDARD"] = {r: tuple(sorted(cid for cid in rarity if rarity[cid] == r)) for r in RARITIES}
    valid_types = {"MINION", "SPELL", "WEAPON", "LOCATION"}
    premium_sellable = {cid for cid in rarity if cid not in WELCOME_IDS
                        and definitions[cid]["cardType"] in valid_types
                        and not definitions[cid].get("tags", {}).get("12", 0)}
    target = {cid for cid in premium_sellable if cid not in core and not cid.upper().startswith("CORE_")
              and cid not in STARTER_IDS}
    if not target:
        raise ValueError("Empty craftable target")
    prices = {p["PackType"]: p["Price"] for p in read("data/shop/catalog-v1.json")["Products"]
              if p["Kind"] == "Pack" and p["PackType"] in pools}
    if set(prices) != set(pools) or any(price <= 0 for price in prices.values()):
        raise ValueError("Missing or invalid pack shop prices")
    ladder = [m for m in read("data/ladder/milestones-v1.json")["milestones"]
              if m["kind"] == "StarIndex" and m["threshold"] <= 150]
    return Catalogue(meta["id"], pools, rarity, card_set, target, premium_sellable,
                     signatures, prices, ladder,
                     shop_deck_costs(root, definitions, core, active_sets, set(meta["extraCards"])))


class CollectionRun:
    """Small dictionary ledgers; lifetime acquisition never decreases on a sale."""

    def __init__(self, catalogue, seed):
        self.catalogue = catalogue
        self.target_value = catalogue.target_value
        self.pack_rng = random.Random(seed * 4 + 1)
        self.quality_rng = random.Random(seed * 4 + 2)
        self.game_rng = random.Random(seed * 4 + 3)
        self.reward_rng = random.Random(seed * 4 + 4)
        self.owned, self.acquired = {}, {}
        self.counters = {name: PackCounters() for name in catalogue.pools}
        self.gold = self.dust = self.gold_earned = self.dust_earned = 0.0
        self.value = 0
        self.missing_value = {name: 0 for name in catalogue.pools if name != "STANDARD"}
        self.missing = {r: [] for r in RARITIES}
        for cid in sorted(catalogue.target, reverse=True):
            r = catalogue.rarity[cid]
            self.missing[r].append(cid)
            self.missing_value[catalogue.card_set[cid]] += CRAFT[r] * (1 if r == "LEGENDARY" else 2)

    def currency(self, gold=0.0, dust=0.0):
        self.gold += gold
        self.dust += dust
        self.gold_earned += gold
        self.dust_earned += dust

    def acquire(self, cid, premium=0):
        r = self.catalogue.rarity[cid]
        cap = 1 if r == "LEGENDARY" else 2
        self.acquired[cid] = min(cap, self.acquired.get(cid, 0) + 1)
        if self.owned.get(cid, 0) < cap:
            self.owned[cid] = self.owned.get(cid, 0) + 1
            if cid in self.catalogue.target:
                self.value += CRAFT[r]
                self.missing_value[self.catalogue.card_set[cid]] -= CRAFT[r]
        elif premium and cid in self.catalogue.premium_sellable:
            self.currency(dust=PREMIUM_DUST[r])
        elif not premium and cid in self.catalogue.target:
            self.currency(dust=NORMAL_DUST[r])

    def open_pack(self, name):
        for cid, premium in draw_pack(self.pack_rng, self.quality_rng, self.catalogue.pools[name],
                                      self.counters[name], self.acquired, self.catalogue.signatures):
            self.acquire(cid, premium)

    def next_missing(self, rarity):
        candidates = self.missing[rarity]
        cap = 1 if rarity == "LEGENDARY" else 2
        while candidates and self.owned.get(candidates[-1], 0) >= cap:
            candidates.pop()
        return candidates[-1] if candidates else None

    def token(self, rarity):
        cid = self.next_missing(rarity)
        if cid is not None:
            self.acquire(cid)

    def settle(self, standard_packs=0):
        for _ in range(standard_packs):
            self.open_pack("STANDARD")
        while self.gold >= min(self.catalogue.prices.values()) and self.value < self.target_value:
            name = max(self.missing_value, key=self.missing_value.get, default="STANDARD")
            if self.catalogue.prices["STANDARD"] < self.catalogue.prices[name]:
                name = "STANDARD"
            price = self.catalogue.prices[name]
            if self.gold < price:
                break
            self.gold -= price
            self.open_pack(name)
        for rarity in RARITIES:
            while self.dust >= CRAFT[rarity]:
                cid = self.next_missing(rarity)
                if cid is None:
                    break
                self.dust -= CRAFT[rarity]
                self.acquire(cid)


def simulate(catalogue, tier, seed, win_rate=0.5, games_per_hour=6, achievement_games=400, stop_fraction=1.0):
    state = CollectionRun(catalogue, seed)
    target_value = catalogue.target_value
    state.currency(gold=500, dust=400)
    state.settle(10)
    welcome_value = state.value
    thresholds = {str(round(f * 100)): f * target_value for f in FRACTIONS}
    reached = {name: 0.0 for name, value in thresholds.items() if state.value >= value}
    half_craft_value = state.value if "50" in reached else None
    schedule = collections.defaultdict(list)
    for milestone in catalogue.ladder:
        at = math.ceil(tier.games_to_legend * milestone["threshold"] / 150)
        schedule[at].extend(milestone["grants"])
    game = 0
    while state.value < target_value * stop_fraction:
        game += 1
        if game > 2000000:
            raise RuntimeError("Completion exceeded 2,000,000 games; check inputs")
        packs = 0
        if tier.fixed_gold_per_hour is not None:
            state.currency(gold=tier.fixed_gold_per_hour / games_per_hour,
                           dust=tier.fixed_dust_per_hour / games_per_hour)
            packs = (math.floor(game * tier.fixed_packs_per_hour / games_per_hour)
                     - math.floor((game - 1) * tier.fixed_packs_per_hour / games_per_hour))
        else:
            state.currency(*reward_v6(state.reward_rng, state.game_rng.random() < win_rate, tier.reward_scale))
        if game <= achievement_games:
            # Difference of cumulative totals avoids floating-point drift at A.
            state.currency(2100 * game / achievement_games - 2100 * (game - 1) / achievement_games,
                           3550 * game / achievement_games - 3550 * (game - 1) / achievement_games)
            packs += 30 * game // achievement_games - 30 * (game - 1) // achievement_games
        for grant in schedule.get(game, ()):
            if grant["Kind"] == "Pack":
                packs += grant["Count"]
            elif grant["Kind"] == "Token":
                for _ in range(grant["Count"]):
                    state.token(grant["Ref"])
        state.settle(packs)
        for name, value in thresholds.items():
            if state.value >= value and name not in reached:
                reached[name] = game / games_per_hour
                if name == "50":
                    half_craft_value = state.value
    hours = game / games_per_hour
    half_hours = reached.get("50", 0)
    new_value = (half_craft_value or welcome_value) - welcome_value
    deck_hours = (catalogue.meta_deck_cost * half_hours / new_value
                  if catalogue.meta_deck_cost is not None and half_hours > 0 and new_value > 0 else None)
    return {"hours": dict(reached, new_meta_deck=specific_deck_hours(catalogue, tier, win_rate, games_per_hour),
                          collection_value_deck_equivalent=deck_hours),
            "welcome_craft_value": welcome_value,
            "half_craft_value": half_craft_value,
            "total_gold_per_hour": state.gold_earned / hours if hours else None,
            "total_dust_per_hour": state.dust_earned / hours if hours else None}


# Retail comparison at 2 h/day; 1 h/day is a sensitivity. Calibration never overrides the owner's retained 0.40 scale.
RETAIL_HOURS_PER_DAY = 2


def calibrate_retail(catalogue, runs=20, seed=0, win_rate=0.5, games_per_hour=6, achievement_games=400,
                     hours_per_day=RETAIL_HOURS_PER_DAY):
    """Bisect seeded median half-collection time, keeping every one-time grant."""
    def half(tier):
        return statistics.median(simulate(catalogue, tier, seed + run, win_rate, games_per_hour,
                                          achievement_games, stop_fraction=0.5)["hours"]["50"]
                                 for run in range(runs))

    gold, packs, dust = RETAIL_REFERENCES[hours_per_day]
    reference = half(Tier("retail_reference", 1, 217, gold, packs, dust))
    low, high = 0.01, 1.0
    if not half(Tier("low", low, 217)) >= reference >= half(Tier("high", high, 217)):
        raise ValueError("Retail reference outside calibration bracket")
    while high - low > 0.0025:
        middle = (low + high) / 2
        if half(Tier("calibration", middle, 217)) > reference:
            low = middle
        else:
            high = middle
    scale = (low + high) / 2
    rounded = math.floor(scale / 0.05 + 0.5) * 0.05
    return {"hours_per_day": hours_per_day, "gold_per_hour": gold, "packs_per_hour": packs, "dust_per_hour": dust,
            "reference_half_hours": reference, "scale": scale,
            "table_scale": round(rounded, 2), "bracket": [low, high]}


def build_parser():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--tier", action="append", type=parse_tier, help="name:reward_scale:games_to_legend; repeatable")
    parser.add_argument("--runs", type=int, default=20)
    parser.add_argument("--seed", type=int, default=0)
    parser.add_argument("--win-rate", type=float, default=0.5)
    parser.add_argument("--games-per-hour", type=float, default=6)
    parser.add_argument("--achievement-games", type=int, default=400)
    parser.add_argument("--calibrate-retail", action="store_true", help="bisect retail scale with the same run seeds")
    parser.add_argument("--json", nargs="?", const="-", metavar="FILE", help="JSON stdout, or save FILE alongside Markdown")
    return parser


def make_report(catalogue, args):
    tiers = args.tier or [parse_tier(value) for value in DEFAULT_TIERS]
    if len({t.name for t in tiers}) != len(tiers):
        raise ValueError("Tier names must be unique")
    calibration_1h = calibrate_retail(catalogue, args.runs, args.seed, args.win_rate,
                                      args.games_per_hour, args.achievement_games, hours_per_day=1) if args.calibrate_retail else None
    calibration = calibrate_retail(catalogue, args.runs, args.seed, args.win_rate,
                                   args.games_per_hour, args.achievement_games) if args.calibrate_retail else None
    # Owner 2026-10-31: report calibration, but keep the shipped Retail scale at 0.40.
    tiers = list(tiers) + [Tier("retail_reference" if daily == 1 else "retail_reference_2h", 1, 217, *income)
                           for daily, income in RETAIL_REFERENCES.items()]
    rows = []
    for tier in tiers:
        runs = [simulate(catalogue, tier, args.seed + run, args.win_rate,
                         args.games_per_hour, args.achievement_games) for run in range(args.runs)]

        def median(values):
            return statistics.median(values) if all(v is not None for v in values) else None

        win = reward_expectation(True, tier.reward_scale)
        loss = reward_expectation(False, tier.reward_scale)
        income = [(w * args.win_rate + l * (1 - args.win_rate)) * args.games_per_hour for w, l in zip(win, loss)]
        rows.append({"tier": tier.name, "reward_scale": tier.reward_scale,
                     "games_to_legend": tier.games_to_legend,
                     "hours": {name: median([r["hours"][name] for r in runs]) for name in runs[0]["hours"]},
                     "welcome_craft_value": median([r["welcome_craft_value"] for r in runs]),
                     "match_gold_per_hour": tier.fixed_gold_per_hour if tier.fixed_gold_per_hour is not None else income[0],
                     "match_packs_per_hour": tier.fixed_packs_per_hour,
                     "match_dust_per_hour": tier.fixed_dust_per_hour if tier.fixed_gold_per_hour is not None else income[1],
                     "total_gold_per_hour": median([r["total_gold_per_hour"] for r in runs]),
                     "total_dust_per_hour": median([r["total_dust_per_hour"] for r in runs])})
    assumptions = [part.strip().replace("\n  ", " ") for part in __doc__.split("Assumptions (also printed and included in JSON):\n", 1)[1]
                   .split("\nUsage (", 1)[0].split("\n*")]
    assumptions[0] = assumptions[0].lstrip("* ")
    return {"meta": catalogue.meta, "target_identities": len(catalogue.target),
            "target_copies": sum(1 if catalogue.rarity[cid] == "LEGENDARY" else 2 for cid in catalogue.target),
            "target_craft_value": catalogue.target_value,
            "pack_identities": len(catalogue.rarity), "pack_types": list(catalogue.pools),
            "runs": args.runs, "seed": args.seed, "win_rate": args.win_rate,
            "games_per_hour": args.games_per_hour, "achievement_games": args.achievement_games,
            "deck_costs": deck_cost_summary(catalogue), "retail_calibration": calibration,
            "retail_1h_calibration": calibration_1h,
            "welcome_deck_kit_gold_cap_ignored": 2500, "assumptions": assumptions, "tiers": rows}


def markdown(report):
    def fmt(value):
        return "n/a" if value is None else f"{value:.2f}"

    lines = [f"Meta: {report['meta']}; target: {report['target_identities']} identities / "
             f"{report['target_copies']} copies / {report['target_craft_value']:,} craft dust.",
             f"Medians: {report['runs']} runs; win rate {report['win_rate']:.0%}; "
             f"{report['games_per_hour']:g} games/h; achievements over {report['achievement_games']} games.",
             "", "All completion times in hours. Total income amortized through 100% completion.", "",
             "| Tier | 25% | 50% | 75% | 90% | 100% | One specific deck (X) | Match gold/h | Match packs/h | Match dust/h | Total gold/h | Total dust/h |",
             "|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|"]
    for row in report["tiers"]:
        values = [row["hours"][name] for name in ("25", "50", "75", "90", "100", "new_meta_deck")]
        values += [row[key] for key in ("match_gold_per_hour", "match_packs_per_hour", "match_dust_per_hour", "total_gold_per_hour", "total_dust_per_hour")]
        name = row["tier"].replace("|", "\\|").replace("\n", " ")
        lines.append("| " + name + " | " + " | ".join(map(fmt, values)) + " |")
    lines += ["", "Deck costs (dust and shop gold): " + json.dumps(report["deck_costs"]),
              "Retail calibration: " + json.dumps(report["retail_calibration"]),
              "Retail 1 h/day sensitivity: " + json.dumps(report["retail_1h_calibration"])]
    lines += ["", "Assumptions:", ""] + ["- " + a for a in report["assumptions"]]
    return "\n".join(lines)


def main(argv=None):
    parser = build_parser()
    args = parser.parse_args(argv)
    if args.runs <= 0 or args.achievement_games <= 0:
        parser.error("runs and achievement-games must be positive")
    if not 0 < args.win_rate <= 1 or not math.isfinite(args.games_per_hour) or args.games_per_hour <= 0:
        parser.error("win-rate must be in (0,1]; games-per-hour must be finite and positive")
    try:
        report = make_report(load_catalogue(args.root), args)
        payload = json.dumps(report, indent=2, allow_nan=False)
        if args.json and args.json != "-":
            Path(args.json).write_text(payload + "\n")
        print(payload if args.json == "-" else markdown(report))
    except (OSError, ValueError, KeyError, StopIteration, RuntimeError) as error:
        parser.exit(1, f"grind_pace: {error}\n")


if __name__ == "__main__":
    main()
