using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static class RoundTwoSuite
{
    internal static NpcProfile Profile(int width = 16, int cap = 1926)
    {
        var p = NpcCatalog.All[0];
        return new NpcProfile(p.Id, p.Version, p.DisplayName, p.DeckId, p.HeroSkin, p.CardBack, p.Personality,
            // Mistake bounds on the unreplied 1926-node path (starved-replies would reply here, which widens the candidate gaps).
            new Budget(28000, cap, 0, 8, 3, 160, width), p.PlanId, p.HeroSkinPower, fixes: new AiFixes(new[] { "starved-replies" }));
    }
    internal static GoldenContext Context(FairBotState view, NpcProfile profile, int decision = 1) =>
        new GoldenContext(view.Facts.Turn, decision, 71, profile);
    internal static PlannedTurn Search(FairBotState view, DeckPlan plan, NpcProfile profile)
    {
        var job = TurnPlanner.Job(view, profile, plan, 71);
        while (!job.IsDone) job.StepNode();
        return job.Result;
    }
    internal static Minion Body(Game game, bool own, int attack, int health, bool taunt = false)
    {
        var player = own ? game.Player1 : game.Player2;
        var body = (Minion)Entity.FromCard(player, Cards.FromId("CS2_182"), zone: player.BoardZone);
        body[GameTag.ATK] = attack; body[GameTag.HEALTH] = health; body[GameTag.TAUNT] = taunt ? 1 : 0;
        body.IsExhausted = false; game.SummonedMinions.Clear(); return body;
    }
    internal static FairBotState TradeView()
    {
        var game = PassivitySuite.Fixture(0);
        Body(game, true, 3, 3); Body(game, false, 2, 2, true); Body(game, false, 2, 1, true);
        return PassivitySuite.View(game);
    }
    internal static void MasterTrades()
    {
        var view = TradeView(); var plan = PassivitySuite.Plan(); var profile = Profile();
        var raw = Search(view, plan, profile);
        var trades = raw.Candidates.Where(c => c.Key.Type == PlayerTaskType.MINION_ATTACK).OrderByDescending(c => c.Value).ToArray();
        Program.Require(trades.Length == 2 && trades[0].Value - trades[1].Value > 0 && trades[0].Value - trades[1].Value < 4,
            "engine supplies distinct trade losses within master bound");
        var master = SkillCatalog.Parse(Program.Data("skills-v1.json")).Find("master"); int count = 0;
        for (int seed = 0; seed < 2000; seed++)
        {
            var pilot = new SkilledPilot(new GoldenPilot(profile, plan), null, master, seed);
            var decision = pilot.Decide(view, Context(view, profile));
            if (decision.Action.Equals(raw.Line[0])) continue;
            count++;
            Program.Require(decision.SearchInfo.Mistake != "none", "accepted master deviation records its mistake kind");
            double actual = raw.Value - Search(view.Apply(decision.Action), plan, profile).Value;
            Program.Require(actual > 0 && actual <= 4 + 1e-9, "master real trade loss inside bound");
        }
        Program.Require(count > 0 && count < 200, "master has nonzero deterministic engine deviations: " + count);
        Console.WriteLine("PASS master trades: " + count + "/2000, gap=" + (trades[0].Value - trades[1].Value));
    }
    internal static void BoundedKinds()
    {
        var view = TradeView(); var plan = PassivitySuite.Plan(); var profile = Profile();
        var searched = Search(view, plan, profile);
        var other = searched.Candidates.Where(c => c.Key.Type == PlayerTaskType.MINION_ATTACK && !c.Key.Equals(searched.Line[0])).Single();
        var best = new GoldenDecision(searched.Line[0], searched.Value, searched.Nodes, searched.Line.Count, "Exhausted", null,
            new[] { new KeyValuePair<BotAction, double>(searched.Line[0], searched.Value),
                new KeyValuePair<BotAction, double>(other.Key, searched.Value - .01) });
        var cappedSkill = new SkillProfile("cap-test", 1, 0, 1, profile.Budget, 0, 1, 1,
            new[] { new MistakeRule(MistakeKind.TradeTarget, 1, 4) });
        var capped = new SkilledPilot(cappedSkill, 0, plan, v => new FixedPilot(best), "cap-test");
        Program.Require(capped.Decide(view, Context(view, Profile(16, 1))).Action.Equals(best.Action)
            && capped.PerturbFailures == 0, "incomplete continuation cannot certify a mistake");
        var clock = new SkilledPilot(cappedSkill, 0, plan, v => new FixedPilot(best), "clock-test").Begin(view, Context(view, profile));
        clock.StepNode();
        Program.Require(!clock.IsDone && ((INpcSearchContinuation)clock).StopSearch(SearchStop.Clock).Action.Equals(best.Action),
            "clock interruption during continuation validation keeps searched best");
        foreach (var kind in new MistakeKind?[] { MistakeKind.TradeTarget, null, MistakeKind.IgnoreAoeRisk })
        {
            var skill = new SkillProfile("price-test", 1, 0, 1, profile.Budget, kind.HasValue ? 0 : 1000, 2, 1,
                kind.HasValue ? new[] { new MistakeRule(kind.Value, 1, kind == MistakeKind.IgnoreAoeRisk ? 0 : 1) } : new MistakeRule[0], 1);
            for (int seed = 0; seed < 32; seed++)
            {
                var pilot = new SkilledPilot(skill, seed, plan, v => new FixedPilot(v == SkilledVariant.None ? best :
                    new GoldenDecision(other.Key, other.Value, 1, 1, "Exhausted", null, null)), "price-test");
                var result = pilot.Decide(view, Context(view, profile));
                Program.Require(result.Action.Equals(best.Action) && pilot.PerturbFailures == 0,
                    "real continuation bound enforced for " + (kind?.ToString() ?? "softmax"));
            }
        }
        var game = PassivitySuite.Fixture(0); Body(game, true, 3, 3); Body(game, false, 2, 2);
        view = PassivitySuite.View(game); searched = Search(view, plan, profile);
        var face = view.Actions.Single(a => a.Type == PlayerTaskType.MINION_ATTACK && a.Target == view.Facts.Opponent.Hero.EntityId);
        Program.Require(searched.Line[0].Target != face.Target, "face-bound fixture prefers trade");
        best = new GoldenDecision(searched.Line[0], searched.Value, searched.Nodes, searched.Line.Count, "Exhausted", null,
            new[] { new KeyValuePair<BotAction, double>(searched.Line[0], searched.Value), new KeyValuePair<BotAction, double>(face, searched.Value - .01) });
        var faceSkill = new SkillProfile("face-test", 1, 0, 1, profile.Budget, 0, 1, 1,
            new[] { new MistakeRule(MistakeKind.FaceBias, 1, 1) });
        var facePilot = new SkilledPilot(faceSkill, 0, plan, v => new FixedPilot(best), "face-test");
        Program.Require(facePilot.Decide(view, Context(view, profile)).Action.Equals(best.Action) && facePilot.PerturbFailures == 0,
            "FaceBias real continuation exceeds one-point bound");

        game = PassivitySuite.Fixture(0); game.Player2.Hero.Health = 3;
        for (int i = 0; i < 3; i++) Body(game, true, 1, 2);
        view = PassivitySuite.View(game);
        var lethal = new LethalJob(view, 500); while (!lethal.IsDone) lethal.StepNode();
        Program.Require(lethal.Result.Outcome == LethalOutcome.Found && lethal.Result.Line.Count >= 3, "engine supplies missable long lethal");
        best = new GoldenDecision(lethal.Result.Line[0], 1000000, 1, lethal.Result.Line.Count, "Terminal", null, null);
        var end = view.Actions.Single(a => a.Type == PlayerTaskType.END_TURN);
        var miss = new GoldenDecision(end, view.Apply(end).Evaluate(new DeckPlanEvaluator(plan)), 1, 1, "Exhausted", null, null);
        var lethalSkill = new SkillProfile("lethal-test", 1, 0, 1, profile.Budget, 0, 1, 1,
            new[] { new MistakeRule(MistakeKind.LethalMiss, 1, 0, 3) });
        var lethalPilot = new SkilledPilot(lethalSkill, 0, plan, v => new FixedPilot(v == SkilledVariant.None ? best : miss, v == SkilledVariant.None), "lethal-test");
        Program.Require(lethalPilot.Decide(view, Context(view, profile)).Action.Equals(best.Action) && lethalPilot.PerturbFailures == 0,
            "LethalMiss cannot bypass its declared zero loss bound");

        view = TradeView(); searched = Search(view, plan, profile);
        best = new GoldenDecision(searched.Line[0], searched.Value, searched.Nodes, searched.Line.Count, "Exhausted", null, searched.Candidates);
        var pendingSkill = new SkillProfile("pending-test", 1, 0, 1, profile.Budget, 0, 1, 1,
            new[] { new MistakeRule(MistakeKind.IgnoreAoeRisk, 1, 0) });
        var pending = new SkilledPilot(pendingSkill, 0, plan, v => new FixedPilot(best, pending: v != SkilledVariant.None), "pending-test")
            .Begin(view, Context(view, profile));
        pending.StepNode();
        Program.Require(((INpcBackgroundSearch)pending).Waiting, "variant background wait must not burn turn nodes");
        Program.Require(((INpcSearchContinuation)pending).StopSearch(SearchStop.Clock).Action.Equals(best.Action)
            && !((INpcBackgroundSearch)pending).BackgroundRunning, "clock cut retains baseline and stops variant worker");
        Console.WriteLine("PASS repriced TradeTarget, softmax, FaceBias, IgnoreAoeRisk and LethalMiss bounds");
    }
    private static SkillProfile HoldSkill(NpcProfile p, double loss) => new SkillProfile("hold-test", 1, 0, 1,
        p.Budget, 0, 1, 1, new[] { new MistakeRule(MistakeKind.HoldTooLong, 1, loss) });
    internal static FairBotState HoldView()
    {
        var game = PassivitySuite.Fixture(5, "CS2_011");
        game.Player1.Hero.HeroPower.IsExhausted = true;
        Body(game, true, 1, 2); Body(game, true, 1, 2);
        return PassivitySuite.View(game);
    }
    internal static void Holds()
    {
        var view = HoldView(); var plan = AutoDeckPlan.Derive(NpcDeckLibrary.Parse(Program.Data("decks-v1.json")).Find("npc-death-knight-aggro")); var profile = Profile();
        var raw = Search(view, plan, profile);
        Console.WriteLine("HOLD ROOT " + raw.Value + " " + string.Join(";", raw.Line));
        int card = view.Facts.Own.Hand.Single().EntityId;
        Program.Require(raw.Line[0].Source == card, "hold fixture starts with attack buff");
        var strict = new SkilledPilot(new GoldenPilot(profile, plan), null, HoldSkill(profile, 2), 0);
        var kept = strict.Decide(view, Context(view, profile));
        Program.Require(kept.Action.Source == card, "two-point hold cannot discard the later buff");
        var pilot = new SkilledPilot(new GoldenPilot(profile, plan), null, HoldSkill(profile, 8), 0);
        var state = view; int decisions = 0; GoldenDecision first = null;
        while (state.CanExpand && decisions < 10)
        {
            var decision = pilot.Decide(state, Context(state, profile, ++decisions));
            if (first == null) first = decision;
            Program.Require(decision.Action.Source != card, "sticky hold survives actual subsequent decisions");
            state = state.Apply(decision.Action);
        }
        double actualLoss = raw.Value - state.Evaluate(new DeckPlanEvaluator(plan));
        Program.Require(!state.CanExpand && decisions >= 3 && pilot.MistakeCount(MistakeKind.HoldTooLong) == 1,
            "priced hold reaches turn completion through multiple decisions");
        Program.Require(first.SearchInfo.Mistake == "HoldTooLong", "accepted sticky hold records its typed mistake kind");
        Program.Require(actualLoss > 2 && actualLoss <= 8 && Math.Abs(first.Value - state.Evaluate(new DeckPlanEvaluator(plan))) < 1e-8,
            "hold price equals replayed completed turn loss: " + actualLoss);
        Console.WriteLine("PASS hold replay: decisions=" + decisions + " loss=" + actualLoss);
    }
    internal static FairBotState OwlView()
    {
        // Owl828 turn 12 public position. Platysaur was summoned, so deliberately has no linked draw enchantment.
        var game = PassivitySuite.Fixture(6, "TLC_830", "TLC_366", "DINO_138", "TLC_366", "CATA_554", "TLC_823", "TLC_EVENT_400", "TIME_053");
        game.Turn = 12; game.Player1.Hero.Damage = 1; game.Player2.Hero.Damage = 3;
        game.Player1.Hero.HeroPower = (HeroPower)Entity.FromCard(game.Player1, Cards.FromId("DS1h_292"));
        Entity.FromCard(game.Player2, Cards.FromId("JAIL_122"), zone: game.Player2.BoardZone);
        Entity.FromCard(game.Player2, Cards.FromId("TLC_603"), zone: game.Player2.BoardZone);
        while (game.Player2.HandZone.Count < 7) Entity.FromCard(game.Player2, Cards.FromId("CS2_182"), zone: game.Player2.HandZone);
        return PassivitySuite.View(game);
    }
    internal static void Owl()
    {
        var view = OwlView(); var plan = AutoDeckPlan.Derive(NpcDeckLibrary.Parse(Program.Data("decks-v1.json")).Find("npc-hunter-quest"));
        var narrow = Search(view, plan, Profile(8)); var wide = Search(view, plan, Profile(16));
        Console.WriteLine("OWL width8=" + narrow.Value + " width16=" + wide.Value + " " + string.Join(";", wide.Line));
        Program.Require(wide.Value >= narrow.Value - 1e-9, "width 16 retains or improves the narrower Owl value");
        int roar = view.Facts.Own.Hand.Single(c => c.CardId == "CATA_554").EntityId;
        int cower = view.Facts.Own.Hand.Single(c => c.CardId == "TLC_823").EntityId;
        int food = view.Facts.Own.Hand.Single(c => c.CardId == "TLC_830").EntityId;
        int storm = view.Facts.Own.Hand.Single(c => c.CardId == "TLC_EVENT_400").EntityId;
        int sandmaw = view.Facts.Own.Hand.Single(c => c.CardId == "TIME_053").EntityId;
        int manastorm = view.Facts.Opponent.Board.Single(c => c.CardId == "JAIL_122").EntityId;
        // Quest valuation replaces the old 2-mana power with both quests; all five plays spend 6 mana.
        Program.Require(wide.Line.Count == 6 && wide.Line.Last().Type == PlayerTaskType.END_TURN
            && wide.Line.Any(a => a.Type == PlayerTaskType.PLAY_CARD && a.Source == roar && a.Target == manastorm)
            && wide.Line.Any(a => a.Type == PlayerTaskType.PLAY_CARD && a.Source == cower && a.Target == manastorm)
            && new[] { food, storm, sandmaw }.All(id => wide.Line.Any(a => a.Type == PlayerTaskType.PLAY_CARD && a.Source == id)),
            "wider Owl line finds Roar and Cower on Manastorm, Food Chain, Storm the Gates and Sandmaw");
        var replay = view;
        foreach (var action in wide.Line.Take(wide.Line.Count - 1)) {
            Program.Require(replay.Actions.Contains(action), "wider Owl action is legal: " + action);
            replay = replay.Apply(action);
        }
        Program.Require(replay.Facts.Own.Mana == 0
            && replay.Facts.Own.Board.Single().EntityId == sandmaw
            && replay.Facts.Opponent.Board.Single().CardId == "TLC_603"
            && replay.Facts.Own.Quests.Count == 2
            && replay.Facts.Own.Quests.All(q => q.Tags[GameTag.QUEST_PROGRESS] == 1),
            "wider Owl clears Manastorm and discounts Sandmaw after both quests, advancing each once");
        int small = view.Facts.Opponent.Board.Single(c => c.CardId == "TLC_603").EntityId;
        var wrong = view.Actions.Single(a => a.Source == roar && a.Target == small);
        // Price against the recovered best line; width 8 now also misses the quest/removal commitment.
        double real = wide.Value - Search(view.Apply(wrong), plan, Profile(16)).Value;
        Program.Require(real > 8.5, "Owl wrong Roar actual replanned loss exceeds casual bound: " + real);
        // Reproduce the journal's 3.15 estimate at the selector boundary; the engine must veto its continuation.
        var raw = new GoldenDecision(wide.Line[0], wide.Value, wide.Nodes, wide.Line.Count, "Exhausted", null,
            new[] { new KeyValuePair<BotAction, double>(wide.Line[0], wide.Value), new KeyValuePair<BotAction, double>(wrong, wide.Value - 3.15) });
        var skill = new SkillProfile("owl-trade", 2, 0, 1, Profile(16).Budget, 0, 1, 1,
            new[] { new MistakeRule(MistakeKind.TradeTarget, 1, 8.5) });
        var wrapper = new SkilledPilot(skill, 0, plan, v => new FixedPilot(raw), "owl");
        var selected = wrapper.Decide(view, Context(view, Profile(16)));
        Program.Require(!selected.Action.Equals(wrong) && wrapper.PerturbFailures == 0, "Owl target cannot pass with a one-ply price");
        Console.WriteLine("PASS Owl bounded target: actual loss=" + real);
    }
    internal static void Width()
    {
        var view = OwlView(); var plan = AutoDeckPlan.Derive(NpcDeckLibrary.Parse(Program.Data("decks-v1.json")).Find("npc-hunter-quest"));
        Program.Require(Search(view, plan, Profile(NpcCatalog.All[0].Budget.OwnBeamWidth)).Value
            >= Search(view, plan, Profile(16)).Value - 1e-9, "shipping beam retains the wider Owl line");
        Console.WriteLine("PASS shipping beam retains wider Owl removal, discount and quest development");
    }
    internal sealed class FixedPilot : IFrameSlicedGoldenPilot
    {
        private readonly GoldenDecision decision;
        private readonly bool lethal;
        private readonly bool pending;
        internal FixedPilot(GoldenDecision decision, bool lethal = false, bool pending = false)
        { this.decision = decision; this.lethal = lethal; this.pending = pending; }
        public GoldenDecision Decide(FairBotState view, GoldenContext context) => decision;
        public IReadOnlyList<int> Mulligan(IGoldenMulliganView view, GoldenContext context) => new int[0];
        public IGoldenDecisionJob Begin(FairBotState view, GoldenContext context) => new FixedJob(decision, lethal, pending);
    }
    private sealed class FixedJob : IGoldenDecisionJob, INpcBackgroundSearch
    {
        private readonly bool lethal;
        private readonly bool pending;
        private bool stopped;
        internal FixedJob(GoldenDecision decision, bool lethal, bool pending) { Result = decision; this.lethal = lethal; this.pending = pending; }
        public bool IsDone => !pending;
        public int BackgroundNodes => 0;
        public bool Waiting => pending;
        public bool BackgroundClock => pending;
        public bool BackgroundRunning => pending && !stopped;
        public void LimitBackground(double milliseconds) { }
        public void PollBackground() { }
        public void StopBackground() { stopped = true; }
        public GoldenDecision Result { get; }
        public string LethalResult => lethal ? "found" : "none";
        public bool PlanReused => false;
        public void StepNode() { }
    }
}
