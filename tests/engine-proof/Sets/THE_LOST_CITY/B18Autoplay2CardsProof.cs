using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using L = LocationProofs;

internal static class B18Autoplay2CardsProof
{
    internal static readonly string[] Costs = { "CS2_231", "CS2_189", "CS2_172", "CS2_121", "CS2_182", "CS2_187", "CS2_200", "CS2_201", "EX1_298", "EX1_561", "EX1_586" };
    [GroupProof("b18-t-autoplay2-cards", ProofKind.Cards)]
    internal static void Run()
    {
        ComposedSnapshots();
        StandardSetsProof.Case("TLC_460", Unpinned_ForbiddenDiscoverProgress);
        StandardSetsProof.Case("TLC_100", Unpinned_EliseCraftingTwoTrioChoices);
    }
    private static void ComposedSnapshots()
    {
        var game = L.Game(); var p = game.Player1;
        var definitions = new List<Card>();
        foreach (int tier in new[] { 1, 2, 3 })
        foreach (bool reverse in new[] { false, true })
        {
            string body = "TLC_100t" + tier;
            definitions.Add(CompositeLocationRules.Compose(Cards.FromId(body),
                Cards.FromId(body + (reverse ? "6" : "3")), Cards.FromId(body + (reverse ? "3" : "6"))));
        }
        definitions.Add(Card.CreateAssembledCard(Cards.FromId("CORE_LOOT_413"), Cards.FromId("CORE_DMF_067"), Cards.FromId("CORE_LOOT_413")));
        var first = Cards.FromId("CS2_171"); var second = Cards.FromId("CS2_172");
        definitions.Add(Card.CreateZombeastCard(in first, in second, true));
        foreach (var definition in definitions)
        {
            var entity = Entity.FromCard(p, definition, zone: p.SetasideZone);
            var parts = definition[AssembleRules.FirstHalf] != 0
                ? new[] { AssembleRules.FirstHalf, AssembleRules.SecondHalf }
                : new[] { GameTag.MODULAR_ENTITY_PART_1, GameTag.MODULAR_ENTITY_PART_2 };
            var created = game.PowerHistory.Last.OfType<PowerHistoryFullEntity>().First(e => e.Entity.Id == entity.Id).Entity;
            var before = game.SaveSnapshot();
            var snapshots = new[] { created, PowerHistoryBuilder.FullEntity(entity).Entity,
                PowerHistoryBuilder.ShowEntity(entity).Entity, PowerHistoryBuilder.FullEntity(entity, reset: true).Entity };
            foreach (var snapshot in snapshots)
                Core2026Proof.Check(parts.All(tag => snapshot.Tags.TryGetValue(tag, out int value) && value == definition[tag]),
                    "FULL/SHOW/reset freeze composed parts: " + definition.Id);
            Core2026Proof.Check(before.SequenceEqual(game.SaveSnapshot()), "snapshot capture changes no game state or RNG");
            entity[parts[0]] = 0; entity[parts[1]] = first.AssetId;
            foreach (var snapshot in new[] { PowerHistoryBuilder.FullEntity(entity).Entity,
                PowerHistoryBuilder.ShowEntity(entity).Entity, PowerHistoryBuilder.FullEntity(entity, reset: true).Entity })
                Core2026Proof.Check(snapshot.Tags[parts[0]] == 0 && snapshot.Tags[parts[1]] == first.AssetId,
                    "instance parts, including zero, override definition");
            Core2026Proof.Check(parts.All(tag => created.Tags[tag] == definition[tag]), "old snapshot keeps original recipe");
        }
        Console.WriteLine("PASS composed FULL/SHOW/reset parts: Elise all tiers/orders, Assemble, legacy Zombeast, instance overrides");
    }
    private static void Unpinned_ForbiddenDiscoverProgress(Game game, Controller p, string id)
    {
        foreach (int emptySlots in new[] { 0, 1 })
        {
            var kept = L.Game(); var keeper = kept.Player1;
            var keptQuest = (Spell)Play(kept, keeper, id);
            while (keeper.HandZone.Count < 10 - emptySlots) Add(keeper, "CS2_182");
            new SabberStoneCore.Tasks.SimpleTasks.DiscoverTask(CardType.MINION, CardClass.DRUID, keepAllCondition: cards => true)
                .Process(kept, keeper, keptQuest, null);
            Core2026Proof.Check(keptQuest.QuestProgress == 3 && keeper.HandZone.Count == 10,
                "keep-all Discover credit independent of full or partially full hand");
            B16AutoplayProof.AssertSaved(kept);
        }
        var quest = (Spell)Play(game, p, id);
        for (int i = 0; i < 7; i++)
        {
            var offers = B18Autoplay2Proof.Offers(p, p.Hero.HeroPower, true, "CS2_182", "CS2_189");
            if (i < 3) p.Choice.ChoiceAction = ChoiceAction.SUMMON;
            B16AutoplayProof.Answer(game, offers[0].Id);
        }
        Core2026Proof.Check(quest.QuestProgress == 7, "genuine Hero Power Discovers count");
        var lookalike = B18Autoplay2Proof.Offers(p, p.Hero.HeroPower, false, "CS2_182", "CS2_189");
        B16AutoplayProof.Answer(game, lookalike[0].Id);
        Core2026Proof.Check(quest.QuestProgress == 7, "non-Discover lookalike excluded");
        var enemyOffers = B18Autoplay2Proof.Offers(p.Opponent, p.Opponent.Hero.HeroPower, true, "CS2_182", "CS2_189");
        Generic.ChoicePick(p.Opponent, game, enemyOffers[0].Id);
        Core2026Proof.Check(quest.QuestProgress == 7, "opponent Discover never advances owner quest");
        while (!p.HandZone.IsFull) Add(p, "CS2_182");
        var final = B18Autoplay2Proof.Offers(p, p.Hero.HeroPower, true, "CS2_182", "CS2_189");
        foreach (var future in B16AutoplayProof.Futures(game))
        {
            B16AutoplayProof.Answer(future, final[0].Id);
            var owner = future.Player1;
            Core2026Proof.Check(!owner.HandZone.Any(x => x.Card.Id == "TLC_460t"), "reward waits for hand space despite progress");
            Generic.DiscardBlock(owner, owner.HandZone[0]);
            var stone = owner.HandZone.Single(x => x.Card.Id == "TLC_460t");
            owner.UsedMana = 0; PlayEntity(future, owner, stone);
            Core2026Proof.Check(owner.Hero.Weapon == stone && owner.Hero.Weapon.Durability == 8, "working Origin Stone reward");
            B16AutoplayProof.AssertSaved(future);
        }
    }
    private static void Unpinned_EliseCraftingTwoTrioChoices(Game game, Controller p, string id)
    {
        Play(game, p, id);
        Core2026Proof.Check(p.Choice == null, "ineligible starting Costs open no crafting choice");
        p.DeckCards.Clear(); p.DeckCards.AddRange(Costs.Select(Cards.FromId));
        Core2026Proof.Check(p.DeckCards.Select(c => c.Cost).Distinct().Count() >= 10, "fixture has ten distinct printed Costs");
        p.UsedMana = 0;
        var elise = (Minion)Play(game, p, id);
        Core2026Proof.Check(p.Choice.Choices.Select(e => game.IdEntityDic[e].Card.Id).SequenceEqual(new[] { "TLC_100t1", "TLC_100t2", "TLC_100t3" }), "fixed base order");
        int discoveries = game.HistoryFacts.Query.Discoveries().Count();
        foreach (var branch in B16AutoplayProof.Futures(game))
        {
            var c = branch.Player1; int tier = branch.Random.Next(1, 4);
            var rng = branch.Random.Clone();
            var bucket = Enumerable.Range(1, tier == 1 ? 6 : 7).Select(n => "TLC_100t" + tier + n).ToList();
            var expected = new List<string>();
            for (int n = 0; n < 3; n++) { int index = rng.Next(bucket.Count); expected.Add(bucket[index]); bucket.RemoveAt(index); }
            B16AutoplayProof.Answer(branch, c.Choice.Choices[tier - 1]);
            var firstOffers = c.Choice.Choices.Select(e => branch.IdEntityDic[e].Card.Id).ToArray();
            Core2026Proof.Check(firstOffers.SequenceEqual(expected) && branch.Random.CaptureState().Equals(rng.CaptureState()), "seeded ordered ability sample, six/seven domain");
            string first = firstOffers[0];
            ((Minion)branch.IdEntityDic[elise.Id]).Silence();
            B16AutoplayProof.Answer(branch);
            var secondOffers = c.Choice.Choices.Select(e => branch.IdEntityDic[e].Card.Id).ToArray();
            Core2026Proof.Check(secondOffers.Length == 3 && secondOffers.Distinct().Count() == 3 && !secondOffers.Contains(first), "second trio excludes only selected ability");
            var overflow = branch.CloneExact(true); var fullOwner = overflow.Player1;
            ((Minion)overflow.IdEntityDic[elise.Id]).Destroy(); overflow.DeathProcessingAndAuraUpdate();
            while (!fullOwner.BoardZone.IsFull) L.Minion(fullOwner);
            while (!fullOwner.HandZone.IsFull) Add(fullOwner, "CS2_182");
            B16AutoplayProof.Answer(overflow);
            Core2026Proof.Check(fullOwner.HandZone.Count == 10 && !fullOwner.HandZone.OfType<BoardObject>().Any()
                && fullOwner.BoardZone.Count == 7 && fullOwner.Choice == null, "started crafting survives source death/full board; full hand burns result");
            foreach (var future in B16AutoplayProof.Futures(branch))
            {
                var owner = future.Player1;
                string second = future.IdEntityDic[owner.Choice.Choices[0]].Card.Id;
                B16AutoplayProof.Answer(future);
                var result = owner.HandZone.OfType<BoardObject>().Single();
                Core2026Proof.Check(result.Card[GameTag.MODULAR_ENTITY_PART_1] == Cards.FromId(first).AssetId && result.Card[GameTag.MODULAR_ENTITY_PART_2] == Cards.FromId(second).AssetId, "immutable selection order");
                var snapshot = future.PowerHistory.Last.OfType<PowerHistoryFullEntity>().First(e => e.Entity.Id == result.Id).Entity;
                Core2026Proof.Check(snapshot.Tags.TryGetValue(GameTag.MODULAR_ENTITY_PART_1, out int part1) && part1 == Cards.FromId(first).AssetId
                    && snapshot.Tags.TryGetValue(GameTag.MODULAR_ENTITY_PART_2, out int part2) && part2 == Cards.FromId(second).AssetId,
                    "crafted location's first FULL freezes chosen effects");
                Core2026Proof.Check(result.Durability == 2 && result.Cost == (tier == 1 ? 1 : tier == 2 ? 5 : 10), "pinned bases");
                Core2026Proof.Check(future.HistoryFacts.Query.Discoveries().Count() == discoveries, "crafting earns no Discover synergy credit");
                B16AutoplayProof.AssertSaved(future);
            }
        }
        Console.WriteLine("POOLS Elise components=6/7/7; Runic " + p.HeroClass + "=" + OfflineCardPools.Candidates(game, p.HeroClass, CompositeLocationRules.Spells, 117762).Count);
    }
}
