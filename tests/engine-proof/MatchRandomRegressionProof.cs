using System.Xml.Linq;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

// B6 review regressions (#141): small real manifest subsets make self-offers and empty pools deterministic.
internal static class MatchRandomRegressionProof
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }

    private static OfflinePoolManifest Subset(Func<OfflinePoolPolicy.Member, bool> filter, int count = 2)
    {
        var installed = OfflinePoolManifest.Installed;
        var ids = installed.Standard.Where(filter).Take(count).Select(m => m.CardId).ToHashSet();
        Check(ids.Count == count, "fixture has enough members");
        var xml = OfflinePoolFixtures.Catalog();
        xml.Root.Element("Members").Elements("Card")
            .Where(row => !ids.Contains((string)row.Attribute("cardId"))).Remove();
        return OfflinePoolFixtures.Manifest(xml);
    }

    private static Game NewGame(OfflinePoolManifest pools, int legacy = 0, FormatType format = FormatType.FT_WILD)
    {
        var game = new Game(new GameConfig {
            RandomSeed = 7, StartPlayer = 1, Shuffle = false, SkipMulligan = true, Logging = false, History = true,
            FormatType = format, PoolFormat = GenerationFormat.Standard, ReplayPools = pools,
            LegacyPoolReplay = legacy == 1, UnversionedRandomPoolReplay = legacy == 2,
            Player1HeroClass = CardClass.MAGE, Player2HeroClass = CardClass.WARRIOR,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_168"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_168"), 30).ToList() });
        game.StartGame();
        return game;
    }

    private static Card[] Offer(Game game, ISimpleTask task, IEntity source)
    {
        var c = game.Player1;
        c.Choice = null;
        task.Process(game, c, source, null, new TaskStack());
        return c.Choice?.Choices.Select(id => game.IdEntityDic[id].Card).ToArray() ?? Array.Empty<Card>();
    }

    internal static void Run()
    {
        var failures = new List<string>();
        foreach (Action proof in new Action[] { DiscoverCriteriaSourceExclusion, DiscoverTypedSourceExclusion, DiscoverTriClassSourceExclusion,
            RandomCastSourceExclusion, ChangeEntityEmptyPool, PrinceLiamEmptyPool, RandomMinionExhaustedPool })
        {
            try { proof(); Console.WriteLine("PASS " + proof.Method.Name); }
            catch (Exception error) { failures.Add(proof.Method.Name); Console.WriteLine($"FAIL {proof.Method.Name}: {error.GetType().Name}: {error.Message}"); Console.WriteLine(error.StackTrace); }
        }
        Check(failures.Count == 0, "match random regressions failed: " + string.Join(", ", failures));
    }

    private static void DiscoverCriteriaSourceExclusion()
    {
        var pools = Subset(m => m.Type == "MINION" && m.Classes.Contains("NEUTRAL") && m.Rarity == "COMMON");
        foreach (var format in new[] { FormatType.FT_WILD, FormatType.FT_STANDARD })
        {
            var game = NewGame(pools, format: format);
            var c = game.Player1;
            var cards = Cards.RandomPool(game, PoolDestination.Hand).All;
            Check(cards.Count == 2, "two-card Discover fixture");
            var task = new DiscoverTask(CardType.MINION, repeat: 2);
            foreach (var card in cards.Concat(cards))
            {
                var source = Entity.FromCard(c, card);
                var offer = Offer(game, task, source);
                Check(offer.Length == 1 && offer[0].AssetId != card.AssetId,
                    $"criteria Discover offered its own source {card.Id}");
                var next = c.Choice.NextChoice;
                next.TryPrepare();
                Check(next.Choices.Select(id => game.IdEntityDic[id].Card).All(x => x.AssetId != card.AssetId),
                    "repeated Discover excludes source too");
            }
            Check(Offer(game, task, c.Hero).Length == 2, "source filtering must not mutate the shared memo");
        }
        // Both replay gates retain the old source-eligible criteria set and RNG state.
        foreach (int legacy in new[] { 1, 2 })
        foreach (var format in new[] { FormatType.FT_WILD, FormatType.FT_STANDARD })
        {
            var game = NewGame(pools, legacy, format);
            var task = new DiscoverTask(CardType.MINION);
            var control = Offer(game, task, game.Player1.Hero);
            var source = Entity.FromCard(game.Player1, control[0]);
            var expected = game.CloneExact();
            var actual = Offer(game, task, source);
            var unchanged = Offer(expected, task, expected.Player1.Hero);
            Check(actual.Select(c => c.AssetId).SequenceEqual(unchanged.Select(c => c.AssetId)), "legacy criteria offers unchanged");
            Check(game.Random.Next() == expected.Random.Next(), "legacy criteria RNG unchanged");
        }
    }

    private static void DiscoverTypedSourceExclusion()
    {
        var pools = Subset(m => m.Type == "MINION" && m.Classes.Contains("NEUTRAL") && m.Rarity == "COMMON");
        foreach (var format in new[] { FormatType.FT_WILD, FormatType.FT_STANDARD })
        {
            var game = NewGame(pools, format: format);
            var cards = Cards.RandomPool(game, PoolDestination.Hand).All;
            foreach (var type in new[] { DiscoverType.MINION, DiscoverType.ALL })
            foreach (var card in cards)
            {
                var offer = Offer(game, new DiscoverTask(type), Entity.FromCard(game.Player1, card));
                Check(offer.Length == 1 && offer[0].AssetId != card.AssetId,
                    $"typed {type} Discover offered its own source {card.Id}");
            }
            // Copying an existing deck card remains legal, including the source's identity.
            var copySource = Entity.FromCard(game.Player1, Cards.FromId("CS2_168"));
            foreach (var type in new[] { DiscoverType.DECK_MINION, DiscoverType.OP_DECK_MINION, DiscoverType.OP_DECK })
                Check(Offer(game, new DiscoverTask(type), copySource).Single().AssetId == copySource.Card.AssetId,
                    type + " still copies its source from a deck");
        }
        foreach (int legacy in new[] { 1, 2 })
        foreach (var format in new[] { FormatType.FT_WILD, FormatType.FT_STANDARD })
        {
            var game = NewGame(pools, legacy, format);
            // All choices requested: source eligibility is deterministic, independent of seed.
            var task = new DiscoverTask(DiscoverType.MINION, numberOfChoices: 10000);
            var source = Entity.FromCard(game.Player1, Cards.FromId("EX1_014"));
            Check(Offer(game, task, source).Any(c => c.AssetId == source.Card.AssetId), "legacy typed Discover still offers source");
        }
    }

    private static void RandomCastSourceExclusion()
    {
        var pools = Subset(m => m.Type == "SPELL" && m.Classes.Contains("MAGE") && m.Tags.TryGetValue((int)GameTag.SECRET, out int secret) && secret > 0, 1);
        foreach (int legacy in new[] { 0, 1, 2 })
        foreach (var format in new[] { FormatType.FT_WILD, FormatType.FT_STANDARD })
        foreach (bool secret in new[] { false, true })
        {
            var game = NewGame(pools, legacy, format);
            var c = game.Player1;
            var card = legacy == 0 ? Cards.RandomPool(game).All.Single()
                : Cards.FormatTypeClassCards(format)[CardClass.MAGE].First(x => x.IsSecret);
            var source = Entity.FromCard(c, card);
            var task = secret ? SpecificTask.CastRandomSecret(CardClass.MAGE)
                : (ISimpleTask)new CastRandomSpellTask(x => x.AssetId == card.AssetId);
            int before = game.IdEntityDic.Count;
            var rng = game.CloneExact();
            bool selfCast = false;
            for (int i = 0; i < (legacy != 0 && secret ? 200 : 1); i++)
            {
                int last = game.IdEntityDic.Values.Max(e => e.Id);
                task.Process(game, c, source, null, new TaskStack());
                selfCast |= game.IdEntityDic.Values.Any(e => e.Id > last && e.Card.AssetId == card.AssetId);
                foreach (var active in c.SecretZone.ToArray()) c.SecretZone.Remove(active);
            }
            Check(legacy != 0 ? game.IdEntityDic.Count > before : game.IdEntityDic.Count == before,
                $"{(secret ? "secret" : "spell")} cast {(legacy == 0 ? "generated its own source" : "lost legacy source roll")}");
            Check(legacy == 0 || selfCast, "legacy random cast still rolls its own source");
            if (legacy == 0)
            {
                Check(game.Random.Next() == rng.Random.Next(), "empty cast pool must not consume RNG");
                task.Process(game, c, c.Hero, null, new TaskStack());
                Check(game.IdEntityDic.Count > before, "another source can still cast the card");
            }
        }
    }

    private static void DiscoverTriClassSourceExclusion()
    {
        var ids = new[] { "MAGE", "PRIEST", "WARLOCK" }.Select(cls => OfflinePoolManifest.Installed.Standard
            .First(m => m.Type == "MINION" && m.Classes.Count == 1 && m.Classes.Contains(cls)).CardId).ToHashSet();
        var pools = Subset(m => ids.Contains(m.CardId), 3);
        var game = NewGame(pools);
        var card = Cards.RandomPool(game).ClassOnly(CardClass.MAGE).Single();
        var task = new DiscoverTask(DiscoverType.MAGE_PRIEST_WARLOCK);
        Check(Offer(game, task, game.Player1.Hero).Length == 3, "control: one card per tri-class set");
        var offer = Offer(game, task, Entity.FromCard(game.Player1, card));
        Check(offer.Length == 2 && offer.All(x => x.AssetId != card.AssetId),
            "tri-class Discover offered its source instead of the two remaining cards");
    }

    private static void ChangeEntityEmptyPool()
    {
        var pools = Subset(m => m.Type == "MINION" && m.Rarity == "COMMON");
        foreach (var format in new[] { FormatType.FT_WILD, FormatType.FT_STANDARD })
        {
            var game = NewGame(pools, format: format);
            var c = game.Player1;
            var before = game.SaveSnapshot();
            var task = new ChangeEntityTask(EntityType.HAND, CardType.MINION, rarity: Rarity.LEGENDARY);
            Check(task.Process(game, c, c.Hero, null, new TaskStack()) == TaskState.STOP, "empty ChangeEntity must stop");
            Check(before.SequenceEqual(game.SaveSnapshot()), "empty ChangeEntity changes no state or RNG");
        }
    }

    private static void PrinceLiamEmptyPool()
    {
        var pools = Subset(m => m.Type == "MINION" && m.Rarity == "COMMON");
        foreach (var format in new[] { FormatType.FT_WILD, FormatType.FT_STANDARD })
        {
            var game = NewGame(pools, format: format);
            var c = game.Player1;
            var liam = Entity.FromCard(c, Cards.FromId("GIL_694"));
            Check(c.DeckZone.Any(x => x.Cost == 1), "Liam fixture has 1-Cost deck cards");
            Check(RandomCardTask.GetCardList(liam, CardType.MINION, rarity: Rarity.LEGENDARY).Count == 0, "Liam fixture has no legendaries");
            var before = game.SaveSnapshot();
            liam.Card.Power.PowerTask.Process(game, c, liam, null, new TaskStack());
            Check(before.SequenceEqual(game.SaveSnapshot()), "empty Liam changes no state or RNG");
        }
    }

    private static void RandomMinionExhaustedPool()
    {
        var pools = Subset(m => m.Type == "MINION" && m.Rarity == "COMMON", 1);
        var game = NewGame(pools);
        var stack = new TaskStack();
        new RandomMinionTask(GameTag.RARITY, (int)Rarity.COMMON, amount: 2)
            .Process(game, game.Player1, game.Player1.Hero, null, stack);
        Check(stack.Playables.Count == 1, "exhausted minion pool returns the available card once");
        var card = Cards.RandomPool(game, PoolDestination.Summon).All.Single();
        game.Player1.DeckCards.Clear();
        game.Player1.DeckCards.AddRange(Enumerable.Repeat(card, card.MaxAllowedInDeck));
        new RandomMinionTask(GameTag.RARITY, (int)Rarity.COMMON, amount: 2, maxInDeckFlag: true)
            .Process(game, game.Player1, game.Player1.Hero, null, stack);
        Check(stack.Playables.Count == 0, "MaxInDeck skips exhaust the shrinking pool too");
    }
}
