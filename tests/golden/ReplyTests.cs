using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

public static class ReplyTests
{
    internal static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
    internal static Game Fixture(CardClass own = CardClass.WARRIOR, CardClass enemy = CardClass.MAGE)
    {
        var deck = Enumerable.Range(0, 30).Select(i => Cards.FromId(new[] { "CS2_182", "CS2_120", "CS2_119", "CS2_231", "CS2_121", "CS2_200" }[i % 6])).ToList();
        var g = new Game(new GameConfig { Player1HeroClass = own, Player2HeroClass = enemy,
            Player1Deck = deck, Player2Deck = deck.ToList(), StartPlayer = 1, Shuffle = false,
            SkipMulligan = true, History = true, Logging = false, RandomSeed = 7 });
        g.StartGame();
        foreach (var p in new[] { g.Player1, g.Player2 }) {
            foreach (var c in p.HandZone.ToArray()) { p.HandZone.Remove(c); p.SetasideZone.Add(c); }
            p.BaseMana = 10; p.UsedMana = 0;
        }
        return g;
    }
    internal static FairBotState View(Game g) => FairBotState.Create(g, 1, 71, DeckStyle.Tempo,
        new FairOptions { RemoveOpponentSecrets = true, TrackOwnDeck = true, MaterializeOwnDeckTriggers = true });
    internal static Game Variant(Game g)
    {
        var v = g.CloneExact();
        foreach (var c in v.Player2.HandZone.ToArray())
            v.Player2.HandZone.ChangeEntity(c, Entity.FromCard(v.Player2, Cards.FromId("CS2_029"), id: c.Id));
        int index = 0;
        foreach (var c in v.Player2.DeckZone.ToArray())
            v.Player2.DeckZone.ChangeEntity(c, Entity.FromCard(v.Player2, Cards.FromId(new[] { "EX1_414", "CS2_029", "CS2_032", "EX1_559" }[index++ % 4]), id: c.Id));
        v.SetRandomSeed(99812); v.Player1.DeckZone.Shuffle(); v.Player2.DeckZone.Shuffle(); return v;
    }
    internal static void Run(INodeJob job, int slice = 1000)
    { var scheduler = new NodeScheduler(100000); scheduler.Enqueue(job); while (scheduler.Step(slice) == NodeStepResult.Running) {} Check(job.IsDone, "job exceeded test budget"); }
    internal static Minion Board(Game g, bool own, string id, bool ready = false)
    { var p = own ? g.Player1 : g.Player2; var m = (Minion)Entity.FromCard(p, Cards.FromId(id), zone: p.BoardZone); m.IsExhausted = !ready; g.SummonedMinions.Clear(); return m; }
    internal static string Samples(ReplyJob j) => string.Join(";", j.Samples.Select(s => s.Weight.ToString("R", CultureInfo.InvariantCulture) + ":" + string.Join(",", s.Cards)));
    public static void TestDeterminizedChooseOneReply()
    {
        foreach (var cardId in new[] { "EX1_160", "CORE_EX1_160" }) {
            var g = Fixture(CardClass.WARRIOR, CardClass.DRUID);
            var state = View(g);
            var ended = state.Apply(state.Actions.Single(a => a.Type == PlayerTaskType.END_TURN));
            // Sampled cards need real Choose One branches, including after exact cloning for the reply.
            var reply = ended.Determinize(new[] { cardId }, 91).ForReply();
            var field = typeof(FairBotState).GetField("game", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var masked = (Game)field.GetValue(ended);
            Check(reply.Facts.Own.Hand.Select(c => c.EntityId).SequenceEqual(masked.Player2.HandZone.Select(c => c.Id)),
                "sampled Choose One hand ids must survive materialization and viewpoint change");
            var plays = reply.Actions.Where(a => a.Type == PlayerTaskType.PLAY_CARD).ToArray();
            Check(reply.CanExpand && plays.Select(a => a.ChooseOne).Distinct().OrderBy(x => x).SequenceEqual(new[] { 1, 2 }),
                "sampled Choose One card has both playable branches");
            var summon = reply.Apply(plays.Single(a => a.ChooseOne == 1));
            Check(summon.Facts.Own.Board.Count == 1 && summon.Facts.Own.Board[0].Attack == 3,
                "sampled Power of the Wild summons its Panther");
            Check(reply.Apply(plays.Single(a => a.ChooseOne == 2)).Facts.Own.Hand.Count == 0,
                "sampled buff branch resolves too");
            Check(g.Player2.HandZone.Count == 0 && g.Player2.BoardZone.Count == 0, "reply changed authoritative game");
        }
    }
    public static void TestReplyPreservesNewHandValue()
    {
        var g = Fixture(); Entity.FromCard(g.Player1, Cards.FromId("CS2_182"), zone: g.Player1.HandZone);
        var leaf = View(g); var ended = leaf.Apply(leaf.Actions.Single(a => a.Type == PlayerTaskType.END_TURN));
        var before = ended.Determinize(new[] { "CS2_182" }, 91).ForReply();
        var changed = g.CloneExact(); Entity.FromCard(changed.Player1, Cards.FromId("CS2_182"), zone: changed.Player1.HandZone);
        var other = View(changed); other = other.Apply(other.Actions.Single(a => a.Type == PlayerTaskType.END_TURN));
        var after = other.Determinize(new[] { "CS2_182" }, 91).ForReply();
        var evaluator = new DeckPlanEvaluator(DeckPlans.DragonWarrior);
        Check(Math.Abs(evaluator.ReplyValue(after.Facts, leaf.Facts, after.ForReplyHand(leaf.Facts)) - evaluator.ReplyValue(before.Facts, leaf.Facts, before.ForReplyHand(leaf.Facts)) - 5.5) < 1e-12,
            "reply scoring must retain the value of newly drawn unknown cards");
    }
    public static void TestGeneratedOpponentHandIsSampledAlongsideUnknowns()
    {
        var g = Fixture();
        var mukla = Entity.FromCard(g.Player1, Cards.FromId("EX1_014"), zone: g.Player1.HandZone);
        var state = View(g);
        state = state.Apply(state.Actions.First(a => a.Source == mukla.Id));
        Check(state.Facts.Opponent.Hand.Count(c => !c.Unknown) == 2, "Mukla generates two public Bananas");
        var ended = state.ForReplyEndTurn();
        var reply = ended.Determinize(new[] { "EX1_160" }, 91).ForReply();
        Check(reply.Facts.Own.Hand.Count == 3 && reply.Facts.Own.Hand.Count(c => c.CardId == "EX1_014t") == 2,
            "determinization replaced known Bananas");
        Check(reply.Actions.Where(a => a.Type == PlayerTaskType.PLAY_CARD && a.ChooseOne > 0).Any(),
            "mixed known/sampled hand lost Choose One branches");
        var job = ReplyModel.Job(state, NpcCatalog.All[0], 71, new DeckPlanEvaluator(DeckPlans.DragonWarrior));
        Run(job, 1);
        Check(job.Samples.Count > 0 && job.Samples.All(s => s.Cards.Count == 1) && !double.IsNaN(job.Value), "sample unknown draw while preserving generated Bananas");
    }

    public static void TestWeightedLowerQuartile()
    {
        var outcomes = new[] { new KeyValuePair<double, double>(-10, .1), new KeyValuePair<double, double>(0, .3), new KeyValuePair<double, double>(10, .6) };
        Check(Math.Abs(ReplyModel.Blend(outcomes, .35) - 1.85) < 1e-12, "fractional lower-quartile mass and profile beta");
    }
    public static void TestSamplingLimitsAndPublicPlays()
    {
        var freq = CardFrequencies.For(CardClass.MAGE);
        Check(freq.Count > 50 && freq.Any(x => x.CardId == "CS2_032"), "frequency catalog includes class decks and smoothed Standard pool");
        for (int seed = 0; seed < 30; seed++) {
            var samples = CardFrequencies.Sample(CardClass.MAGE, 10, 4, 10, seed, new[] { "CS2_032", "CS2_032", "EX1_559" });
            foreach (var sample in samples) {
                Check(sample.Cards.Count == 10, "full sampled hand");
                Check(!sample.Cards.Contains("CS2_032") && !sample.Cards.Contains("EX1_559"), "played copies removed");
                Check(sample.Cards.GroupBy(x => x).All(x => x.Count() <= (Cards.FromId(x.Key).Rarity == Rarity.LEGENDARY ? 1 : 2)), "sample copy limit");
            }
        }
    }
    public static void TestReplySlicesAndHiddenVariants()
    {
        var g = Fixture(); Board(g, true, "CS2_182"); Board(g, false, "CS2_182");
        Entity.FromCard(g.Player2, Cards.FromId("CS2_182"), zone: g.Player2.HandZone);
        var bytes = g.SaveSnapshot(); string expected = null;
        foreach (var candidate in new[] { g, Variant(g) }) foreach (int slice in new[] { 1, 7, 1000 }) {
            var job = ReplyModel.Job(View(candidate), NpcCatalog.All[0], 89, new DeckPlanEvaluator(DeckPlans.DragonWarrior)); Run(job, slice);
            string result = job.Value.ToString("R", CultureInfo.InvariantCulture) + ":" + job.Nodes + ":" + Samples(job);
            if (expected == null) expected = result; else Check(expected == result, "reply slices/hidden variants differ");
            Check(job.Samples.All(s => s.Cards.Count == 2), "sample includes exactly one next draw");
        }
        Check(bytes.SequenceEqual(g.SaveSnapshot()), "reply mutated authoritative state");
    }
    public static void TestBoardClearTrapAndDurableControl()
    {
        foreach (string extra in new[] { "CS2_120", "CS2_119" }) {
            var g = Fixture(); for (int i = 0; i < 3; i++) Board(g, true, "CS2_120");
            for (int i = 0; i < 9; i++) Entity.FromCard(g.Player2, Cards.FromId("CS2_182"), zone: g.Player2.HandZone);
            var a = Entity.FromCard(g.Player1, Cards.FromId(extra), zone: g.Player1.HandZone);
            var b = Entity.FromCard(g.Player1, Cards.FromId(extra), zone: g.Player1.HandZone);
            // Ten-health handbuffed bodies survive Flamestrike plus the sampled Sleet Storm / hero-power follow-up.
            if (extra == "CS2_119") { ((Minion)a).Health = 10; ((Minion)b).Health = 10; }
            var hold = View(g); var dump = hold.Apply(hold.Actions.First(x => x.Source == a.Id));
            dump = dump.Apply(dump.Actions.First(x => x.Source == b.Id));
            var h = ReplyModel.Job(hold, NpcCatalog.All[0], 3, new DeckPlanEvaluator(DeckPlans.DragonWarrior));
            var d = ReplyModel.Job(dump, NpcCatalog.All[0], 3, new DeckPlanEvaluator(DeckPlans.DragonWarrior)); Run(h); Run(d);
            // Isolate the actual clear sample: pricing enemy Deathrattles changes the non-clear mixture,
            // which need not prefer holding two Crocolisks against every sampled development hand.
            Check(h.Samples[0].Cards.Contains("CS2_032") && d.Samples[0].Cards.Contains("CS2_032")
                && h.Outcomes[0].Value == h.Samples[0].Weight && d.Outcomes[0].Value == d.Samples[0].Weight,
                "first completed reply must be the Flamestrike sample");
            Check(extra == "CS2_120" ? h.Outcomes[0].Key > d.Outcomes[0].Key : d.Value > h.Value,
                "clear trap/control " + extra + " hold=" + h.Value + " dump=" + d.Value + " clear="
                + h.Outcomes[0].Key + "/" + d.Outcomes[0].Key);
        }
    }
    public static void TestPublicLethalKeepsBlocker()
    {
        var g = Fixture(); g.Player1.Hero.Damage = 25; Board(g, false, "CS2_200");
        var blocker = Board(g, true, "CS2_121", true); blocker.AttackDamage = 1; blocker.Health = 3; blocker.HasTaunt = true;
        var hold = View(g); var trade = hold.Apply(hold.Actions.First(a => a.Source == blocker.Id && a.Target == g.Player2.BoardZone[0].Id));
        var h = ReplyModel.Job(hold, NpcCatalog.All[0], 2, new DeckPlanEvaluator(DeckPlans.DragonWarrior));
        var t = ReplyModel.Job(trade, NpcCatalog.All[0], 2, new DeckPlanEvaluator(DeckPlans.DragonWarrior)); Run(h); Run(t);
        Check((h.Survival ?? 1) > (t.Survival ?? 1), "public attacks must rank surviving Taunt by verified survival");
    }
}
