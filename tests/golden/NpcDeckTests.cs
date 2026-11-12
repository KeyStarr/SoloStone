using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;

public static class NpcDeckTests
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    public static void TestContractBoundaries()
    {
        Check(typeof(GoldenDecision).GetProperty("DriverKind") != null, "recorder trace preserves driver path kind");
        foreach (var property in typeof(IGoldenMulliganView).GetProperties())
            Check(new[] { typeof(IReadOnlyList<GoldenMulliganCard>), typeof(bool), typeof(IReadOnlyList<string>), typeof(CardClass) }
                .Contains(property.PropertyType), "mulligan boundary contains values only");
        var ids = new[] { "CATA_556" };
        var hold = new HoldRule(HoldCondition.DragonActivator, 3, ids);
        ids[0] = "JAIL_384";
        Check(hold.CardIds.Single() == "CATA_556", "record snapshots cannot be changed through constructor arrays");
        Check(!NullMatchRecorder.Instance.Enabled, "null recorder skips fingerprint work");
    }

    public static void TestNpcDeckCatalog()
    {
        var field = typeof(LocalDecks).GetField("NpcDecks");
        Check(field != null, "NPC decks need their own list");
        var decks = ((IEnumerable<LocalDeck>)field.GetValue(null)).ToArray();
        Check(decks.Select(d => d.Id).SequenceEqual(new[] {
            "npc-dragon-warrior", "npc-pure-paladin", "npc-zee-shaman" }), "three golden NPC decks");
        foreach (var deck in decks) Check(ReferenceEquals(LocalDecks.Get(deck.Id), deck), "NPC lookup " + deck.Id);
    }

    public static void TestDeckListsAndLegality()
    {
        var fixtures = new[] {
            new[] { "npc-dragon-warrior", "HERO_01", "Dragonyyyy",
                "EDR_456:2 CATA_556:2 CATA_585:2 CAP_107:2 EDR_457:2 EDR_889:2 FIR_939:2 CATA_582:2 CAP_105:2 CATA_584:2 EDR_492:2 TIME_034:2 JAIL_421:1 END_033:2 TLC_600:2 JAIL_384:1" },
            new[] { "npc-pure-paladin", "HERO_04", "FatherUther",
                "CORE_ICC_038:2 END_011:2 CATA_472:2 MEND_802:2 JAIL_035:2 JAIL_327:2 TIME_009t1:1 TIME_019:2 CATA_474:2 CATA_479:2 EDR_258:1 TIME_009t2:1 TIME_700:2 CATA_473:1 JAIL_397:1 JAIL_516:2 CATA_475:2 TIME_009:1" },
            new[] { "npc-zee-shaman", "HERO_02", "ZeeCashh",
                "CORE_GIL_531:2 CORE_ULD_191:2 CORE_UNG_205:1 CORE_UNG_809:2 CORE_WC_042:2 CATA_556:2 DINO_411:2 CATA_565:2 JAIL_802:2 JAIL_850:1 EDR_856:1 JAIL_457:2 JAIL_458:1 TLC_482:2 CATA_722:2 CATA_497:1 JAIL_800:1 CATA_153:1 CATA_190h:1" }
        };
        foreach (var row in fixtures)
        {
            var deck = LocalDecks.Get(row[0]);
            var expected = row[3].Split(' ').SelectMany(pair => Enumerable.Repeat(pair.Split(':')[0], int.Parse(pair.Split(':')[1])));
            Check(deck.CardIds.OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(expected.OrderBy(id => id, StringComparer.Ordinal)), "exact list " + deck.Id);
            Check(deck.Hero == row[1] && deck.Opponent == row[2], "identity " + deck.Id);
            Check(deck.Style == (deck.Id == "npc-pure-paladin" ? DeckStyle.Value : DeckStyle.Tempo), "style " + deck.Id);
            var built = deck.Build();
            Check(built.Count == 30 && built.All(c => c.Implemented), "30 implemented slots " + deck.Id);
            Check(built.All(c => CardClassRules.IsAllowedInDeck(c, Cards.FromId(deck.Hero).Class)), "class-correct " + deck.Id);
            Check(built.All(c => StandardSets.CollectibleIds.Contains(c.Id) || FabledRules.IsCompanion(c.Id)), "Standard or Fabled " + deck.Id);
            Check(!FabledRules.DeckErrors(deck.CardIds).Any(), "complete Fabled bundle " + deck.Id);
            Check(built.GroupBy(c => c.Id).All(g => g.Count() <= (g.First().Rarity == Rarity.LEGENDARY ? 1 : 2)), "submitted copy limits");
            Check(built.All(c => !c.IsSecret && c.Id != "CS3_035" && c.Id != "BE_055"), "excluded cards " + deck.Id);
            DeckbuildingRules.Validate(deck.CardIds, deck.Sideboards, FormatType.FT_STANDARD);
            Check(deck.Source.StartsWith("https://hearthstone-decks.net/", StringComparison.Ordinal), "source provenance");
        }
        var paladin = LocalDecks.Get("npc-pure-paladin");
        Check(paladin.Sideboards.Count == 1 && paladin.Sideboards["JAIL_397"].SequenceEqual(new[] { "JAIL_328" }), "Beatrix Scarlet Bruiser sideboard");
        Check(LocalDecks.NpcDecks.Where(d => d != paladin).All(d => d.Sideboards.Count == 0), "other NPC sideboards empty");
        var sideCopy = paladin.Sideboards; sideCopy["JAIL_397"][0] = "EX1_015";
        Check(paladin.Sideboards["JAIL_397"][0] == "JAIL_328", "sideboard getter cannot mutate curated deck");
    }

    private static Game Opening(LocalDeck deck, int seed, bool shuffle = true)
    {
        var game = new Game(new GameConfig {
            StartPlayer = 1, RandomSeed = seed, Shuffle = shuffle, SkipMulligan = false,
            History = true, Logging = false, FillDecks = false, FormatType = FormatType.FT_STANDARD,
            Player1HeroClass = CardClass.MAGE, Player2HeroClass = Cards.FromId(deck.Hero).Class,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CORE_UNG_809"), 30).ToList(),
            Player2Deck = deck.Build(), Player2Sideboards = deck.Sideboards
        });
        game.StartGame();
        // Start of Game resolves after both mulligans (retail order); keep every offered card.
        foreach (var player in new[] { game.Player1, game.Player2 })
            if (player.Choice?.ChoiceType == ChoiceType.MULLIGAN)
                game.Process(ChooseTask.Mulligan(player, player.Choice.Choices.ToList()));
        game.MainBegin(); // the host's post-mulligan join
        return game;
    }

    public static void TestHoggerCopiesWarptooth()
    {
        var deck = LocalDecks.Get("npc-dragon-warrior");
        foreach (int seed in new[] { 7, 17, 31 })
        {
            var game = Opening(deck, seed);
            var cards = game.Player2.HandZone.Concat(game.Player2.DeckZone).Where(c => c.Card.Id == "JAIL_421").ToArray();
            Check(cards.Length == 2, "Hogger produces exactly two total Warptooths seed=" + seed);
            var hogger = game.Player2.HandZone.Concat(game.Player2.DeckZone).Single(c => c.Card.Id == "JAIL_384");
            Check(cards.Count(c => c[GameTag.DISPLAYED_CREATOR] == hogger.Id) == 1, "one Hogger-created Warptooth");
            Check(game.Player2.DeckCards.Count(c => c.Id == "JAIL_421") == 1, "submitted list remains one Warptooth");
            if (seed == 7)
                Check(!game.Player2.HandZone.Any(c => c.Card.Id == "JAIL_421") && game.Player2.DeckZone.Count(c => c.Card.Id == "JAIL_421") == 2,
                    "pinned opening keeps both Warptooths in deck");
        }
    }

    public static void TestPaladinAndZeeStartOfGame()
    {
        var paladin = Opening(LocalDecks.Get("npc-pure-paladin"), 7);
        Check(paladin.Player2.DeckCards.Count == 30, "Paladin submitted thirty preserved");
        Check(paladin.Player2.HandZone.Concat(paladin.Player2.DeckZone).Count(c => c.Card.Id == "JAIL_328") == 10, "Beatrix adds ten Bruisers");
        Check(paladin.Player2.HandZone.Concat(paladin.Player2.DeckZone).Count(c => FabledRules.IsCompanion(c.Card.Id)) == 2, "both Gelbin Auras retained");
        var zee = Opening(LocalDecks.Get("npc-zee-shaman"), 7);
        Check(zee.Player2.Hero.HeroPower.Card.Id == "JAIL_800hp2", "spell-free Zee gets fifth-minion power");
    }

    public static void TestNoPlayerFacingLeakage()
    {
        var legacy = LocalDecks.All.Concat(LocalDecks.TestLoaners).Concat(LocalDecks.TestOpponents)
            .Concat(LocalDecks.PlayerStarterDecks).ToArray();
        foreach (var deck in LocalDecks.NpcDecks)
            Check(legacy.All(old => old.Id != deck.Id), "NPC excluded from player/test lists " + deck.Id);
        var uiOnly = LocalDecks.TestLoaners.Concat(LocalDecks.TestOpponents).Concat(LocalDecks.PlayerStarterDecks).SelectMany(d => d.CardIds)
            .Where(StandardSets.CollectibleIds.Contains).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal);
        Check(LocalDecks.TestStandardCardIds.SequenceEqual(uiOnly), "UI definitions derive only from test and player decks");
        var extras = new HashSet<string>(OfflinePoolPolicy.Load().Metas.SelectMany(meta => meta.ExtraCards), StringComparer.Ordinal);
        foreach (var card in LocalDecks.CustomCards)
            Check(LocalCardCatalog.Supported.Contains(card)
                || extras.Contains(card.Id) && card.Collectible && card.Implemented && LocalCardCatalog.InActiveMeta(card)
                || StandardSets.CollectibleIds.Any(id =>
                (FabledRules.IsFabled(id) || DeckbuildingRules.HasSideboard(id)) && (id == card.Id || FabledRules.Bundle(id).Contains(card.Id))),
                "custom filter gained NPC-only card " + card.Id);
        Check(LocalCardCatalog.Current.Count == 252 && LocalCardCatalog.CurrentPoolVersion == 2, "economy catalog unchanged");
        foreach (var deck in legacy) Check(NpcCatalog.ForDeck(deck.Id) == null, "legacy pilot lookup " + deck.Id);
        Check(NpcCatalog.ForDeck(null) == null && NpcCatalog.ForDeck("missing") == null, "unknown decks use legacy");
        for (int i = 0; i < 30; i++)
            Check(!LocalDecks.NextOpponent(Enumerable.Repeat("npc-dragon-warrior", i)).Id.StartsWith("npc-", StringComparison.Ordinal), "normal rotation stays legacy");
    }

    public static void TestPlayerDefaultsDoNotRetuneInferencePopulation()
    {
        var population = CardFrequencies.PopulationDecks().ToDictionary(deck => deck.Id, StringComparer.Ordinal);
        var oldShowcases = new[] {
            new[] { "showcase-rogue-imbue", "EDR_528 CORE_BT_701 EDR_852 END_000 CORE_DMF_511 JAIL_225 EDR_523 TIME_001 END_001 JAIL_470 TIME_770 EDR_800 TIME_710", "FIR_919 END_036 EDR_856 TIME_712" },
            new[] { "showcase-warlock-dragon-shred", "CATA_556 TIME_026 END_020 EDR_485 CORE_WON_096 CORE_NEW1_023 EDR_889 TIME_027 TIME_025 CATA_111 FIR_955 TIME_028 TIME_031 TIME_029 END_033", "" },
            new[] { "showcase-warrior-armor-herald", "CORE_AT_064 CORE_EX1_606 BE_559 BE_572 BE_600 DINO_400 CATA_580 TLC_620 CATA_160 TLC_601 CATA_722 BE_560 CORE_SW_068", "FIR_939 EDR_979 TIME_714 CATA_150" }
        };
        foreach (var row in oldShowcases)
        {
            var expected = row[1].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).SelectMany(id => new[] { id, id })
                .Concat(row[2].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            Check(population[row[0]].Cards.SequenceEqual(expected), "historical showcase remains inference input: " + row[0]);
        }
        // The class tables stay player-free; DeckInference adds the Core starters as inference candidates only (#63).
        Check(population.Keys.All(id => !id.StartsWith("player-", StringComparison.Ordinal)),
            "player-only defaults excluded from inference population");
    }

    public static void TestProfilesAndSkins()
    {
        // Hero skin facts. In the private tree the client export is checked for every skin (strict). Without it (the public
        // tree) facts/violet-hold-2026-09.json answers, which lists only the cards the engine plays: a skin it lacks is not
        // checked there. Each answer is (set is HERO_SKINS, collectible, class), or null when the card is unknown.
        Func<string, Tuple<bool, bool, string>> skinFacts;
        string export = Path.Combine(AppContext.BaseDirectory, "cards.json.gz");
        var disposables = new List<IDisposable>();
        if (File.Exists(export))
        {
            var file = File.OpenRead(export); var gzip = new GZipStream(file, CompressionMode.Decompress); var json = JsonDocument.Parse(gzip);
            disposables.AddRange(new IDisposable[] { file, gzip, json });
            var cards = json.RootElement.GetProperty("cards").EnumerateArray().ToDictionary(c => c.GetProperty("cardId").GetString());
            skinFacts = id => { var c = cards[id]; return Tuple.Create(c.GetProperty("cardSet").GetProperty("name").GetString() == "HERO_SKINS",
                c.GetProperty("collectible").GetBoolean(), c.GetProperty("cardClass").GetString()); };
        }
        else
        {
            var tagsById = new Dictionary<string, Dictionary<int, int>>();
            using (var facts = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "facts.json"))))
                foreach (var group in new[] { facts.RootElement.GetProperty("cards"), facts.RootElement.GetProperty("overlay").GetProperty("cards") })
                    foreach (var row in group.EnumerateArray())
                    {
                        var tags = new Dictionary<int, int>();
                        foreach (var pair in row.GetProperty("tags").EnumerateArray()) tags[pair[0].GetInt32()] = pair[1].GetInt32();
                        tagsById[row.GetProperty("id").GetString()] = tags;
                    }
            skinFacts = id => { Dictionary<int, int> t; return tagsById.TryGetValue(id, out t)
                ? Tuple.Create(t[(int)GameTag.CARD_SET] == (int)CardSet.HERO_SKINS, t[(int)GameTag.COLLECTIBLE] == 1, t[(int)GameTag.CLASS].ToString()) : null; };
        }
        int skinsChecked = 0;
        try
        {
            Check(NpcCatalog.All.Count == 3, "three profiles");
            for (int i = 0; i < NpcCatalog.All.Count; i++)
            {
                var profile = NpcCatalog.All[i]; var deck = LocalDecks.Get(profile.DeckId);
                Check(profile.Id == "npc.golden." + (i + 1) && profile.Version == 1 && profile.DisplayName == deck.Opponent, "versioned identity");
                Check(ReferenceEquals(NpcCatalog.ForDeck(deck.Id), profile) && DeckPlans.ForId(profile.PlanId).DeckId == deck.Id, "catalog/plan join");
                var skin = skinFacts(profile.HeroSkin); var hero = skinFacts(deck.Hero);
                Check(profile.HeroSkin != deck.Hero, "alternate skin " + profile.Id);
                if (skin != null && hero != null)
                {
                    skinsChecked++;
                    Check(skin.Item1 && skin.Item2 && skin.Item3 == hero.Item3, "collectible alternate class skin " + profile.Id);
                }
                Check(profile.CardBack == 0, "card back awaits native qualification");
                var personality = profile.Personality; var budget = profile.Budget;
                Check(personality.PreTurnMinSeconds <= personality.PreTurnMaxSeconds && personality.ActionMinSeconds <= personality.ActionMaxSeconds
                    && personality.PreTurnMinSeconds >= 0 && personality.ActionMinSeconds >= 0, "valid pacing");
                Check(budget.TurnNodes > 0 && budget.PlanNodes <= budget.TurnNodes && budget.LethalNodes > 0
                    && budget.ReplyK > 0 && budget.ReplyL > 0 && budget.ReplyR > 0, "usable placeholder budget");
                Check(ReferenceEquals(budget, NpcCatalog.All[0].Budget), "all bots equal search difficulty");
            }
            Check(skinsChecked >= 2, "hero skins checked against the card data");
        }
        finally { foreach (var d in disposables) d.Dispose(); }
        Check(NpcCatalog.All.Select(p => p.Personality.ReplyBlendBeta).SequenceEqual(new[] { .35, .55, .15 }), "risk styles");
        Check(NpcCatalog.All.Select(p => p.Personality.ConcedeFromTurn).SequenceEqual(new[] { 8, 10, 12 }), "late concession minimums");
        Check(NpcCatalog.All.Take(2).All(p => p.Personality.Emotes.Rules.All(r => !r.Mock && r.Emote != "Threaten"))
            && NpcCatalog.All[2].Personality.Emotes.Rules.Any(r => r.Mock), "polite veteran/pro, mocking gambler");
    }

    public static void TestPlansReferenceOnlyDeckAndDocumentedPools()
    {
        var manifest = OfflinePoolPolicy.Load().BuildManifest();
        foreach (var plan in DeckPlans.All)
        {
            var deck = LocalDecks.Get(plan.DeckId); var own = new HashSet<string>(deck.CardIds, StringComparer.Ordinal);
            Check(plan.CardRoles.Select(r => r.CardId).OrderBy(id => id, StringComparer.Ordinal)
                .SequenceEqual(own.OrderBy(id => id, StringComparer.Ordinal)), "every deck card has exactly one role " + plan.Id);
            var direct = plan.MulliganRules.Select(r => r.CardId).Concat(plan.Holds.SelectMany(h => h.CardIds.Concat(h.EnabledBy)))
                .Concat(plan.RemovalThresholds.Select(r => r.CardId)).Concat(plan.Hints.SelectMany(h => h.CardIds))
                .Concat(plan.DiscoverPreferences.Select(p => p.SourceCardId)).Concat(plan.RewindPreferences.Select(p => p.SourceCardId));
            Check(direct.All(own.Contains), "plan source/hold/hint ids belong to its deck " + plan.Id);
            foreach (var preference in plan.DiscoverPreferences)
            {
                IEnumerable<string> pool;
                switch (preference.Pool)
                {
                    case DiscoverPool.ClassDragons:
                        pool = OfflineCardPools.Candidates(manifest, Cards.FromId(deck.Hero).Class,
                            OfflinePoolFilter.Discover(type: CardType.MINION, race: Race.DRAGON), Cards.FromId(preference.SourceCardId).AssetId).Select(c => c.Id); break;
                    case DiscoverPool.WarriorMinions:
                        pool = OfflineCardPools.Candidates(manifest, CardClass.WARRIOR,
                            new OfflinePoolFilter(classMode: PoolClassMode.Exact, exactClass: CardClass.WARRIOR, type: CardType.MINION),
                            Cards.FromId(preference.SourceCardId).AssetId).Select(c => c.Id); break;
                    case DiscoverPool.ClassPirates:
                        pool = OfflineCardPools.Candidates(manifest, Cards.FromId(deck.Hero).Class,
                            OfflinePoolFilter.Discover(type: CardType.MINION, race: Race.PIRATE), Cards.FromId(preference.SourceCardId).AssetId).Select(c => c.Id); break;
                    case DiscoverPool.OwnDeckMinions: pool = own.Where(id => Cards.FromId(id).Type == CardType.MINION); break;
                    default: pool = new string[0]; break;
                }
                Check(preference.PreferredCardIds.All(pool.Contains), "preferred ids in actual declared offer pool " + preference.SourceCardId);
            }
            foreach (var rewind in plan.RewindPreferences)
            {
                var weapons = OfflineCardPools.Candidates(manifest, Cards.FromId(deck.Hero).Class,
                    OfflinePoolFilter.Random(type: CardType.WEAPON), Cards.FromId(rewind.SourceCardId).AssetId).Select(c => c.Id);
                Check(rewind.Weapons.Select(w => w.CardId).OrderBy(id => id, StringComparer.Ordinal)
                    .SequenceEqual(weapons.OrderBy(id => id, StringComparer.Ordinal)), "full generated weapon table");
            }
            var classes = plan.MatchupClasses.SelectMany(m => m.Classes).ToArray();
            Check(classes.Length == 11 && classes.Distinct().Count() == 11, "class priors complete and unambiguous");
            Check(plan.MulliganRules.All(r => r.MaxCopies > 0 && r.MaxCopies <= 2), "mulligan copy limits");
        }
        Check(DeckPlans.ForId("legacy") == null, "no implicit plan for legacy");
        Check(!DeckPlans.DragonWarrior.MulliganRules.Any(r => new[] { "JAIL_384", "TLC_600", "END_033" }.Contains(r.CardId)), "expensive Dragons cannot enable conditional mulligan keeps");
        Check(DeckPlans.ZeeShaman.Holds.Where(h => h.Condition == HoldCondition.FifthMinionBattlecry)
            .SelectMany(h => h.CardIds).All(id => Cards.FromId(id).Type == CardType.MINION), "only minions can consume Zee doubling");
    }
}
