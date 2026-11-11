using System.IO.Compression;
using System.Text.Json;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

internal static class StandardBatchChecks
{
    internal static void Run(string root)
    {
#if ENGINE_SOURCE
        var batches = StandardBatchData.Load("tests/progression/batches");
        int checks = 0;
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
        using var input = File.OpenRead("data/carddb/37.0.0.254477/cards.json.gz");
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var export = JsonDocument.Parse(gzip);
        var exportById = export.RootElement.GetProperty("cards").EnumerateArray().ToDictionary(card => card.GetProperty("cardId").GetString());
        var cardClasses = export.RootElement.GetProperty("cards").EnumerateArray().ToDictionary(
            card => card.GetProperty("cardId").GetString(),
            card => card.GetProperty("cardClasses").EnumerateArray().Select(cls => cls.GetString()).ToHashSet());
        bool Legal(string id, CardClass hero) => cardClasses[id].Contains("NEUTRAL") || cardClasses[id].Contains(hero.ToString());
        var fabledCompanionIds = SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds
            .SelectMany(SabberStoneCore.Actions.FabledRules.Bundle).ToHashSet(StringComparer.Ordinal);
        string[] dualIds = "END_001 END_002 END_004 END_007 END_008 END_014 END_017 END_023 END_025 END_026 END_028 END_031 END_032".Split(' ');
        Check(dualIds.All(id => LocalDecks.TestLoaners.Any(deck => deck.CardIds.Contains(id)
            && Legal(id, Cards.FromId(deck.Hero).Class))), "all thirteen dual-class cards have an export-legal player loaner");
        var azalina = LocalDecks.Get("test-standard-azalina-priest");
        Check(azalina.Build().Count == 20 && azalina.Hero == "HERO_09" && azalina.CardIds.Contains("JAIL_430")
            && NativeUIProjection.DeckId(azalina.Id) == 900000049L
            && LocalDecks.OpponentFor(azalina.Id, Array.Empty<string>()).Id == "test-standard-batch-2-opp-rogue"
            && LocalDecks.TestStandardCardIds.Contains("JAIL_430"), "twenty-card Azalina loaner, native identity and inventory");
        Check(LocalDecks.Get("test-standard-mecha-priest").Build().Count == 30
            && !LocalDecks.Get("test-standard-mecha-priest").CardIds.Contains("JAIL_430"), "mecha priest remains thirty cards");
        var loaner = LocalDecks.Get(LocalDecks.TestStandardBatch1Id);
        var opponent = LocalDecks.Get(LocalDecks.TestStandardBatch1OpponentId);
        var batch2 = new[] {
            (Id: "test-standard-batch-2-warrior", Name: "Warrior", Hero: "HERO_01", NativeId: 900000012L,
                OpponentId: "test-standard-batch-2-opp-druid", OpponentHero: "HERO_06"),
            (Id: "test-standard-batch-2-priest", Name: "Priest", Hero: "HERO_09", NativeId: 900000013L,
                OpponentId: "test-standard-batch-2-opp-rogue", OpponentHero: "HERO_03"),
            (Id: "test-standard-batch-2-mage", Name: "Mage", Hero: "HERO_08", NativeId: 900000014L,
                OpponentId: "test-standard-batch-2-opp-warlock", OpponentHero: "HERO_07"),
            (Id: "test-standard-batch-2-paladin", Name: "Paladin", Hero: "HERO_04", NativeId: 900000015L,
                OpponentId: "test-standard-batch-2-opp-hunter", OpponentHero: "HERO_05"),
            (Id: "test-standard-batch-2-druid", Name: "Druid", Hero: "HERO_06", NativeId: 900000016L,
                OpponentId: "test-standard-batch-2-warrior", OpponentHero: "HERO_01"),
            (Id: "test-standard-batch-2-rogue", Name: "Rogue", Hero: "HERO_03", NativeId: 900000017L,
                OpponentId: "test-standard-batch-2-priest", OpponentHero: "HERO_09"),
            (Id: "test-standard-batch-2-warlock", Name: "Warlock", Hero: "HERO_07", NativeId: 900000018L,
                OpponentId: "test-standard-batch-2-mage", OpponentHero: "HERO_08"),
            (Id: "test-standard-batch-2-hunter", Name: "Hunter", Hero: "HERO_05", NativeId: 900000019L,
                OpponentId: "test-standard-batch-2-paladin", OpponentHero: "HERO_04"),
            (Id: "test-standard-batch-2-shaman", Name: "Shaman", Hero: "HERO_02", NativeId: 900000020L,
                OpponentId: "test-standard-batch-2-opp-druid", OpponentHero: "HERO_06"),
            (Id: "test-standard-batch-2-demonhunter", Name: "Demon Hunter", Hero: "HERO_10", NativeId: 900000021L,
                OpponentId: "test-standard-batch-2-opp-rogue", OpponentHero: "HERO_03")
        };
        Check(LocalDecks.TestLoaners.Where(deck => deck.Id.StartsWith("test-standard-batch-2-", StringComparison.Ordinal))
            .Select(deck => deck.Id).ToHashSet().SetEquals(batch2.Select(spec => spec.Id)), "exactly ten Standard batch 2 player loaners");
        Check(LocalDecks.TestOpponents.Where(deck => deck.Id.StartsWith("test-standard-batch-2-", StringComparison.Ordinal))
            .Select(deck => deck.Id).ToHashSet().SetEquals(batch2.Take(4).Select(spec => spec.OpponentId)), "exactly four Standard batch 2 fixed opponents");
        var batch2Decks = batch2.SelectMany(spec => new[] { LocalDecks.Get(spec.Id), LocalDecks.Get(spec.OpponentId) }).ToArray();
        // Freeze batches 1+2 independently of later registrations and loaner edits, including engine-only cards.
        string[] batch1And2Ids = batches["batch1-and2"];
        Check(batch1And2Ids.Length == 106 && batch1And2Ids.Distinct().Count() == 106
            && batch1And2Ids.All(SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds.Contains), "batches 1+2 retain 106 registered cards");
        foreach (var deck in LocalDecks.TestLoaners.Concat(LocalDecks.TestOpponents).Where(deck => deck.Id != LocalDecks.TestCenariusId))
        {
            var cards = deck.Build();
            Check(cards.Count == LocalDecks.DeckSize(deck.CardIds) && cards.All(card => (card.Collectible || fabledCompanionIds.Contains(card.Id)) && card.Implemented), "Standard test deck must be complete and implemented");
            var hero = Cards.FromId(deck.Hero);
            Check(hero != null && hero.Type == CardType.HERO, "Standard test hero exists in engine: " + deck.Id);
            Check(cards.All(card => Legal(card.Id, hero.Class) && LocalDecks.IsAllowedInDeck(card, hero.Class)), "Standard test deck class");
            LocalDecks.ValidateRunes(cards, hero.Class);
            if (deck.Hero == "HERO_11")
            foreach (var card in cards)
            {
                var printed = exportById[card.Id];
                Check(card.AssetId == printed.GetProperty("dbfId").GetInt32(), "DK loaner export identity: " + card.Id);
                foreach (int tag in new[] { 2196, 2197, 2198, 4058 })
                {
                    int expected = printed.GetProperty("tags").TryGetProperty(tag.ToString(), out var value) ? value.GetInt32() : 0;
                    Check(card[(GameTag)tag] == expected, "DK loaner export runes/spender: " + card.Id);
                    Check(NativeUIProjection.TryGetDefinitionTag(true, true, true, card.Id, tag, out int projected) && projected == expected,
                        "DK definition rune/spender projection: " + card.Id);
                    Check(NativeUIProjection.TryGetDbfTag(true, card.Id, tag, out projected) && projected == expected,
                        "DK DBF rune/spender projection: " + card.Id);
                }
            }
            Check(cards.GroupBy(card => card.Id).All(group => group.Count() <= (group.First().Rarity == Rarity.LEGENDARY ? 1 : 2)), "Standard test deck copy limits");
            if (deck == loaner || (LocalDecks.TestOpponents.Contains(deck) && !deck.Id.StartsWith("showcase-", StringComparison.Ordinal)))
                Check(cards.All(card => batch1And2Ids.Contains(card.Id)), "frozen Standard decks retain batch 1+2 cards");
            else if (deck.Id != LocalDecks.TestDecisionsId && deck.Id != LocalDecks.TestRushSilenceId && !deck.Id.StartsWith("test-deathknight-", StringComparison.Ordinal))
                Check(cards.All(card => SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds.Contains(card.Id) || fabledCompanionIds.Contains(card.Id)),
                    "class loaners contain registered Standard cards or their Fabled companions only");
            Check(LocalCardCatalog.WithinCopyLimits(deck.CardIds), "Standard test deck grouped copy limits");
            Check(cards.Where(card => SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds.Contains(card.Id) && !LocalCoreProgression.IsAvailable(card.Id)).All(card => !LocalCardCatalog.Supported.Any(owned => owned.Id == card.Id)
                && !LocalEconomy.Catalog.Any(owned => owned.Id == card.Id)
                && !LocalCoreProgression.ById.ContainsKey(card.Id)), "test cards cannot enter collection or Core catalog");
        }
        Check(loaner.Hero == "HERO_01" && opponent.Hero == "HERO_05" && loaner.CardIds.Distinct().Count() == 25
            && opponent.CardIds.Distinct().Count() == 22, "Standard test deck heroes and distinct counts");
        Check(loaner.Name == "TEST: Standard batch 1" && loaner.Strategy.Contains("Test loaner") && loaner.Strategy.Contains("Not a starter"), "test loaner label");
        Check(LocalDecks.OpponentFor(loaner.Id, new[] { "mage" }) == opponent, "Standard fixed opponent mapping");
        Check(LocalDecks.All.All(deck => deck.Id != loaner.Id && deck.Id != opponent.Id)
            && LocalDecks.NextOpponent(new[] { opponent.Id }).Id == LocalDecks.NextOpponent(Array.Empty<string>()).Id, "Standard decks excluded from normal rotation");
        var ids = loaner.CardIds.ToArray();
        NativeUIProjection.ValidateSelection(loaner.Id, loaner.Hero, ids, new Dictionary<string, int>());
        long nativeId = NativeUIProjection.DeckId(loaner.Id);
        Check(nativeId == 900000011L && NativeUIProjection.IsLoanerId(nativeId) && !NativeUIProjection.IsPersonalId(nativeId)
            && NativeUIProjection.IsExactLoaner(nativeId, loaner.Hero, ids), "Standard native deck ID and immutable binding");
        var tampered = ids.ToArray(); tampered[0] = opponent.CardIds.First(id => !ids.Contains(id));
        bool rejected = false;
        try { NativeUIProjection.ValidateSelection(loaner.Id, loaner.Hero, tampered, new Dictionary<string, int>()); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "test deck support cannot admit altered loaner contents");
        foreach (var spec in batch2)
        {
            var deck = LocalDecks.Get(spec.Id);
            var cardIds = deck.CardIds.ToArray();
            Check(deck.Hero == spec.Hero && deck.Name == "TEST: Standard " + spec.Name
                && deck.Strategy.Contains("Test loaner") && deck.Strategy.Contains("Not a starter"), "batch 2 class and test label: " + spec.Id);
            var fixedOpponent = LocalDecks.Get(spec.OpponentId);
            Check(fixedOpponent.Hero == spec.OpponentHero && LocalDecks.OpponentFor(spec.Id, new[] { opponent.Id, "mage" }) == fixedOpponent,
                "batch 2 fixed opponent mapping: " + spec.Id);
            bool opponentHasNativeId = true;
            try { NativeUIProjection.DeckId(fixedOpponent.Id); }
            catch (ArgumentException) { opponentHasNativeId = false; }
            Check(opponentHasNativeId == LocalDecks.TestLoaners.Contains(fixedOpponent), "opponent native identity follows player loaner status: " + spec.OpponentId);
            NativeUIProjection.ValidateSelection(spec.Id, spec.Hero, cardIds, new Dictionary<string, int>());
            Check(NativeUIProjection.DeckId(spec.Id) == spec.NativeId && NativeUIProjection.IsLoanerId(spec.NativeId)
                && !NativeUIProjection.IsPersonalId(spec.NativeId) && NativeUIProjection.IsExactLoaner(spec.NativeId, spec.Hero, cardIds.Reverse()),
                "batch 2 native deck ID and immutable binding: " + spec.Id);
            var altered = cardIds.ToArray(); altered[0] = "CS2_182";
            bool alteredRejected = false, heroRejected = false;
            try { NativeUIProjection.ValidateSelection(spec.Id, spec.Hero, altered, new Dictionary<string, int>()); }
            catch (InvalidOperationException) { alteredRejected = true; }
            string wrongHero = spec.Hero == "HERO_08" ? "HERO_01" : "HERO_08";
            try { NativeUIProjection.ValidateSelection(spec.Id, wrongHero, cardIds, new Dictionary<string, int>()); }
            catch (InvalidOperationException) { heroRejected = true; }
            Check(alteredRejected && heroRejected && !NativeUIProjection.IsExactLoaner(spec.NativeId, spec.Hero, altered)
                && !NativeUIProjection.IsExactLoaner(spec.NativeId, wrongHero, cardIds), "batch 2 altered contents and hero rejected: " + spec.Id);
        }
        foreach (var deck in batch2Decks)
        {
            Check(LocalDecks.All.All(normal => normal.Id != deck.Id)
                && LocalDecks.NextOpponent(new[] { "mage", deck.Id }).Id == LocalDecks.NextOpponent(new[] { "mage" }).Id,
                "batch 2 deck excluded from normal rotation: " + deck.Id);
        }
        string[] batch2Ids = File.ReadAllText("tests/progression/fixtures/batch2.txt").Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        var playerCovered = LocalDecks.TestLoaners.SelectMany(deck => deck.CardIds).ToHashSet();
        Check(batch2Ids.Length == 56 && batch2Ids.All(playerCovered.Contains), "player loaners cover all 56 batch 2 cards");
        Check(batch2Ids.All(id => LocalDecks.TestStandardCardIds.Contains(id)
            && NativeUIProjection.ForcesTestPlayability(true, id)), "all batch 2 cards enter test-only native playability");
        Check(LocalDecks.TestStandardCardIds.All(SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds.Contains),
            "only registered cards enter Standard test playability");
        // Independent snapshots: these batch-1+2 recipes must not follow player loaner edits.
        var frozenOpponents = new Dictionary<string, string> {
            ["druid"] = "END_035 END_035 TIME_434 TIME_434 TLC_427 TLC_427 TLC_468 TLC_468 TLC_621 TLC_621 TLC_831 TLC_831 EDR_495 EDR_495 BE_850t BE_856 BE_859 DINO_130 DINO_421 DINO_432 EDR_272 TIME_703 TLC_233 TLC_234 TLC_237 DINO_410 FIR_958 BE_994 CATA_111 CATA_720",
            ["rogue"] = "TLC_468 TLC_468 TLC_621 TLC_621 TLC_831 TLC_831 EDR_495 EDR_495 BE_994 BE_994 CATA_111 CATA_111 END_035 END_035 TIME_434 TIME_434 TLC_427 TLC_427 BE_725 CAP_001 CATA_201 DINO_408 EDR_523 JAIL_720 TIME_710 FIR_958 CATA_720 DINO_410 BE_193 CATA_612",
            ["warlock"] = "TLC_831 TLC_831 EDR_495 EDR_495 BE_994 BE_994 CATA_111 CATA_111 END_035 END_035 TIME_434 TIME_434 TLC_427 TLC_427 TLC_468 TLC_468 TLC_621 TLC_621 EDR_485 JAIL_399 JAIL_513 TIME_025 TIME_028 TIME_029 FIR_958 CATA_720 DINO_410 BE_193 CATA_612 DINO_411",
            ["hunter"] = "TLC_427 TLC_427 TLC_468 TLC_468 TLC_621 TLC_621 TLC_831 TLC_831 EDR_495 EDR_495 TIME_434 DINO_403 BE_550 BE_732 CATA_558 EDR_014 EDR_261 EDR_481 JAIL_882 MEND_302 TIME_601 TIME_603 TIME_605 TLC_826 DINO_410 FIR_958 BE_994 CATA_111 CATA_720 END_035"
        };
        foreach (var recipe in frozenOpponents)
        {
            var frozen = LocalDecks.Get("test-standard-batch-2-opp-" + recipe.Key);
            Check(frozen.CardIds.SequenceEqual(recipe.Value.Split(' ')), "frozen opponent recipe: " + recipe.Key);
            Check(!frozen.CardIds.SequenceEqual(LocalDecks.Get("test-standard-batch-2-" + recipe.Key).CardIds),
                "frozen opponent differs from player loaner: " + recipe.Key);
            Check(batch1And2Ids.Select(Cards.FromId).Where(card => card.Class == Cards.FromId(frozen.Hero).Class)
                .All(card => frozen.CardIds.Contains(card.Id)), "frozen opponent keeps batch 1+2 class coverage: " + recipe.Key);
        }
        var demonHunter = LocalDecks.Get("test-standard-batch-2-demonhunter");
        Check(demonHunter.CardIds.Contains("TLC_902") && demonHunter.Build().Count(card => card.Class == CardClass.DEMONHUNTER) == 30,
            "DH loaner covers all thirty registered class cards");
        var visibleIds = NativeUIProjection.ExpectedNativeSortOrders(new[] { new KeyValuePair<long, long>(910000010L, 0) }, NativeUIProjection.ProjectedLoanerKeys(1, "default"));
        Check(visibleIds.Count == 26 && visibleIds.Count < 27
            && visibleIds.Values.Distinct().Count() == visibleIds.Count && batch2.All(spec => visibleIds.ContainsKey(spec.NativeId)),
            "visible baseline decks fit native 27-deck limit with unique IDs and ranks");
        foreach (string id in LocalDecks.TestStandardCardIds)
        {
            var card = Cards.FromId(id);
            Check(NativeUIProjection.ForcesTestPlayability(true, id) && !NativeUIProjection.ForcesTestPlayability(false, id), "active-only test playability");
            Check(NativeUIProjection.SupportsPreview(id), "test card definition projection");
            foreach (var tag in new[] { GameTag.COST, GameTag.ATK, GameTag.HEALTH })
            {
                int expected = tag == GameTag.HEALTH && card.Type == CardType.WEAPON ? card[GameTag.DURABILITY] : card[tag];
                Check(NativeUIProjection.TryGetDefinitionTag(true, true, true, id, (int)tag, out int value) && value == expected, "test card printed definition stats");
            }
            Check(LocalCoreProgression.IsAvailable(id) || !NativeUIProjection.Supported.Contains(id),
                "non-Core test cards excluded from pack receipt and personal deck support");
        }
        foreach (string id in new[] { null, "CORE_CS3_002", "EX1_577", "CS2_182" })
            Check(!NativeUIProjection.ForcesTestPlayability(true, id), "test playability scope must stay exact: " + id);
        string[] batch3Ids = batches["batch3"];
        Check(batch3Ids.Length == 57 && batch3Ids.Distinct().Count() == 57
            && batch3Ids.All(SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds.Contains),
            "batch 3 retains 57 registered cards");
        // Batch 4 added primitives; batches 5+6 added these explicit collectible registrations.
        string[] batch5Ids = batches["batch5"];
        string[] batch6Ids = batches["batch6"];
        Check(batch5Ids.Length == 16 && batch5Ids.Distinct().Count() == 16
            && batch5Ids.All(SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds.Contains), "batch 5 retains 16 registered cards");
        Check(batch6Ids.Length == 149 && batch6Ids.Distinct().Count() == 149
            && batch6Ids.All(SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds.Contains), "batch 6 retains 149 registered cards");
        // Batch 7: PREPARE (Violet Hold) and HERALD (Cataclysm) family cards.
        string[] batch7Ids = batches["batch7"];
        Check(batch7Ids.Length == 13 && batch7Ids.Distinct().Count() == 13
            && batch7Ids.All(SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds.Contains), "batch 7 retains 13 registered cards");
        // Batch 8: first player-decision consumers (choose a card in your hand).
        string[] batch8Ids = batches["batch8"];
        Check(batch8Ids.All(SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds.Contains), "batch 8 retains 3 registered cards");
        string[] poolPilotIds = batches["pool-pilot"];
        Check(poolPilotIds.Length == 9 && poolPilotIds.All(SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds.Contains), "pool pilot retains 9 registered cards");
        // Current CORE cards whose legacy originals are unscripted (Sets/CORE slices).
        string[] coreIds = batches["core"];
        Check(coreIds.Length == 62 && coreIds.Distinct().Count() == 62
            && coreIds.All(SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds.Contains), "current CORE slices retain 62 registered cards");
        string[] questBatchIds = batches["quest-batch"];
        Check(questBatchIds.All(SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds.Contains), "quest pilot modules registered");
        string[] locationsPilotIds = batches["locations-pilot"];
        Check(locationsPilotIds.Length == 6 && locationsPilotIds.All(id =>
            SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds.Contains(id)),
            "locations pilot retains six collectible registrations");
        string[] deathKnightIds = batches["death-knight"];
        Check(deathKnightIds.Length == 23 && deathKnightIds.All(SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds.Contains),
            "Death Knight batch retains 23 registered cards");
        // Batch 9 (offline pools, player choices, ready cards): one line per landed slice.
        string[] batch9Ids = batches["batch9"];
        string[] darkGiftIds = batches["dark-gift"];
        Check(batch9Ids.Distinct().Count() == batch9Ids.Length
            && batch9Ids.All(SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds.Contains), "batch 9 retains its registered cards");
        Check(darkGiftIds.Distinct().Count() == darkGiftIds.Length
            && darkGiftIds.All(SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds.Contains), "dark gift retains its registered cards");
        string[] outcastIds = batches["outcast"];
        Check(outcastIds.Length == 3 && outcastIds.All(SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds.Contains), "Outcast batch retains three registered cards");
        string[] selectorIds = batches["selector"];
        Check(selectorIds.Length == 7 && selectorIds.All(SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds.Contains),
            "selector batch retains seven registered cards");
        // History-facts consumers and history-blocked cards (feat/history-facts); seated in the group-4/DK seat loaners.
        string[] historyFactsIds = batches["history-facts"];
        Check(historyFactsIds.Distinct().Count() == historyFactsIds.Length
            && historyFactsIds.All(SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds.Contains), "history-facts cards are registered");
        // Cost rules (feat/cost-rules); seated in the group-4/DK seat loaners.
        string[] costRulesIds = batches["cost-rules"];
        Check(costRulesIds.Length == 23 && costRulesIds.All(SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds.Contains),
            "cost rules retains twenty-three registered collectibles");
        // Mech families: one line per landed slice; each slice seats its cards with coordinator mech-family bookkeeping (group-6 loaners).
        string[] mechFamiliesIds = batches["mech-families"];
        Check(mechFamiliesIds.Distinct().Count() == mechFamiliesIds.Length
            && mechFamiliesIds.All(SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds.Contains), "mech families retains its registered cards");
        string[] rewindIds = batches["rewind"]; /*RW-END*/
        Check(rewindIds.Length == 18 && rewindIds.All(SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds.Contains),
            "Rewind retains eighteen registered collectibles including both generators");
        string[] keywordsCfaIds = batches["keywords-cfa"];
        foreach (var deck in LocalDecks.All.Concat(LocalDecks.TestLoaners))
            Check(!SabberStoneCore.Actions.FabledRules.DeckErrors(deck.CardIds).Any(), "complete Fabled bundles: " + deck.Id);
        foreach (Action invalid in new Action[] {
            () => LocalDecks.ValidateFabled(new[] { "TIME_609" }),
            () => new LocalDeck("invalid", "TEST: invalid", "HERO_05", "test", "test", DeckStyle.Value, Array.Empty<string>(), new[] { "TIME_609" }),
            () => LocalDecks.Draft("Incomplete bundle", "HERO_05", new[] { "TIME_609" }),
            () => LocalDecks.Custom("Incomplete bundle", "HERO_05", new[] { "TIME_609" }.Concat(Enumerable.Repeat("CS2_182", 29))),
            () => NativeUIProjection.ValidateSelection("paladin", "HERO_04", new[] { "TIME_609" }.Concat(Enumerable.Repeat("CS2_182", 29)).ToArray(), new Dictionary<string, int>())
        })
        {
            bool bundleRejected = false;
            try { invalid(); } catch (InvalidOperationException error) { bundleRejected = error.Message.StartsWith("Fabled bundle", StringComparison.Ordinal); }
            Check(bundleRejected, "Fabled incomplete bundle rejected at local/native deck boundary");
        }
        var cfa = LocalDecks.Get("test-keywords-cfa-paladin");
        Check(cfa.Name == "TEST: Keywords CFA Paladin" && cfa.Hero == "HERO_04"
            && cfa.CardIds.Count == 30 && cfa.CardIds.Count(id => id == "CATA_432") == 1
            && NativeUIProjection.DeckId(cfa.Id) == 900000130L
            && LocalDecks.OpponentFor(cfa.Id, Array.Empty<string>()).Id == "test-standard-batch-2-opp-hunter", "keyword fixture identity and fixed opponent");
        var cfaHunter = LocalDecks.Get("test-keywords-cfa-hunter");
        string[] hunterBundle = { "TIME_609", "TIME_609t1", "TIME_609t2" };
        Check(cfaHunter.Name == "TEST: Keywords CFA Hunter" && cfaHunter.Hero == "HERO_05"
            && cfaHunter.CardIds.Count == 30 && cfaHunter.CardIds.Take(3).SequenceEqual(hunterBundle)
            && hunterBundle.All(id => cfaHunter.CardIds.Count(cardId => cardId == id) == 1)
            && NativeUIProjection.DeckId(cfaHunter.Id) == 900000131L
            && LocalDecks.OpponentFor(cfaHunter.Id, Array.Empty<string>()).Id == "test-standard-batch-2-opp-hunter",
            "Hunter keyword fixture seats complete Fabled bundle with fixed identity and opponent");
        var colossalSeats = new[] {
            (Id: "test-keywords-cfa-warrior", Class: "Warrior", Hero: "HERO_01", Native: 900000132L, Card: "CATA_150"),
            (Id: "test-keywords-cfa-shaman", Class: "Shaman", Hero: "HERO_02", Native: 900000133L, Card: "CATA_153"),
            (Id: "test-keywords-cfa-priest", Class: "Priest", Hero: "HERO_09", Native: 900000134L, Card: "CATA_300"),
            (Id: "test-keywords-cfa-mage", Class: "Mage", Hero: "HERO_08", Native: 900000135L, Card: "CATA_488")
        };
        foreach (var spec in colossalSeats)
        {
            Check(LocalDecks.TestLoaners.Any(deck => deck.Id == spec.Id), "Colossal keyword loaner exists: " + spec.Id);
            var deck = LocalDecks.Get(spec.Id);
            Check(deck.Name == "TEST: Keywords CFA " + spec.Class && deck.Hero == spec.Hero
                && deck.CardIds.Count == 30 && deck.CardIds[0] == spec.Card && deck.CardIds.Count(id => id == spec.Card) == 1
                && NativeUIProjection.DeckId(deck.Id) == spec.Native
                && NativeUIProjection.IsExactLoaner(spec.Native, spec.Hero, deck.CardIds.ToArray())
                && LocalDecks.OpponentFor(deck.Id, Array.Empty<string>()).Id == "test-standard-batch-2-opp-hunter",
                "Colossal keyword fixture seats body with fixed identity and opponent: " + spec.Id);
        }
        Check(NativeUIProjection.TestLoanerGroups[7].SequenceEqual(new[] { cfa.Id, cfaHunter.Id }.Concat(colossalSeats.Select(spec => spec.Id))),
            "keyword group retains creation order");
        string[] cardStateIds = batches["card-state"];
        var admitted = OfflinePoolPolicy.Load().BuildManifest();
        using var eligibility = JsonDocument.Parse(File.ReadAllText("data/pools/pool-eligibility-v1.json"));
        using var setOverrides = JsonDocument.Parse(File.ReadAllText("data/carddb/37.0.0.254477/set-overrides.json"));
        var policy = OfflinePoolPolicy.Load();
        var knownExtras = policy.Metas.SelectMany(m => m.ExtraCards).ToHashSet();
        var eligibleIds = eligibility.RootElement.GetProperty("records").EnumerateArray().Select(r => r.GetProperty("cardId").GetString())
            .Concat(policy.Members.Where(m => knownExtras.Contains(m.CardId)).Select(m => m.CardId)).Distinct();
        var eligible = eligibleIds.Select(id => {
            var card = exportById[id];
            string set = card.GetProperty("cardSet").GetProperty("name").GetString();
            if (set == "INVALID") set = setOverrides.RootElement.GetProperty("overrides").GetProperty(card.GetProperty("dbfId").ToString()).GetProperty("set").GetString();
            bool standard = admitted.Sets.Contains(set) || policy.ExtraCards.Contains(id);
            return (Id: id, Standard: standard,
                Inactive: !standard && (policy.Metas.Any(meta => meta.Sets.Contains(set)) || knownExtras.Contains(id)));
        }).Where(c => !c.Inactive).ToArray();
        Check(cardStateIds.Length == 22
            && admitted.Standard.Select(m => m.CardId).ToHashSet().SetEquals(eligible.Where(c => c.Standard).Select(c => c.Id))
            && admitted.Past.Select(m => m.CardId).ToHashSet().SetEquals(eligible.Where(c => !c.Standard).Select(c => c.Id))
            && cardStateIds.All(id => admitted.Standard.Any(member => member.CardId == id)),
            "card-state admission: all 22 collectibles; Standard/Past match eligibility/export");
        Check(LocalDecks.Get("test-standard-cardstate-shaman").CardIds.Count(id => id != "JAIL_801" && Cards.FromId(id).Type == CardType.SPELL) >= 3,
            "Molten Gold loaner can cast three other spells while holding its fixture");
        // Batch 10: one line per landed slice; each slice seats its cards with coordinator batch-10 bookkeeping (group-5 loaners).
        string[] batch10Ids = batches["batch10"];
        Check(batch10Ids.Distinct().Count() == batch10Ids.Length
            && batch10Ids.All(SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds.Contains), "batch 10 retains its registered cards");
        // Batch 11: legacy-scripted CORE registrations (owner parity decision); seated with coordinator batch-11 bookkeeping (group 10).
        string[] batch11Ids = batches["batch11"];
        Check(batch11Ids.Distinct().Count() == batch11Ids.Length
            && batch11Ids.All(SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds.Contains), "batch 11 retains its registered cards");
        string[] batch12Ids = batches["batch12"];
        Check(batch12Ids.Distinct().Count() == batch12Ids.Length
            && batch12Ids.All(SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds.Contains), "batch 12 retains its registered cards");
        string[] costMemoryIds = batches["cost-memory"];
        Check(costMemoryIds.All(SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds.Contains), "cost memory retains its registered cards");
        string[] payloadIds = batches["payload"];
        Check(payloadIds.Length == 9 && payloadIds.Distinct().Count() == 9
            && payloadIds.All(SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds.Contains), "payload slice retains nine registered cards");
        string[] discoverRealIds = batches["discover-real"];
        string[] companionIds = batches["companion"];
        string[] bonusIds = batches["bonus"];
        var allBatchIds = batches.Values.SelectMany(ids => ids).ToArray();
        Check(allBatchIds.Distinct().Count() == allBatchIds.Length, "Standard batch lists must not overlap");
        Check(allBatchIds.ToHashSet(StringComparer.Ordinal).SetEquals(SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds),
            "explicit batch lists exactly cover registered Standard collectibles");
        // Historical modules are implemented for generation, never for Standard player decks.
        var exclusions = batches["ev-colossals"].ToDictionary(id => id,
            _ => "Past-only Colossal: generated by Primordial Lord, not Standard-deckable", StringComparer.Ordinal);
        var registry = SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds.ToHashSet(StringComparer.Ordinal);
        var pastColossalSets = new[] { "THE_SUNKEN_CITY", "RETURN_OF_THE_LICH_KING" };
        var pastColossals = registry.Where(id => Cards.FromId(id)[(GameTag)2247] > 0
            && pastColossalSets.Contains(exportById[id].GetProperty("cardSet").GetProperty("name").GetString()))
            .ToHashSet(StringComparer.Ordinal);
        Check(exclusions.Count == 13 && pastColossals.SetEquals(exclusions.Keys)
            && pastColossals.All(id => Cards.FromId(id).Implemented && Cards.FromId(id)[(GameTag)2247] > 0),
            "all thirteen historical Colossals stay implemented but not Standard-deckable");
        // These three historical identities already have compatibility seats in the baseline.
        var generatedOnly = admitted.Past.Select(m => m.CardId).Where(registry.Contains)
            .Except(new[] { "CORE_FP1_011", "YOG_514", "CORE_EDR_004" }).ToHashSet(StringComparer.Ordinal);
        foreach (var id in generatedOnly.Except(exclusions.Keys).ToArray())
            exclusions.Add(id, "Past-only module: generated, not Standard-deckable");
        Check(generatedOnly.SetEquals(exclusions.Keys) && generatedOnly.All(id => Cards.FromId(id).Implemented),
            "historical module batches exactly cover admitted generated-only registrations");
        var playerClasses = LocalDecks.PlayerHeroes.Select(id => Cards.FromId(id).Class.ToString()).Append("NEUTRAL").ToHashSet();
        var derivedExclusions = registry.Where(id => generatedOnly.Contains(id) || !cardClasses[id].Overlaps(playerClasses)).ToHashSet(StringComparer.Ordinal);
        Check(registry.Count == allBatchIds.Length
            && derivedExclusions.SetEquals(exclusions.Keys) && exclusions.Values.All(reason => !string.IsNullOrWhiteSpace(reason)),
            "explicit exclusions match Past-only cards or unsupported classes and carry reasons");
        foreach (var id in new[] { "BE_036", "BE_EVENT_100", "BE_EVENT_101", "BE_EVENT_102" }
            .Where(id => !LocalCardCatalog.InActiveMeta(Cards.FromId(id))))
            exclusions.Add(id, "Early-admission seat uses filler until the installed catalog admits this card");
        var registeredPlayerCoverage = playerCovered.Where(registry.Contains).ToHashSet(StringComparer.Ordinal);
        Check(!exclusions.Keys.Any(playerCovered.Contains)
            && registeredPlayerCoverage.Count + exclusions.Count == registry.Count
            && registeredPlayerCoverage.Concat(exclusions.Keys).ToHashSet(StringComparer.Ordinal).SetEquals(registry),
            "all registered cards: legal player loaners plus reasoned exclusions, no pending seats; missing=" + string.Join(",", registry.Except(registeredPlayerCoverage).Except(exclusions.Keys)));
        foreach (var (label, sliceIds) in batches)
            Check(sliceIds.All(id => playerCovered.Contains(id) || exclusions.ContainsKey(id)), label + " has no pending loaner seats");
        Check(LocalDecks.TestStandardCardIds.ToHashSet(StringComparer.Ordinal).SetEquals(registeredPlayerCoverage),
            "opponent-only coverage cannot conceal a missing player card");
        // Groups: numbered tray pages; aliases keep old selector values. The showcase leads every page and belongs to no
        // group; every other loaner belongs to exactly one group.
        var groups = NativeUIProjection.TestLoanerGroups;
        var showcases = NativeUIProjection.ShowcaseLoanerKeys;
        var testKeys = LocalDecks.TestLoaners.Select(deck => deck.Id).ToArray();
        Check(showcases.SequenceEqual(new[] { LocalDecks.TestShowcaseId }) && showcases.All(testKeys.Contains)
            && !groups.SelectMany(group => group).Any(showcases.Contains), "one showcase loaner outside every group");
        Check(groups.Count == 11 && groups.All(group => group.Count > 0 && showcases.Count + group.Count <= NativeUIProjection.MaximumTestLoanerGroupSize)
            && groups.SelectMany(group => group).Count() == testKeys.Length - showcases.Count
            && groups.SelectMany(group => group).Concat(showcases).ToHashSet(StringComparer.Ordinal).SetEquals(testKeys),
            "loaner groups partition every non-showcase test loaner into tray pages of at most fifteen with the showcase");
        string[] groupNames = Enumerable.Range(1, groups.Count).Where(i => groups[i - 1].Count > 0).Select(i => i.ToString()).ToArray();
        Check(NativeUIProjection.CanonicalTestLoanerGroup("default") == "1" && NativeUIProjection.CanonicalTestLoanerGroup(" EXTRA ") == "2"
            && NativeUIProjection.CanonicalTestLoanerGroup("DeathKnight") == "3" && NativeUIProjection.CanonicalTestLoanerGroup("10") == "10"
            && NativeUIProjection.CanonicalTestLoanerGroup("11") == "11" && NativeUIProjection.CanonicalTestLoanerGroup("showcase") == "11"
            && NativeUIProjection.CanonicalTestLoanerGroup("12") == null && NativeUIProjection.CanonicalTestLoanerGroup("010") == null
            && NativeUIProjection.CanonicalTestLoanerGroup("0") == null && groupNames.All(name => NativeUIProjection.CanonicalTestLoanerGroup(name) == name),
            "numbered groups with default/extra/deathknight aliases");
        for (int personalCount = 0; personalCount <= 3; personalCount++)
        {
            var reachable = new HashSet<string>(StringComparer.Ordinal);
            var reachableLoaners = new HashSet<string>(StringComparer.Ordinal);
            var personals = Enumerable.Range(0, personalCount)
                .Select(i => new KeyValuePair<long, long>(910000010L + i, i)).ToArray();
            foreach (string group in groupNames)
            {
                var projected = NativeUIProjection.ProjectedLoanerKeys(personalCount, group);
                var ranks = NativeUIProjection.ExpectedNativeSortOrders(personals, projected);
                Check(ranks.Count <= 26 && personals.All(personal => ranks[personal.Key] == personal.Value),
                    $"group {group} tray preserves personals and New Deck slot at P={personalCount}");
                var visible = projected.Where(key => ranks.ContainsKey(NativeUIProjection.DeckId(key))).ToArray();
                if (group == "11")
                {
                    Check(visible.SequenceEqual(NativeUIProjection.PlayerLoanerKeys), "group 11 exposes only player defaults");
                    reachableLoaners.UnionWith(visible);
                    reachable.UnionWith(visible.SelectMany(key => LocalDecks.Get(key).CardIds).Where(registry.Contains));
                    continue;
                }
                Check(groups[int.Parse(group) - 1].All(visible.Contains), $"group {group} shows all its members at P={personalCount}");
                var visibleTests = visible.Where(key => testKeys.Contains(key))
                    .OrderBy(key => ranks[NativeUIProjection.DeckId(key)]).ToArray();
                Check(visibleTests.Take(showcases.Count).SequenceEqual(showcases)
                    && visibleTests.Skip(showcases.Count).Take(groups[int.Parse(group) - 1].Count).SequenceEqual(groups[int.Parse(group) - 1]),
                    $"group {group} ranks the showcase first, then its members, at P={personalCount}");
                reachableLoaners.UnionWith(visible.Where(key => testKeys.Contains(key)));
                reachable.UnionWith(visible.SelectMany(key => LocalDecks.Get(key).CardIds).Where(registry.Contains));
            }
            foreach (var alias in new[] { ("default", "1"), ("extra", "2"), ("deathknight", "3") })
                Check(NativeUIProjection.ProjectedLoanerKeys(personalCount, alias.Item1).SequenceEqual(NativeUIProjection.ProjectedLoanerKeys(personalCount, alias.Item2)),
                    $"alias {alias.Item1} projects group {alias.Item2} at P={personalCount}");
            var trayKeys = testKeys.Except(groups[10]).Concat(NativeUIProjection.PlayerLoanerKeys).ToArray();
            Check(reachableLoaners.SetEquals(trayKeys), $"native tray reaches coverage seats and the player defaults at P={personalCount}; reached {reachableLoaners.Count}");
            Check(reachable.SetEquals(registeredPlayerCoverage) && reachable.SetEquals(registry.Except(exclusions.Keys)),
                $"native tray reaches all {registeredPlayerCoverage.Count} covered cards while preserving {personalCount} personal decks; reached {reachable.Count}");
            Console.WriteLine($"PASS native tray coverage P={personalCount}: {reachable.Count}/{registry.Count} cards, {reachableLoaners.Count}/{testKeys.Length} loaners across groups {string.Join(", ", groupNames)}");
        }
        var nativeIds = LocalDecks.TestLoaners.Select(deck => NativeUIProjection.DeckId(deck.Id)).ToArray();
        Check(nativeIds.Distinct().Count() == nativeIds.Length && nativeIds.All(id => (id >= 900000010L && id <= 900000079L) || (id >= 900000130L && id <= 900000149L) || (id >= 900000150L && id <= 900000158L) || (id >= 900000170L && id <= 900000179L) || (id >= 900000200L && id <= 900000211L)),
            "test loaner native ids are unique inside the reserved bands");
        foreach (var deck in LocalDecks.TestLoaners)
        {
            long id = NativeUIProjection.DeckId(deck.Id);
            NativeUIProjection.ValidateSelection(deck.Id, deck.Hero, deck.CardIds.ToArray(), new Dictionary<string, int>());
            Check(NativeUIProjection.IsLoanerId(id) && !NativeUIProjection.IsPersonalId(id)
                && NativeUIProjection.IsExactLoaner(id, deck.Hero, deck.CardIds), "every loaner has an immutable reserved id: " + deck.Id);
            Check(LocalDecks.NextOpponent(new[] { "mage", deck.Id }).Id == LocalDecks.NextOpponent(new[] { "mage" }).Id,
                "every test loaner stays outside normal rotation: " + deck.Id);
        }
        var extras = new[] {
            (Id: "test-standard-extra-warrior", Hero: "HERO_01", Native: 900000023L, Opponent: "test-standard-batch-2-opp-druid"),
            (Id: "test-standard-extra-priest", Hero: "HERO_09", Native: 900000024L, Opponent: "test-standard-batch-2-opp-rogue"),
            (Id: "test-standard-extra-druid", Hero: "HERO_06", Native: 900000025L, Opponent: "test-standard-batch-2-warrior"),
            (Id: "test-standard-extra-hunter", Hero: "HERO_05", Native: 900000026L, Opponent: "test-standard-batch-2-paladin"),
            (Id: "test-standard-extra-demonhunter", Hero: "HERO_10", Native: 900000027L, Opponent: "test-standard-batch-2-opp-rogue"),
            (Id: "test-standard-extra-rogue", Hero: "HERO_03", Native: 900000028L, Opponent: "test-standard-batch-2-priest")
        };
        Check(LocalDecks.TestLoaners.Count == 105 && LocalDecks.TestLoaners.Where(deck => deck.Id.StartsWith("test-standard-extra-", StringComparison.Ordinal))
            .Select(deck => deck.Id).ToHashSet().SetEquals(extras.Select(spec => spec.Id)), "exactly six overflow loaners");
        foreach (var spec in extras)
        {
            var deck = LocalDecks.Get(spec.Id);
            Check(deck.Hero == spec.Hero && NativeUIProjection.DeckId(spec.Id) == spec.Native
                && LocalDecks.OpponentFor(spec.Id, Array.Empty<string>()).Id == spec.Opponent,
                "overflow stable identity and fixed opponent: " + spec.Id);
            string pathExtra = Path.Combine(root, spec.Id + ".xml");
            using (var profile = new LocalProfile(pathExtra)) profile.Begin(spec.Id, spec.Opponent, 17);
            using (var profile = new LocalProfile(pathExtra))
            {
                Check(profile.Pending?.DeckHero == spec.Hero && profile.Pending?.Opponent == spec.Opponent,
                    "overflow pending binding survives reload: " + spec.Id);
                profile.Settle(profile.Pending.Id, "Won", 12, false);
                Check(profile.ClassXp[spec.Hero] == LocalCoreProgression.WinXp, "overflow XP targets correct hero: " + spec.Id);
            }
        }
        // Seat loaners: stable ids, heroes and frozen fixed opponents; settlement credits the right class without ownership.
        var seats = new[] {
            (Id: "test-deathknight-buu", Hero: "HERO_11", Native: 900000044L, Opponent: "test-standard-batch-2-opp-rogue"),
            (Id: "test-standard-more-warrior", Hero: "HERO_01", Native: 900000050L, Opponent: "test-standard-batch-2-opp-druid"),
            (Id: "test-standard-more-priest", Hero: "HERO_09", Native: 900000051L, Opponent: "test-standard-batch-2-opp-rogue"),
            (Id: "test-standard-more-mage", Hero: "HERO_08", Native: 900000052L, Opponent: "test-standard-batch-2-opp-warlock"),
            (Id: "test-standard-more-paladin", Hero: "HERO_04", Native: 900000053L, Opponent: "test-standard-batch-2-opp-hunter"),
            (Id: "test-standard-more-druid", Hero: "HERO_06", Native: 900000054L, Opponent: "test-standard-batch-2-opp-warlock"),
            (Id: "test-standard-more-rogue", Hero: "HERO_03", Native: 900000055L, Opponent: "test-standard-batch-2-opp-hunter"),
            (Id: "test-standard-more-warlock", Hero: "HERO_07", Native: 900000056L, Opponent: "test-standard-batch-2-opp-druid"),
            (Id: "test-standard-more-hunter", Hero: "HERO_05", Native: 900000057L, Opponent: "test-standard-batch-2-opp-rogue"),
            (Id: "test-standard-more-shaman", Hero: "HERO_02", Native: 900000058L, Opponent: "test-standard-batch-2-opp-warlock"),
            (Id: "test-standard-more-demonhunter", Hero: "HERO_10", Native: 900000059L, Opponent: "test-standard-batch-2-opp-hunter")
        };
        Check(LocalDecks.TestLoaners.Where(deck => deck.Id.StartsWith("test-standard-more-", StringComparison.Ordinal) || deck.Id == "test-deathknight-buu")
            .Select(deck => deck.Id).ToHashSet().SetEquals(seats.Select(spec => spec.Id)), "exactly eleven seat loaners");
        Check(seats.Select(spec => Cards.FromId(spec.Hero).Class).Distinct().Count() == 11, "one seat loaner per player class");
        // Mechanic-families seats (group 6): same contract; recipes start as filler and coordinator mech-family bookkeeping replaces filler copies.
        var mechFamiliesSeats = new[] {
            (Id: "test-standard-mecha-warrior", Hero: "HERO_01", Native: 900000029L, Opponent: "test-standard-batch-2-opp-druid"),
            (Id: "test-standard-mecha-priest", Hero: "HERO_09", Native: 900000030L, Opponent: "test-standard-batch-2-opp-rogue"),
            (Id: "test-standard-mecha-mage", Hero: "HERO_08", Native: 900000031L, Opponent: "test-standard-batch-2-opp-warlock"),
            (Id: "test-standard-mecha-paladin", Hero: "HERO_04", Native: 900000032L, Opponent: "test-standard-batch-2-opp-hunter"),
            (Id: "test-standard-mecha-druid", Hero: "HERO_06", Native: 900000033L, Opponent: "test-standard-batch-2-opp-warlock"),
            (Id: "test-standard-mecha-rogue", Hero: "HERO_03", Native: 900000034L, Opponent: "test-standard-batch-2-opp-hunter"),
            (Id: "test-standard-mecha-warlock", Hero: "HERO_07", Native: 900000035L, Opponent: "test-standard-batch-2-opp-druid"),
            (Id: "test-standard-mecha-hunter", Hero: "HERO_05", Native: 900000036L, Opponent: "test-standard-batch-2-opp-rogue"),
            (Id: "test-standard-mecha-shaman", Hero: "HERO_02", Native: 900000037L, Opponent: "test-standard-batch-2-opp-warlock"),
            (Id: "test-standard-mecha-demonhunter", Hero: "HERO_10", Native: 900000038L, Opponent: "test-standard-batch-2-opp-hunter"),
            (Id: "test-standard-mecha-deathknight", Hero: "HERO_11", Native: 900000039L, Opponent: "test-standard-batch-2-opp-rogue"),
            (Id: "test-standard-mecha-deathknight-2", Hero: "HERO_11", Native: 900000047L, Opponent: "test-standard-batch-2-opp-rogue")
        };
        Check(LocalDecks.TestLoaners.Where(deck => deck.Id.StartsWith("test-standard-mecha-", StringComparison.Ordinal))
            .Select(deck => deck.Id).SequenceEqual(mechFamiliesSeats.Select(spec => spec.Id))
            && NativeUIProjection.TestLoanerGroups[5].SequenceEqual(mechFamiliesSeats.Select(spec => spec.Id)), "exactly twelve mechanic-families seat loaners form group 6");
        Check(mechFamiliesSeats.Select(spec => Cards.FromId(spec.Hero).Class).Distinct().Count() == 11
            && mechFamiliesSeats.Count(spec => spec.Hero == "HERO_11") == 2
            && mechFamiliesSeats.Length <= 15, "mechanic-families seats cover every player class");
        Check(mechFamiliesSeats.All(spec => LocalDecks.Get(spec.Id).Name == "TEST: Standard " + LocalCoreProgression.ClassName(spec.Hero) + " mech families"
            + (spec.Id == "test-standard-mecha-deathknight-2" ? " #2" : "")), "mechanic-families seat names");
        Check(mechFamiliesSeats.All(spec => LocalDecks.Get(spec.Id).Strategy == "Test loaner for mechanic-families live qualification. Not a starter deck."),
            "mechanic-families seat descriptions");
        // Batch-10 seats (group 5): same contract; recipes start as filler and coordinator batch-10 bookkeeping replaces filler copies.
        var batch10Seats = new[] {
            (Id: "test-standard-b10-warrior", Hero: "HERO_01", Native: 900000060L, Opponent: "test-standard-batch-2-opp-druid"),
            (Id: "test-standard-b10-priest", Hero: "HERO_09", Native: 900000061L, Opponent: "test-standard-batch-2-opp-rogue"),
            (Id: "test-standard-b10-mage", Hero: "HERO_08", Native: 900000062L, Opponent: "test-standard-batch-2-opp-warlock"),
            (Id: "test-standard-b10-paladin", Hero: "HERO_04", Native: 900000063L, Opponent: "test-standard-batch-2-opp-hunter"),
            (Id: "test-standard-b10-druid", Hero: "HERO_06", Native: 900000064L, Opponent: "test-standard-batch-2-opp-warlock"),
            (Id: "test-standard-b10-rogue", Hero: "HERO_03", Native: 900000065L, Opponent: "test-standard-batch-2-opp-hunter"),
            (Id: "test-standard-b10-warlock", Hero: "HERO_07", Native: 900000066L, Opponent: "test-standard-batch-2-opp-druid"),
            (Id: "test-standard-b10-hunter", Hero: "HERO_05", Native: 900000067L, Opponent: "test-standard-batch-2-opp-rogue"),
            (Id: "test-standard-b10-shaman", Hero: "HERO_02", Native: 900000068L, Opponent: "test-standard-batch-2-opp-warlock"),
            (Id: "test-standard-b10-demonhunter", Hero: "HERO_10", Native: 900000069L, Opponent: "test-standard-batch-2-opp-hunter"),
            (Id: "test-standard-b10-deathknight", Hero: "HERO_11", Native: 900000045L, Opponent: "test-standard-batch-2-opp-rogue"),
            (Id: "test-standard-b10-deathknight-2", Hero: "HERO_11", Native: 900000046L, Opponent: "test-standard-batch-2-opp-rogue")
        };
        Check(LocalDecks.TestLoaners.Where(deck => deck.Id.StartsWith("test-standard-b10-", StringComparison.Ordinal))
            .Select(deck => deck.Id).SequenceEqual(batch10Seats.Select(spec => spec.Id))
            && NativeUIProjection.TestLoanerGroups[4].SequenceEqual(batch10Seats.Select(spec => spec.Id)), "exactly twelve batch-10 seat loaners form group 5");
        Check(batch10Seats.Select(spec => Cards.FromId(spec.Hero).Class).Distinct().Count() == 11
            && batch10Seats.Count(spec => spec.Hero == "HERO_11") == 2, "batch-10 seats cover every player class, two for Death Knight");
        Check(batch10Seats.All(spec => LocalDecks.Get(spec.Id).Name == "TEST: Standard " + LocalCoreProgression.ClassName(spec.Hero) + " batch 10"
            + (spec.Id.EndsWith("-2", StringComparison.Ordinal) ? " #2" : "")), "batch-10 seat names");
        // Batch-11 seats (group 10): same contract; recipes start as filler and coordinator batch-11 bookkeeping replaces filler copies.
        var batch11Seats = new[] {
            (Id: "test-standard-b11-warrior", Hero: "HERO_01", Native: 900000170L, Opponent: "test-standard-batch-2-opp-druid"),
            (Id: "test-standard-b11-priest", Hero: "HERO_09", Native: 900000171L, Opponent: "test-standard-batch-2-opp-rogue"),
            (Id: "test-standard-b11-mage", Hero: "HERO_08", Native: 900000172L, Opponent: "test-standard-batch-2-opp-warlock"),
            (Id: "test-standard-b11-paladin", Hero: "HERO_04", Native: 900000173L, Opponent: "test-standard-batch-2-opp-hunter"),
            (Id: "test-standard-b11-druid", Hero: "HERO_06", Native: 900000174L, Opponent: "test-standard-batch-2-opp-warlock"),
            (Id: "test-standard-b11-rogue", Hero: "HERO_03", Native: 900000175L, Opponent: "test-standard-batch-2-opp-hunter"),
            (Id: "test-standard-b11-warlock", Hero: "HERO_07", Native: 900000176L, Opponent: "test-standard-batch-2-opp-druid"),
            (Id: "test-standard-b11-hunter", Hero: "HERO_05", Native: 900000177L, Opponent: "test-standard-batch-2-opp-rogue"),
            (Id: "test-standard-b11-shaman", Hero: "HERO_02", Native: 900000178L, Opponent: "test-standard-batch-2-opp-warlock"),
        };
        Check(LocalDecks.TestLoaners.Where(deck => deck.Id.StartsWith("test-standard-b11-", StringComparison.Ordinal))
            .Select(deck => deck.Id).SequenceEqual(batch11Seats.Select(spec => spec.Id))
            && NativeUIProjection.TestLoanerGroups[9].SequenceEqual(batch11Seats.Select(spec => spec.Id)), "exactly nine batch-11 seat loaners form group 10");
        Check(batch11Seats.Select(spec => Cards.FromId(spec.Hero).Class).Distinct().Count() == 9
            && batch11Seats.All(spec => spec.Hero != "HERO_10" && spec.Hero != "HERO_11"), "batch-11 seats cover the nine legacy classes");
        Check(batch11Seats.All(spec => LocalDecks.Get(spec.Id).Name == "TEST: Standard " + LocalCoreProgression.ClassName(spec.Hero) + " batch 11"), "batch-11 seat names");
        Check(batch11Seats.All(spec => LocalDecks.Get(spec.Id).Strategy == "Test loaner for Standard batch 11 live qualification. Not a starter deck."),
            "batch-11 seat descriptions");
        foreach (var spec in seats.Concat(batch10Seats).Concat(batch11Seats).Concat(mechFamiliesSeats))
        {
            var deck = LocalDecks.Get(spec.Id);
            Check(deck.Hero == spec.Hero && NativeUIProjection.DeckId(spec.Id) == spec.Native
                && LocalDecks.OpponentFor(spec.Id, new[] { "mage" }).Id == spec.Opponent
                && LocalDecks.TestOpponents.Any(opponentDeck => opponentDeck.Id == spec.Opponent)
                && deck.Strategy.Contains("Not a starter deck") && deck.Build().Count == LocalDecks.DeckSize(deck.CardIds),
                "seat stable identity, frozen fixed opponent and label: " + spec.Id);
            string pathSeat = Path.Combine(root, "seat-" + spec.Id + ".xml");
            KeyValuePair<string, int>[] ownedBefore;
            using (var profile = new LocalProfile(pathSeat))
            {
                ownedBefore = profile.Collection.OrderBy(pair => pair.Key).ToArray();
                profile.Begin(spec.Id, spec.Opponent, 17);
            }
            using (var profile = new LocalProfile(pathSeat))
            {
                Check(profile.Pending?.DeckHero == spec.Hero && profile.Pending?.Opponent == spec.Opponent
                    && profile.Collection.OrderBy(pair => pair.Key).SequenceEqual(ownedBefore),
                    "seat pending binding survives reload without ownership changes: " + spec.Id);
                profile.Settle(profile.Pending.Id, "Won", 12, false);
                Check(profile.ClassXp[spec.Hero] == LocalCoreProgression.WinXp
                    && profile.ClassXp.Where(pair => pair.Key != spec.Hero).All(pair => pair.Value == 0), "seat XP targets correct hero: " + spec.Id);
            }
        }
        var rewindSeats = new[] {
            (Id: "test-standard-rewind-rogue", Hero: "HERO_03", Native: 900000070L, Opponent: "test-standard-batch-2-opp-hunter"),
            (Id: "test-standard-rewind-mage", Hero: "HERO_08", Native: 900000071L, Opponent: "test-standard-batch-2-opp-warlock"),
            (Id: "test-standard-rewind-warlock", Hero: "HERO_07", Native: 900000072L, Opponent: "test-standard-batch-2-opp-druid"),
            (Id: "test-standard-rewind-shaman", Hero: "HERO_02", Native: 900000073L, Opponent: "test-standard-batch-2-opp-warlock"),
            (Id: "test-standard-rewind-paladin", Hero: "HERO_04", Native: 900000074L, Opponent: "test-standard-batch-2-opp-hunter"),
            (Id: "test-standard-rewind-druid", Hero: "HERO_06", Native: 900000075L, Opponent: "test-standard-batch-2-opp-warlock"),
            (Id: "test-standard-rewind-warrior", Hero: "HERO_01", Native: 900000076L, Opponent: "test-standard-batch-2-opp-druid"),
            (Id: "test-standard-rewind-priest", Hero: "HERO_09", Native: 900000077L, Opponent: "test-standard-batch-2-opp-rogue"),
            (Id: "test-standard-rewind-demonhunter", Hero: "HERO_10", Native: 900000078L, Opponent: "test-standard-batch-2-opp-hunter"),
            (Id: "test-standard-rewind-hunter", Hero: "HERO_05", Native: 900000079L, Opponent: "test-standard-batch-2-opp-rogue")
        };
        Check(LocalDecks.TestLoaners.Where(deck => deck.Id.StartsWith("test-standard-rewind-", StringComparison.Ordinal))
            .Select(deck => deck.Id).SequenceEqual(rewindSeats.Select(spec => spec.Id))
            && NativeUIProjection.TestLoanerGroups[6].SequenceEqual(rewindSeats.Select(spec => spec.Id)), "exactly ten Rewind loaners form group 7");
        foreach (var spec in rewindSeats)
        {
            var deck = LocalDecks.Get(spec.Id);
            Check(deck.Hero == spec.Hero && NativeUIProjection.DeckId(spec.Id) == spec.Native
                && LocalDecks.OpponentFor(spec.Id, Array.Empty<string>()).Id == spec.Opponent
                && LocalDecks.TestOpponents.Any(frozen => frozen.Id == spec.Opponent), "Rewind stable identity and frozen opponent: " + spec.Id);
            Check(deck.CardIds.Count == 30 && deck.CardIds.All(id => (registry.Contains(id) || fabledCompanionIds.Contains(id)) && Legal(id, Cards.FromId(spec.Hero).Class))
                && deck.CardIds.GroupBy(id => id).All(group => group.Count() <= (Cards.FromId(group.Key).Rarity == Rarity.LEGENDARY ? 1 : 2)),
                "Rewind thirty registered class-legal cards with legal copy counts: " + spec.Id);
            var altered = deck.CardIds.ToArray(); altered[0] = "CS2_182";
            Check(!NativeUIProjection.IsExactLoaner(spec.Native, spec.Hero, altered), "Rewind immutable recipe: " + spec.Id);
        }
        Check(rewindIds.All(id => rewindSeats.Any(spec => LocalDecks.Get(spec.Id).CardIds.Contains(id))), "Rewind seats every new collectible");
        // Curated DK showcase: a legal, playable 30-card deck of registered cards that features every newest mechanic.
        var showcase = LocalDecks.Get(LocalDecks.TestShowcaseId);
        var showcaseCards = showcase.Build();
        Check(showcase.Hero == "HERO_11" && NativeUIProjection.DeckId(showcase.Id) == 900000048L
            && LocalDecks.OpponentFor(showcase.Id, new[] { "mage" }).Id == "test-standard-batch-2-opp-hunter"
            && showcase.Name == "SHOWCASE: Death Knight" && showcase.Strategy.Contains("Not a starter deck"),
            "showcase stable identity, frozen Hunter opponent and label");
        Check(showcaseCards.Count == 30 && showcase.CardIds.All(registry.Contains)
            && showcaseCards.All(card => LocalDecks.IsAllowedInDeck(card, CardClass.DEATHKNIGHT) && Legal(card.Id, CardClass.DEATHKNIGHT))
            && string.Join(",", LocalDecks.RuneCounts(showcaseCards)) == "1,0,2"
            && showcaseCards.GroupBy(card => card.Id).All(group => group.Count() <= (group.First().Rarity == Rarity.LEGENDARY ? 1 : 2))
            && LocalCardCatalog.WithinCopyLimits(showcase.CardIds),
            "showcase is a registered, class-legal, rune-legal (B + UU), copy-legal 30-card Death Knight deck");
        Check(new[] { batch9Ids, darkGiftIds, historyFactsIds, costRulesIds, deathKnightIds }.All(slice => showcase.CardIds.Any(slice.Contains))
            && showcaseCards.Count(card => card.Rarity == Rarity.LEGENDARY) == 4
            && showcaseCards.Count(card => card.Cost <= 3) >= 14,
            "showcase features batch 9, Dark Gift, history facts, cost rules, DK runes, four legendaries and an early curve");
        string pathShowcase = Path.Combine(root, "showcase.xml");
        using (var profile = new LocalProfile(pathShowcase)) profile.Begin(showcase.Id, "test-standard-batch-2-opp-hunter", 17);
        using (var profile = new LocalProfile(pathShowcase))
        {
            Check(profile.Pending?.DeckHero == "HERO_11"
                && LocalDecks.TestStandardCardIds.Where(id => !LocalCoreProgression.IsAvailable(id)).All(id => !profile.Collection.ContainsKey(id)),
                "showcase pending binding survives reload with no ownership beyond explicit Core/deckbuilding grants");
            profile.Settle(profile.Pending.Id, "Won", 12, false);
            Check(profile.ClassXp["HERO_11"] == LocalCoreProgression.WinXp
                && profile.ClassXp.Where(pair => pair.Key != "HERO_11").All(pair => pair.Value == 0), "showcase XP targets Death Knight");
        }
        Console.WriteLine($"PASS Standard player coverage: {registeredPlayerCoverage.Count} + {exclusions.Count} exclusions = {registry.Count}, no pending seats");
        Console.WriteLine("PASS Standard registrations: " + string.Join(", ", batches.Select(batch => batch.Key + "=" + batch.Value.Length))
            + $"; union={allBatchIds.Length}, registered={registry.Count}");
        string path = Path.Combine(root, "standard-test-loaner.xml");
        using (var profile = new LocalProfile(path))
        {
            Check(LocalDecks.TestStandardCardIds.Where(id => !LocalCoreProgression.IsAvailable(id)).All(id => !profile.Collection.ContainsKey(id)), "test cards receive only explicit Core/deckbuilding grants");
            var match = profile.Begin(loaner.Id, opponent.Id, 17);
            Check(match.DeckHero == "HERO_01" && match.Opponent == opponent.Id, "Standard match records Warrior XP target");
        }
        using (var profile = new LocalProfile(path))
            Check(profile.Pending?.Opponent == opponent.Id && LocalDecks.TestStandardCardIds.Where(id => !LocalCoreProgression.IsAvailable(id)).All(id => !profile.Collection.ContainsKey(id)), "Standard pending match reload preserves no extra ownership");
        foreach (var spec in batch2)
        {
            string batch2Path = Path.Combine(root, spec.Id + ".xml");
            using (var profile = new LocalProfile(batch2Path))
            {
                var match = profile.Begin(spec.Id, spec.OpponentId, 17);
                Check(match.DeckHero == spec.Hero && match.Opponent == spec.OpponentId, "batch 2 profile accepts player hero and fixed opponent: " + spec.Id);
            }
            using (var profile = new LocalProfile(batch2Path))
            {
                Check(profile.Pending?.DeckHero == spec.Hero && profile.Pending?.Opponent == spec.OpponentId
                    && LocalDecks.TestStandardCardIds.Where(id => !LocalCoreProgression.IsAvailable(id)).All(id => !profile.Collection.ContainsKey(id)), "batch 2 pending reload preserves binding and no extra ownership: " + spec.Id);
                var settled = profile.Settle(profile.Pending.Id, "Won", 12, false);
                Check(settled.DeckHero == spec.Hero && profile.ClassXp[spec.Hero] == LocalCoreProgression.WinXp,
                    "batch 2 settlement credits player class: " + spec.Id);
            }
        }
        Console.WriteLine($"PASS {checks} Standard batch loaner checks");
#endif
    }
}
