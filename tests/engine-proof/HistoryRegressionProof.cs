using System.Reflection;
using System.Runtime.Serialization;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class HistoryRegressionProof
{
    private static void Check(bool value, string message) => Core2026Proof.Check(value, "history regression: " + message);
    internal static void Run()
    {
        var failures = new List<string>();
        foreach (var test in new Action[] { DefinitionIsolation, Electra, TrackingDiscover, FixedSpellOptions, NestedOptionCast, KnownDiscoverQueries, KnownDiscoverAfterMoves, PrivateRediscovery, HiddenDiscoverQueries, FixedPaths, Vargoth, Tess, Chronology, Keysmith, BuildABeast, MastersCall, Prelate })
        {
            try { test(); Console.WriteLine("PASS history regression " + test.Method.Name); }
            catch (Exception e) { failures.Add(test.Method.Name + ": " + e.Message); }
        }
        Check(failures.Count == 0, string.Join("; ", failures));
    }
    private static void DefinitionIsolation()
    {
        var g = HistoryFactsProof.Fixture(); var p = g.Player1;
        var card = Card.CreateZombeastCard(Cards.FromId("CS2_182"), Cards.FromId("CS2_120"), true);
        typeof(Card).GetProperty("PlayRequirements").SetValue(card, new Dictionary<PlayReq,int>(card.PlayRequirements));
        card.Power = new Power { PowerTask = new ArmorTask(1), DeathrattleTask = new ArmorTask(2),
            Enchant = new Enchant(GameTag.ATK, EffectOperator.ADD, 1) };
        typeof(Card).GetProperty("Entourage").SetValue(card, new[]{"CS2_231"});
        var entity = Entity.FromCard(p, card, zone: p.HandZone);
        PlayEntity(g, p, entity);
        var before = g.HistoryFacts.CanonicalText();
        var a = g.CloneExact(); var b = g.CloneExact();
        a.IdEntityDic[entity.Id].Card.Power.Enchant.Effects[0] = new Effect(GameTag.ATK, EffectOperator.ADD, 9);
        Check(g.HistoryFacts.CanonicalText() == before && b.HistoryFacts.CanonicalText() == before,
            "nested Power/Enchant/effect array detached from source/sibling facts");
        a.IdEntityDic[entity.Id].Card.Power.PowerTask = new ArmorTask(9);
        a.IdEntityDic[entity.Id].Card.PlayRequirements[PlayReq.REQ_TARGET_TO_PLAY] = 1;
        Check(g.HistoryFacts.CanonicalText() == before && b.HistoryFacts.CanonicalText() == before,
            "clone live definition mutation cannot change source/sibling payload");
        card.Power.DeathrattleTask = new ArmorTask(10);
        card.Tags[GameTag.ATK] = 99;
        card.Entourage[0] = "GAME_005";
        Check(g.HistoryFacts.CanonicalText() == before && a.HistoryFacts.CanonicalText() == before && b.HistoryFacts.CanonicalText() == before,
            "original Power/collections detached from shared facts");
        var materialized = g.HistoryFacts.Query.Plays().Single().Definition.Materialize();
        materialized.Power.PowerTask = new ArmorTask(20);
        materialized.PlayRequirements.Clear();
        Check(g.HistoryFacts.CanonicalText() == before, "materialization never exposes frozen graph");
    }
    private static void Electra()
    {
        var g = HistoryFactsProof.Fixture(); var p = g.Player1;
        Play(g, p, "BOT_411");
        int plays = g.HistoryFacts.Query.Plays().Count();
        int casts = g.HistoryFacts.Query.Casts().Count();
        var source = Play(g, p, "GAME_005");
        Check(source.Zone == p.GraveyardZone && p.GraveyardZone.Count(card => card.Id == source.Id) == 1,
            "Electra non-choice hand spell remains in graveyard exactly once");
        var facts = g.HistoryFacts.Query.Casts().Skip(casts).ToArray();
        Check(g.HistoryFacts.Query.Plays().Count() == plays + 1 && facts.Length == 4,
            "actual Electra adds one play and two casts");
        Check(facts.Where(f => f.Outcome == HistoryCastOutcome.Started).Select(f => f.CasterKind)
            .SequenceEqual(new[] { HistoryCastKind.Player, HistoryCastKind.Repeat })
            && facts.GroupBy(f => f.CastId).All(c => c.Count() == 2
                && c.Select(f => f.Outcome).SequenceEqual(new[] { HistoryCastOutcome.Started, HistoryCastOutcome.Completed })
                && c.Select(f => f.CasterKind).Distinct().Count() == 1),
            "actual Electra classifies Player then Repeat, with matching outcomes");
    }
    private static void TrackingDiscover()
    {
        var g = HistoryFactsProof.Fixture(); var p = g.Player1;
        var card = Cards.FromAssetId(69545);
        Play(g, p, card.Id);
        Check(p.Choice != null && Generic.ChoicePick(p, g, p.Choice.Choices[0]), "modern Tracking answered");
        Check(g.HistoryFacts.Query.Discoveries().Count() == 1, "modern Tracking real-entity producer completes Discover");
    }
    private static void FixedSpellOptions()
    {
        foreach (var type in new[] { DiscoverType.BRANCHING_PATHS, DiscoverType.ELEMENTAL_INVOCATION })
        {
            var initial = HistoryFactsProof.Fixture();
            new DiscoverTask(type, numberOfChoices: 4).Process(initial, initial.Player1, initial.Player1.Hero, null);
            foreach (var g in new[] { initial, initial.CloneExact(), Game.LoadSnapshot(initial.SaveSnapshot()) })
            {
                var p = g.Player1;
                int armor = p.Hero.Armor; int enemyHealth = p.Opponent.Hero.Health;
                int chosen = p.Choice.Choices.Single(id => g.IdEntityDic[id].Card.Id ==
                    (type == DiscoverType.BRANCHING_PATHS ? "LOOT_054c" : "UNG_211c"));
                Check(Generic.ChoicePick(p, g, chosen), type + " option accepted");
                Check(!g.HistoryFacts.Query.Casts().Any() && !g.HistoryFacts.Query.Plays().Any(),
                    type + " option resolution adds neither casts nor plays (also clone/load)");
                Check(type == DiscoverType.BRANCHING_PATHS ? p.Hero.Armor == armor + 6 : p.Opponent.Hero.Health == enemyHealth - 6,
                    type + " option text still resolves");
            }
        }
        var paths = HistoryFactsProof.Fixture(); Play(paths, paths.Player1, "LOOT_054");
        while (paths.Player1.Choice != null)
            Check(Generic.ChoicePick(paths.Player1, paths, paths.Player1.Choice.Choices[0]), "actual Branching Paths answered");
        Check(paths.HistoryFacts.Query.Plays().Count() == 1 && paths.HistoryFacts.Query.Casts().Count() == 2,
            "actual Branching Paths casts only parent spell");
    }
    private static void NestedOptionCast()
    {
        var g = HistoryFactsProof.Fixture(); var p = g.Player1;
        new DiscoverTask(DiscoverType.BRANCHING_PATHS).Process(g, p, p.Hero, null);
        int selected = p.Choice.Choices[0];
        var option = g.IdEntityDic[selected]; var card = option.Card.Clone();
        card.Power = new Power { PowerTask = new CustomTask((game, controller, source, target, stack) => {
            var nested = (Spell)Entity.FromCard(controller, Cards.FromId("GAME_005"));
            Generic.CastSpell(controller, game, nested, null, 0);
        }) };
        Generic.ChangeEntityBlock(p, option, card, false);
        Check(Generic.ChoicePick(p, g, selected), "nested-cast option answered");
        var casts = g.HistoryFacts.Query.Casts().ToArray();
        Check(casts.Length == 2 && casts.All(f => f.DbfId == Cards.FromId("GAME_005").AssetId
            && f.CasterKind == HistoryCastKind.Effect), "option suppression retains genuine nested Effect cast");
    }
    private static Game DiscoverGame(int controllerIndex, bool keepAll = false)
    {
        var g = HistoryFactsProof.Fixture(); var p = controllerIndex == 1 ? g.Player1 : g.Player2;
        if (keepAll)
            new DiscoverTask(CardType.MINION, keepAllCondition: _ => true).Process(g, p, p.Hero, null);
        else
        {
            new SabberStoneCore.EffectSystem.EffectContext(g, p.Id, p.Hero.Id)
                .Discover(new[] { Cards.FromId("CS2_231"), Cards.FromId("CS2_029") });
            Generic.ChoicePick(p, g, p.Choice.Choices[0]);
        }
        return g;
    }
    private static void KnownDiscoverQueries()
    {
        foreach (bool keepAll in new[] { false, true })
        {
            var g = DiscoverGame(1, keepAll);
            var expected = g.HistoryFacts.Query.Discoveries().Single().SelectedDefinitions.Select(d => d.DbfId).ToArray();
            g.ProjectHistoryForFairView(g.Player1.PlayerId);
            foreach (var projected in new[] { g, g.CloneExact(), Game.LoadSnapshot(g.SaveSnapshot()) })
            {
                var query = projected.HistoryFacts.Query;
                Check(query.Discoveries().SelectMany(f => f.SelectedDefinitions).Select(d => d.DbfId).SequenceEqual(expected),
                    "fair query preserves known own selection/keep-all definitions through clone/load");
                Check(query.Discoveries().Any(f => f.SelectedDefinitions.Any(d => d.DbfId == expected[0]))
                    && query.Discoveries().Sum(f => f.SelectedDefinitions.Count) == expected.Length,
                    "known identity Any/count queries stay truthful");
            }
        }
    }
    private static void KnownDiscoverAfterMoves()
    {
        var own = HistoryFactsProof.Fixture(); var p = own.Player1;
        new SabberStoneCore.EffectSystem.EffectContext(own, p.Id, p.Hero.Id)
            .Discover(new[] { HistoryCanonicalProof.Consumer("CS2_231") });
        Generic.ChoicePick(p, own, p.Choice.Choices[0]);
        var selected = p.HandZone.Single();
        Generic.ShuffleIntoDeck(p, p.Hero, p.HandZone.Remove(selected));
        own.ProjectHistoryForFairView(p.PlayerId);
        Check(own.HistoryFacts.Query.Discoveries().Single().SelectedDefinitions.Single().DbfId == Cards.FromId("CS2_231").AssetId
            && own.HistoryFacts.HasConsumers, "known own selection and consumer enrollment survive shuffling to hidden deck");
        var enemy = DiscoverGame(2); var opponent = enemy.Player2;
        var chosen = opponent.HandZone.Last();
        End(enemy); PlayEntity(enemy, opponent, chosen);
        enemy.ProjectHistoryForFairView(enemy.Player1.PlayerId);
        Check(enemy.HistoryFacts.Query.Discoveries().Single().SelectedDefinitions.Single().DbfId == Cards.FromId("CS2_231").AssetId,
            "revealed opponent selection retains known definition");
    }
    private static void PrivateRediscovery()
    {
        var g = HistoryFactsProof.Fixture(); End(g); var p = g.Player2;
        var publicCard = Play(g, p, "CS2_231");
        foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
        foreach (var id in new[] { "CS2_182", "CS2_120" }) p.DeckZone.Add(Entity.FromCard(p, Cards.FromId(id)));
        Generic.ShuffleIntoDeck(p, p.Hero, p.BoardZone.Remove((Minion)publicCard));
        SpecificTask.MastersCall.Process(g, p, p.Hero, null);
        Check(p.Choice != null && p.Choice.Choices.Contains(publicCard.Id)
            && Generic.ChoicePick(p, g, publicCard.Id), "previously public entity privately rediscovered");
        g.ProjectHistoryForFairView(g.Player1.PlayerId);
        Check(!g.HistoryFacts.Query.Discoveries().Single().DefinitionsKnown,
            "earlier public appearance cannot reveal later private selection");
    }
    private static void HiddenDiscoverQueries()
    {
        foreach (bool keepAll in new[] { false, true })
        {
            var g = DiscoverGame(2, keepAll);
            g.ProjectHistoryForFairView(g.Player1.PlayerId);
            foreach (var projected in new[] { g, g.CloneExact(), Game.LoadSnapshot(g.SaveSnapshot()) })
            {
                Check(projected.HistoryFacts.Query.Discoveries().Count() == 1, "hidden Discover count remains available");
                bool rejected = false;
                try { _ = projected.HistoryFacts.Query.Discoveries().Any(f => f.SelectedDefinitions.Any(d => d.DbfId == 179)); }
                catch (NotSupportedException) { rejected = true; }
                Check(rejected, "hidden selection identity Any query throws rather than false");
                rejected = false;
                try { _ = projected.HistoryFacts.Query.Discoveries().Sum(f => f.SelectedDefinitions.Count); }
                catch (NotSupportedException) { rejected = true; }
                Check(rejected, "hidden selection payload count query throws rather than zero");
                if (!keepAll)
                {
                    rejected = false;
                    try { _ = projected.HistoryFacts.Query.Discoveries().Any(f => f.SelectedEntityIds.Contains(1)); }
                    catch (NotSupportedException) { rejected = true; }
                    Check(rejected, "hidden selection entity query throws rather than false");
                }
            }
        }
    }
    private static void FixedPaths()
    {
        foreach(var type in new[]{DiscoverType.BRANCHING_PATHS, DiscoverType.BASIC_TOTEM,
            DiscoverType.ELEMENTAL_INVOCATION, DiscoverType.HEISTBARON_TOGWAGGLE})
        {
            var g = HistoryFactsProof.Fixture(); var p = g.Player1;
            new DiscoverTask(type).Process(g, p, p.Hero, null);
            Check(p.Choice != null && Generic.ChoicePick(p, g, p.Choice.Choices[0]), type + " answered");
            Check(!g.HistoryFacts.Query.Discoveries().Any(), type + " fixed choice is not Discover");
        }
    }
    private static void Vargoth()
    {
        var g = HistoryFactsProof.Fixture(); var p = g.Player1;
        Play(g, p, "GAME_005"); var source = Summon(p, "DAL_558");
        int before = g.HistoryFacts.Query.Casts().Count();
        SpecificTask.ArchmageVargoth.Process(g, p, source, null);
        var facts = g.HistoryFacts.Query.Casts().Skip(before).ToArray();
        Check(facts.Length == 2 && facts.All(f => f.CasterKind == HistoryCastKind.Repeat)
            && facts[0].CastId == facts[1].CastId && facts[1].Outcome == HistoryCastOutcome.Completed,
            "actual Vargoth producer records one Repeat invocation");
    }
    private static void Tess()
    {
        var g = HistoryFactsProof.Fixture(); var p = g.Player1;
        var source = Summon(p, "GIL_598"); Play(g, p, "CS2_004", source);
        int before = g.HistoryFacts.Query.Casts().Count();
        SpecificTask.TessGreymane.Process(g, p, source, null);
        var facts = g.HistoryFacts.Query.Casts().Skip(before).ToArray();
        Check(facts.Length == 2 && facts.All(f => f.CasterKind == HistoryCastKind.Repeat)
            && facts[0].CastId == facts[1].CastId, "actual Tess producer records one Repeat invocation");
    }
    private static void Chronology()
    {
        foreach (string field in new[] { "Turn", "ActiveControllerId" })
        {
            var g = HistoryFactsProof.Fixture(); Play(g, g.Player1, "GAME_005");
            var play = g.HistoryFacts.Query.Plays().Single();
            typeof(HistoryFact).GetField("<" + field + ">k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(play, field == "Turn" ? 0 : g.Player2.Id);
            bool rejected = false;
            try { Game.LoadSnapshot(g.SaveSnapshot()); } catch (SerializationException) { rejected = true; }
            Check(rejected, "begun-turn chronology rejects forged " + field);
        }
    }
    private static void Keysmith()
    {
        var g = HistoryFactsProof.Fixture(); var p = g.Player1;
        var source = Summon(p, "GIL_116");
        SpecificTask.ArcaneKeysmith.Process(g, p, source, null);
        Check(p.Choice != null && Generic.ChoicePick(p, g, p.Choice.Choices[0]), "Keysmith selection accepted");
        Check(g.HistoryFacts.Query.Discoveries().Count() == 1 && p.SecretZone.Count == 1,
            "actual Keysmith Discover completes once");
    }
    private static void BuildABeast()
    {
        var g = HistoryFactsProof.Fixture(); var p = g.Player1;
        SpecificTask.BuildABeast.Process(g, p, p.Hero, null);
        for (int i = 1; i <= 2; i++)
        {
            Check(p.Choice != null && Generic.ChoicePick(p, g, p.Choice.Choices[0]), "BuildABeast selection accepted");
            Check(g.HistoryFacts.Query.Discoveries().Count() == i, "each actual BuildABeast Discover completes once");
        }
        Check(p.HandZone.Single().Card.Name == "Zombeast", "BuildABeast composed output retained");
    }
    private static void MastersCall()
    {
        foreach(bool beasts in new[]{false,true})
        {
            var g=HistoryFactsProof.Fixture();var p=g.Player1;
            if(beasts)
            {
                foreach(var e in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(e));
                foreach(var id in new[]{"CS2_171","CS2_172","CS2_203"}) p.DeckZone.Add(Entity.FromCard(p,Cards.FromId(id)));
            }
            SpecificTask.MastersCall.Process(g,p,p.Hero,null);
            if(!beasts) Check(p.Choice!=null && Generic.ChoicePick(p,g,p.Choice.Choices[0]),"Master's Call selection accepted");
            Check(g.HistoryFacts.Query.Discoveries().Count()==1
                && g.HistoryFacts.Query.Discoveries().Single().KeepAll==beasts,"Master's Call selection/keep-all semantic completion");
        }
    }
    private static void Prelate()
    {
        var g = HistoryFactsProof.Fixture(); var p = g.Player1; var source = Summon(p, "TRL_306");
        var control = g.CloneExact();
        int index = control.Random.Next(control.Player1.DeckZone.Count);
        int count = p.DeckZone.Count; int triggers = 0;
        g.TriggerManager.ShuffleIntoDeckTrigger += _ => triggers++;
        SpecificTask.ImmortalPrelate.Process(g, p, source, null);
        var inserted = p.DeckZone[index];
        Check(p.DeckZone.Count == count + 1 && inserted.Card.Id == source.Card.Id, "Prelate inserts at same random position");
        Check(g.HistoryFacts.Query.Shuffles().Count() == 1
            && g.HistoryFacts.Query.Events<ShuffleOperationFact>().Single().EntityId == inserted.Id,
            "actual Prelate direct insertion records one shuffle");
        Check(triggers == 0 && g.Random.Next() == control.Random.Next(), "Prelate keeps legacy triggers and RNG");
    }
}
