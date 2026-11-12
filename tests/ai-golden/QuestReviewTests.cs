using System;
using System.Linq;
using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static ReplyTests;

public static class QuestReviewTests
{
    private static DeckPlanEvaluator Eval() => new DeckPlanEvaluator(DeckPlans.DragonWarrior);
    private static FairBotState Strict(FairBotState state)
    { state.FaultReporter = message => { throw new Exception(message); }; return state; }
    private static Game Engine(FairBotState state) => (Game)typeof(FairBotState)
        .GetField("game", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(state);
    private static Spell Install(Game g, string id, bool own = false)
    {
        var q = (Spell)Entity.FromCard(own ? g.Player1 : g.Player2, Cards.FromId(id));
        Check(QuestRules.Install(q), "install " + id); return q;
    }
    private static void Near(double a, double b, string why) => Check(Math.Abs(a - b) < 1e-8, why + ": " + a + "/" + b);
    private static Game Pending()
    {
        var g = Fixture(); var q = Install(g, "TLC_602");
        for (int i = 0; i < 10; i++) {
            var card = Entity.FromCard(g.Player2, Cards.FromId("CS2_182"), zone: g.Player2.HandZone);
            card[GameTag.REVEALED] = 1;
        }
        QuestRules.Advance(q, 10);
        Check(g.Player2.PendingQuestRewards.Count == 1, "pending reward fixture"); return g;
    }
    public static void TestPendingRewardMutationThroughDeliveryAndSearch()
    {
        var g = Pending(); var target = Board(g, false, "ICC_218");
        var moonfire = Entity.FromCard(g.Player1, Cards.FromId("CS2_008"), zone: g.Player1.HandZone);
        var a = Strict(View(g)); var queued = g.Player2.PendingQuestRewards.Dequeue();
        g.Player2.PendingQuestRewards.Enqueue(("CS2_029", queued.CreatorId));
        var b = Strict(View(g)); Near(a.Evaluate(Eval()), b.Evaluate(Eval()), "root score");
        Check(GoldenPublicKey.For(a) == GoldenPublicKey.For(b), "root public key");
        var hit = a.Actions.Single(x => x.Source == moonfire.Id && x.Target == target.Id);
        var x1 = a.Apply(hit); var x2 = b.Apply(hit);
        Check(x1.Facts.Opponent.PendingQuestRewards.Count == 0 && x1.Facts.Opponent.Hand.Count == 10, "Howlfiend releases reward");
        Near(x1.Evaluate(Eval()), x2.Evaluate(Eval()), "delivered score must ignore queued ID");
        Check(GoldenPublicKey.For(x1) == GoldenPublicKey.For(x2), "delivered public key");
        var p1 = TurnPlanner.Job(a, NpcCatalog.All[0], DeckPlans.DragonWarrior, 71, 6000);
        var p2 = TurnPlanner.Job(b, NpcCatalog.All[0], DeckPlans.DragonWarrior, 71, 6000);
        Run(p1); Run(p2);
        Check(PlannerTests.Describe(p1.Result) == PlannerTests.Describe(p2.Result), "search ignores private queued ID");
        Near(p1.Result.Value, p2.Result.Value, "search score ignores private queued ID");
    }
    public static void TestReplyConversionPreservesFullHandQueueAndQuestStage()
    {
        foreach (bool pending in new[] { true, false }) {
            var g = pending ? Pending() : Fixture();
            if (!pending) {
                Install(g, "END_017");
                for (int i = 0; i < 10; i++) Generic.AddHandPhase(g.Player2, Entity.FromCard(g.Player2, Cards.FromId("GAME_005")));
                g.Process(EndTurnTask.Any(g.Player1));
            } else g.Process(EndTurnTask.Any(g.Player1));
            var view = View(g);
            var state = view.Determinize(Enumerable.Repeat("CS2_182", view.Facts.Opponent.Hand.Count(c => c.Unknown)).ToArray(), 91);
            var before = Engine(state).Player2;
            if (!pending) Check(((Spell)before.SecretZone.Single(q => q.Card.IsQuest)).QuestState.Stage == 1
                && before.SecretZone.Single(q => q.Card.IsQuest).QuestProgress == 1, "END_017 starts at progress/stage 1/1");
            var hands = string.Join(";", before.HandZone.Select(c => c.Id + ":" + c.Card.Id + ":" + c.Cost));
            var queue = before.PendingQuestRewards.ToArray();
            var quests = before.SecretZone.Where(q => q.Card.IsQuest).Select(q => q.Id + ":" + q.QuestProgress + ":" + ((Spell)q).QuestState.Stage).ToArray();
            var key = GoldenPublicKey.For(state);
            var reply = state.ForReply(); var after = Engine(reply).Player2;
            Check(hands == string.Join(";", after.HandZone.Select(c => c.Id + ":" + c.Card.Id + ":" + c.Cost)), "conversion changed hand");
            Check(queue.SequenceEqual(after.PendingQuestRewards), "conversion delivered pending reward");
            Check(quests.SequenceEqual(after.SecretZone.Where(q => q.Card.IsQuest).Select(q => q.Id + ":" + q.QuestProgress + ":" + ((Spell)q).QuestState.Stage)), "conversion advanced quest stage");
            Check(key == GoldenPublicKey.For(state) && queue.SequenceEqual(Engine(state).Player2.PendingQuestRewards), "conversion mutated parent");
        }
    }
    public static void TestFullHandPendingReplySamplesScore()
    {
        var job = ReplyModel.Job(Strict(View(Pending())), NpcCatalog.All[0], 71, Eval()); Run(job);
        Check(job.Samples.Count == 8 && job.Outcomes.Count == 8, "all eight reply samples must score: " + job.Samples.Count + "/" + job.Outcomes.Count);
    }
    public static void TestImpossibleQuestAndFatigueHorizonLoseValue()
    {
        var g = Fixture(); Install(g, "TLC_EVENT_400");
        foreach (var p in new[] { g.Player1, g.Player2 })
            foreach (var c in p.DeckZone.ToArray()) { p.DeckZone.Remove(c); p.SetasideZone.Add(c); }
        var state = View(g);
        Near(Eval().QuestAssets(state.Facts.Opponent, state.Facts.Opponent.Hand, false), 0, "empty Mage cannot play three Beasts");
        var early = Fixture(); Install(early, "TLC_602", true);
        var late = early.CloneExact(); late.Turn = 39;
        foreach (var c in late.Player1.DeckZone.ToArray()) { late.Player1.DeckZone.Remove(c); late.Player1.SetasideZone.Add(c); }
        late.Player1.Hero.Damage = 27;
        var e = View(early).Facts.Own; var l = View(late).Facts.Own;
        Check(Eval().QuestAssets(l, l.Hand, true) < .1 * Eval().QuestAssets(e, e.Hand, true), "fatigue horizon discounts distant reward");
        var quest = Entity.FromCard(late.Player1, Cards.FromId("TLC_830"), zone: late.Player1.HandZone);
        var boar = Entity.FromCard(late.Player1, Cards.FromId("CS2_171"), zone: late.Player1.HandZone);
        late.Player1.BaseMana = 1;
        var job = TurnPlanner.Job(View(late), NpcCatalog.All[0], DeckPlans.DragonWarrior, 71, 1926); Run(job);
        Check(job.Result.Line.First().Source == boar.Id, "late impossible quest must lose to development");
    }
    public static void TestOpponentZombeastKeepsOpaqueValueUntilDeparture()
    {
        var g = Fixture(); var quest = Install(g, "TLC_EVENT_400"); quest.QuestProgress = 2;
        g.Process(EndTurnTask.Any(g.Player1));
        var beast = Entity.FromCard(g.Player2, Cards.FromId("CS2_171"), zone: g.Player2.HandZone);
        var before = View(g); var eval = Eval();
        Check(g.Process(g.Player2.Options().First(t => t.Source?.Id == beast.Id)), "play completing Beast");
        while (g.Player2.Choice != null) Check(g.Process(ChooseTask.Pick(g.Player2, g.Player2.Choice.Choices[0])), "choose Zombeast");
        var reward = g.Player2.HandZone.Last(); var after = View(g);
        double asset = eval.QuestAssets(after.Facts.Opponent, after.Facts.Opponent.Hand, false);
        Check(after.Facts.Opponent.OpaqueQuestRewards.Count == 1 && after.Facts.Opponent.Hand.All(c => c.Unknown), "reward prior must not expose components");
        Check(after.Facts.Opponent.OpaqueQuestRewards.Single().Value == quest.Card.Id,
            "opaque metadata identifies only the public quest, never the crafted reward");
        Near(asset, .8 * 18, "public Storm recipe retains its bounded reward estimate");
        var fork = after.WithArtificialSeed(999).ForkForSearch(message => { throw new Exception(message); });
        Near(asset, eval.QuestAssets(fork.Facts.Opponent, fork.Facts.Opponent.Hand, false), "search forks retain public reward prior");
        var reply = fork.Determinize(Enumerable.Repeat("CS2_171", fork.Facts.Opponent.Hand.Count(c => c.Unknown)).ToArray(), 91).ForReply();
        int publicReward = reply.Facts.Own.OpaqueQuestRewards.Single().Key;
        reply = reply.Apply(reply.Actions.First(a => a.Source == publicReward));
        Check(reply.Facts.Own.OpaqueQuestRewards.Count == 0, "playing sampled reward retires entity-linked prior");
        Check(asset >= eval.QuestAssets(before.Facts.Opponent, before.Facts.Opponent.Hand, false), "Zombeast completion must retain public reward value: " + asset);
        reward.Card = Cards.FromId("CS2_029"); reward[GameTag.ATK] = 100; reward[GameTag.HEALTH] = 100;
        Near(after.Evaluate(eval), View(g).Evaluate(eval), "private components cannot change value");
        Check(GoldenPublicKey.For(after) == GoldenPublicKey.For(View(g)), "private components cannot change key");
        g.Player2.HandZone.Remove(reward); g.Player2.DeckZone.Add(reward);
        var gone = View(g); Near(eval.QuestAssets(gone.Facts.Opponent, gone.Facts.Opponent.Hand, false), 0, "departed reward loses prior");
    }
    public static void TestGeneratedOpponentQuestUsesPublicClassSupport()
    {
        var g = Fixture(); g.Process(EndTurnTask.Any(g.Player1));
        var quest = Entity.FromCard(g.Player2, Cards.FromId("TLC_EVENT_400"));
        var generator = Entity.FromCard(g.Player2, Cards.FromId("CORE_BAR_541"), zone: g.Player2.GraveyardZone);
        quest[GameTag.DISPLAYED_CREATOR] = generator.Id;
        Generic.AddHandPhase(g.Player2, quest);
        Check(g.Process(g.Player2.Options().First(t => t.Source?.Id == quest.Id)), "play generated Storm");
        var state = View(g); var p = state.Facts.Opponent;
        Check(p.Quests.Single().GeneratedQuest, "public generation link survives projection");
        double value = Eval().QuestAssets(p, p.Hand, false);
        Check(value < 1, "generated Mage quest lacks supporting class prior: " + value);
        var other = View(Variant(g)); Near(state.Evaluate(Eval()), other.Evaluate(Eval()), "support prior ignores actual deck");
    }
    private static Game StormGame(string held)
    {
        var g = Fixture(); var quest = Install(g, "TLC_EVENT_400"); quest.QuestProgress = 2;
        if (held == "JAIL_852") Entity.FromCard(g.Player1, Cards.FromId("CS2_171"), zone: g.Player1.HandZone);
        Check(g.Process(EndTurnTask.Any(g.Player1)), "start Storm completion turn");
        var beast = Entity.FromCard(g.Player2, Cards.FromId("CS2_171"), zone: g.Player2.HandZone);
        Check(g.Process(g.Player2.Options().First(t => t.Source?.Id == beast.Id)), "complete Storm");
        while (g.Player2.Choice != null) Check(g.Process(ChooseTask.Pick(g.Player2, g.Player2.Choice.Choices[0])), "choose Zombeast");
        var support = Entity.FromCard(g.Player2, Cards.FromId(held), zone: g.Player2.HandZone);
        support[GameTag.REVEALED] = 1;
        return g;
    }
    private static FairBotState StormReply(string held)
    {
        var root = Strict(View(StormGame(held)));
        Check(root.Facts.Opponent.OpaqueQuestRewards.Count == 1, "public Storm reward fixture");
        var sampled = root.Determinize(Enumerable.Repeat("CS2_171", root.Facts.Opponent.Hand.Count(c => c.Unknown)).ToArray(), 91);
        Check(sampled.Facts.Opponent.OpaqueQuestRewards.Count == 1, "determinization retains prior");
        var reply = sampled.ForReply();
        Check(reply.Facts.Own.OpaqueQuestRewards.Count == 1, "viewpoint conversion retains prior");
        return reply;
    }
    public static void TestTransformedOpaqueRewardLosesPrior()
    {
        var reply = StormReply("CATA_200"); var leaf = reply.Facts;
        int reward = leaf.Own.OpaqueQuestRewards.Single().Key;
        int agent = leaf.Own.Hand.Single(c => c.CardId == "CATA_200").EntityId;
        var choosing = reply.Apply(reply.Actions.First(a => a.Source == agent));
        var transformed = choosing.Apply(BotAction.From(ChooseTask.Pick(Engine(choosing).CurrentPlayer, reward)));
        Check(transformed.Facts.Own.Hand.Single(c => c.EntityId == reward).CardId == "GAME_005", "Agent transforms reward into Coin");
        var eval = Eval(); double score = eval.ReplyValue(transformed.Facts, leaf, transformed.ForReplyHand(leaf));
        // Clearing a copy supplies the score oracle without changing the candidate or its parent.
        var cleared = transformed.ForkForSearch(message => { throw new Exception(message); });
        typeof(FairBotState).GetField("opaqueQuestRewards", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(cleared, new System.Collections.Generic.Dictionary<int, string>());
        Near(score, eval.ReplyValue(cleared.Facts, leaf, cleared.ForReplyHand(leaf)), "transformed Coin has no phantom reward threat");
        Check(transformed.Facts.Own.OpaqueQuestRewards.Count == 0, "gameplay transformation retires prior");
        Check(reply.Facts.Own.OpaqueQuestRewards.Count == 1, "transformation does not mutate parent prior");
    }
    public static void TestStolenOpaqueRewardLosesPrior()
    {
        var reply = StormReply("JAIL_852"); int reward = reply.Facts.Own.OpaqueQuestRewards.Single().Key;
        int king = reply.Facts.Own.Hand.Single(c => c.CardId == "JAIL_852").EntityId;
        bool transferred = false;
        for (int seed = 0; seed < 16; seed++) {
            var sample = reply.WithArtificialSeed(seed);
            var after = sample.Apply(sample.Actions.First(a => a.Source == king));
            if (Engine(after).IdEntityDic[reward].Controller == Engine(after).CurrentPlayer) continue;
            transferred = true;
            Check(Engine(after).IdEntityDic[reward].Zone.Type == Zone.HAND, "stolen reward still occupies a hand");
            Check(after.Facts.Own.OpaqueQuestRewards.Count + after.Facts.Opponent.OpaqueQuestRewards.Count == 0,
                "hand departure by theft retires prior instead of transferring it");
            Check(reply.Facts.Own.OpaqueQuestRewards.Count == 1, "theft does not mutate parent prior");
            break;
        }
        Check(transferred, "hand shuffle must exercise theft");
    }
    public static void TestReturnedStolenOpaqueRewardLosesRootPrior()
    {
        var g = StormGame("JAIL_852"); int reward = g.Player2.HandZone[g.Player2.HandZone.Count - 2].Id;
        Check(View(g).Facts.Opponent.OpaqueQuestRewards.Count == 1, "root starts with prior");
        bool stolen = false, returned = false;
        for (int seed = 0; seed < 32; seed++) {
            g.SetRandomSeed(seed); SwapRules.ShuffleHands(g.Player2);
            if (g.IdEntityDic[reward].Controller == g.Player1) stolen = true;
            else if (stolen) { returned = true; break; }
        }
        Check(stolen && returned, "shuffle exercises theft and return to original hand");
        Check(View(g).Facts.Opponent.OpaqueQuestRewards.Count == 0, "root reconstruction cannot resurrect departed prior");
    }
    public static void TestLateEmptyDeckWarriorPrefersBoar()
    {
        foreach (int life in new[] { 3, 30 }) {
            var g = Fixture(); g.Turn = 39; g.Player1.BaseMana = 1; g.Player1.Hero.Damage = 30 - life;
            foreach (var c in g.Player1.DeckZone.ToArray()) { g.Player1.DeckZone.Remove(c); g.Player1.SetasideZone.Add(c); }
            Entity.FromCard(g.Player1, Cards.FromId("TLC_602"), zone: g.Player1.HandZone);
            var boar = Entity.FromCard(g.Player1, Cards.FromId("CS2_171"), zone: g.Player1.HandZone);
            // Static evaluator pin: since starved-replies a 1926 cap replies here, and at life 30 it and the full tier search both take the quest.
            var job = TurnPlanner.Job(Strict(View(g)), NpcCatalog.All[0].WithFixes(new AiFixes(new[] { "starved-replies" })), DeckPlans.DragonWarrior, 71, 1926); Run(job);
            Check(job.Result.Line.First().Source == boar.Id, "empty-deck Warrior develops instead of starting ten-turn quest, life=" + life);
        }
    }
}
