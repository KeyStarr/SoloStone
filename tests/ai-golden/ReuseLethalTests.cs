using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static ReplyTests;

public static class ReuseLethalTests
{
    // Live miss in a playtest match, turn 19 (Warrior NPC against a Rogue): the turn-start plan was made while a Taunt
    // stood, the lethal search stopped early, and later steps reused that plan without looking for lethal again, so a
    // 20/10 minion hit a 7/7 instead of the face for exact lethal.
    // Public turn-19 combat state only. Private hands/decks/history are deliberately not fixtures.
    private static Game Incident(bool taunt = false)
    {
        var g = Fixture(CardClass.WARRIOR, CardClass.ROGUE); g.Turn = 19;
        g.Player1.UsedMana = 10; g.Player1.Hero.Damage = 13; g.Player1.Hero.Armor = 6;
        g.Player2.Hero.Damage = 3; g.Player2.Hero.Armor = 10;
        string[] ids = { "TLC_106", "DINO_410t", "DINO_410t", "DINO_410t2", "DINO_410t4", "DINO_410t3", "TLC_252" };
        int[] attack = { 6, 20, 20, 0, 0, 0, 3 }, health = { 5, 19, 10, 2, 2, 2, 2 };
        for (int i = 0; i < ids.Length; i++) {
            var m = Board(g, true, ids[i], true); m.AttackDamage = attack[i]; m[GameTag.HEALTH] = health[i]; m.Damage = 0;
        }
        if (taunt) {
            var m = Board(g, false, "JAIL_384"); m.AttackDamage = 15; m[GameTag.HEALTH] = 30; m.Damage = 21;
        }
        var victim = Board(g, false, "EDR_527"); victim.AttackDamage = 7; victim[GameTag.HEALTH] = 7;
        g.DeathProcessingAndAuraUpdate(); return g;
    }
    private static LethalJob Probe(FairBotState view)
    {
        var job = new LethalJob(view, GoldenDecisionJob.LethalRecheckNodes, null, null, immediate: true);
        Run(job, 1); return job;
    }
    private static GoldenPilot Exhausted(Game g, bool off, out FairBotState view, bool drain = true)
    {
        var profile = NpcCatalog.All[0].WithFixes(new AiFixes(off ? new[] { "lethal-recheck" } : new string[0]));
        var b = profile.Budget;
        profile = new NpcProfile(profile.Id, profile.Version, profile.DisplayName, profile.DeckId, profile.HeroSkin,
            profile.CardBack, profile.Personality, new Budget(28000, 7000, 4000, b.ReplyK, b.ReplyL, b.ReplyR,
                b.OwnBeamWidth, b.Dedupe, 45, 55, b.Brain), profile.PlanId, profile.HeroSkinPower, profile.Fixes);
        var pilot = new GoldenPilot(profile);
        view = View(g); pilot.Job(view, new GoldenContext(19, 1, 71, profile));
        while (drain && pilot.RemainingLethalNodes > 0) pilot.ChargeLethal();
        return pilot;
    }
    private static GoldenDecision Decide(GoldenPilot pilot, FairBotState view, int decision)
    {
        var job = pilot.Job(view, new GoldenContext(19, decision, 71, pilot.Profile));
        Run(job, 1); return job.Result;
    }
    public static void TestIncidentCachedTradeAfterTwoTorches()
    {
        foreach (bool off in new[] { true, false }) {
            var g = Incident(true);
            var torches = new List<IPlayable>();
            for (int i = 0; i < 2; i++) {
                var c = Entity.FromCard(g.Player1, Cards.FromId("CATA_585"), zone: g.Player1.HandZone);
                c.Cost = 0; c[GameTag.TAG_SCRIPT_DATA_NUM_1] = 5; torches.Add(c);
            }
            var pilot = Exhausted(g, off, out var view);
            var taunt = view.Facts.Opponent.Board.Single(c => c.Taunt);
            var casts = torches.Select(c => view.Actions.Single(a => a.Source == c.Id && a.Target == taunt.EntityId)).ToArray();
            var after = view.Apply(casts[0]).Apply(casts[1]);
            var giant = after.Facts.Own.Board.Single(c => c.CardId == "DINO_410t" && c.Health == 10);
            var victim = after.Facts.Opponent.Board.Single(c => c.CardId == "EDR_527");
            var trade = after.Actions.Single(a => a.Source == giant.EntityId && a.Target == victim.EntityId);
            // Seed the interrupted plan observed live, while the Taunt still stands; no hidden-deck reconstruction.
            var line = casts.Concat(new[] { trade }).Concat(after.Actions.Where(a => a.Type == PlayerTaskType.MINION_ATTACK
                && a.Target == after.Facts.Opponent.Hero.EntityId && a.Source != giant.EntityId)).ToArray();
            pilot.Accept(view, line, 0, LethalOutcome.Incomplete, interrupted: true);
            view = view.Apply(casts[0]);
            int nodes = 0; bool sawLethal = false;
            for (int i = 2; i < 8 && view.Facts.Own.PlayState == PlayState.PLAYING; i++) {
                var result = Decide(pilot, view, i); nodes += result.Nodes;
                if (i == 2 && off) Check(result.SearchInfo.Reused && result.Action.Equals(casts[1]), "OFF continues second Torch");
                if (i == 3 && off) Check(result.SearchInfo.Reused && result.Action.Equals(trade), "OFF reproduces fatal cached trade");
                sawLethal |= result.SearchInfo.Replan == "lethal";
                view = view.Apply(result.Action);
                if (result.Action.Type == PlayerTaskType.END_TURN) break;
            }
            if (off) Check(view.Facts.Opponent.Hero.Health + view.Facts.Opponent.Hero.Armor == 8, "OFF leaves exactly 8 life");
            else Check(view.Facts.Own.PlayState == PlayState.WON && sawLethal, "fresh probe must replace cached trade with lethal");
            Check(pilot.RemainingLethalNodes == 0, "fresh probe must not borrow shared lethal budget");
            Console.WriteLine("reuse-lethal incident off=" + off + " nodes=" + nodes + " deckMs@0.6=" + (nodes / .6));
        }
    }
    public static void TestParallelReuseDoesNotCancelFreshLethalCheck()
    { ParallelReuse(false); }
    public static void TestParallelReuseRetainsCompletedWitness()
    { ParallelReuse(true); }
    private static void ParallelReuse(bool completedWitness)
    {
        bool previous = GoldenPilot.ParallelLethalEnabled;
        try {
            GoldenPilot.ParallelLethalEnabled = true;
            foreach (bool off in new[] { true, false }) {
                var g = Incident(true); var torch = Entity.FromCard(g.Player1, Cards.FromId("CATA_585"), zone: g.Player1.HandZone);
                torch.Cost = 0; torch[GameTag.TAG_SCRIPT_DATA_NUM_1] = 9;
                var pilot = Exhausted(g, off, out var view, drain: false);
                pilot.ObserveLethalSearch = search => {
                    if (!completedWitness) { search.LimitMilliseconds(0); return; }
                    var watch = Stopwatch.StartNew();
                    while (!search.IsDone && watch.ElapsedMilliseconds < 5000) { search.Poll(); System.Threading.Thread.Yield(); }
                    Check(search.IsDone && search.Result.Outcome == LethalOutcome.Found, "controlled completed worker witness");
                };
                var cast = view.Actions.Single(a => a.Source == torch.Id && a.Target == view.Facts.Opponent.Board.Single(c => c.Taunt).EntityId);
                var child = view.Apply(cast);
                var giant = child.Facts.Own.Board.Single(c => c.CardId == "DINO_410t" && c.Health == 10);
                var trade = child.Actions.Single(a => a.Source == giant.EntityId && a.Target == child.Facts.Opponent.Board.Single().EntityId);
                pilot.Accept(view, new[] { cast, trade, child.Actions.First(a => a.Type == PlayerTaskType.END_TURN) }, 0,
                    LethalOutcome.Incomplete, interrupted: true);
                var job = pilot.Job(child, new GoldenContext(19, 2, 71, pilot.Profile));
                // Force either no worker expansion or a completed witness before reuse, independent of scheduling.
                for (int i = 0; !job.IsDone && i < 1000; i++) job.StepNode();
                Check(job.IsDone, "parallel cached decision completes");
                if (off && !completedWitness) Check(job.PlanReused && job.Result.Action.Equals(trade), "OFF cancels workers to reuse fatal trade");
                else Check(!job.PlanReused && job.LethalResult == "found" && job.Result.Action.Target == child.Facts.Opponent.Hero.EntityId,
                    "serial fresh proof must precede parallel reuse");
                Check(pilot.RemainingLethalNodes > 0, "race reproduces without spending the turn lethal reserve");
                Console.WriteLine("reuse-lethal parallel off=" + off + " completedWitness=" + completedWitness + " nodes=" + job.Result.Nodes + " remainingLethal=" + pilot.RemainingLethalNodes);
            }
        }
        finally { GoldenPilot.ParallelLethalEnabled = previous; }
    }
    public static void TestFreshDecisionAfterExhaustedLethalReserve()
    {
        var g = Incident(); var pilot = Exhausted(g, false, out var view);
        var result = Decide(pilot, view, 2);
        Check(result.SearchInfo.Replan == "lethal" && result.Action.Target == view.Facts.Opponent.Hero.EntityId,
            "an incomplete earlier search cannot suppress an uncached decision either");
    }
    public static void TestShieldTauntAndTwoTaunts()
    {
        foreach (string kind in new[] { "shield", "two", "deathrattle" }) {
            var g = Incident();
            var m = Board(g, false, kind == "deathrattle" ? "FP1_012" : "CS2_182"); m.AttackDamage = 1; m[GameTag.HEALTH] = 4; m.HasTaunt = true;
            if (kind == "shield") m.HasDivineShield = true;
            else if (kind == "two") { m = Board(g, false, "CS2_182"); m.AttackDamage = 1; m[GameTag.HEALTH] = 1; m.HasTaunt = true; }
            var view = View(g); var probe = Probe(view);
            Check(probe.Result.Outcome == LethalOutcome.Found, kind + " lethal within fresh cap");
            foreach (var a in probe.Result.Line) view = view.Apply(a);
            Check(view.Facts.Own.PlayState == PlayState.WON, kind + " real engine lethal");
            Console.WriteLine("reuse-lethal " + kind + " nodes=" + probe.Result.Nodes);
        }
    }
    public static void TestCombatRestrictionsAndWeapon()
    {
        foreach (string kind in new[] { "freeze", "cant-face", "rush", "exhausted", "hero-shield", "immune" }) {
            var g = Incident(); var giant = g.Player1.BoardZone.Single(m => m.Card.Id == "DINO_410t" && m.Health == 10);
            if (kind == "freeze") giant.IsFrozen = true;
            if (kind == "cant-face") giant[GameTag.CANNOT_ATTACK_HEROES] = 1;
            if (kind == "rush") { giant[GameTag.RUSH] = 1; giant[GameTag.NUM_TURNS_IN_PLAY] = 0; giant.AttackableByRush = true; }
            if (kind == "exhausted") giant.IsExhausted = true;
            if (kind == "hero-shield") { g.Player2.Hero.HasDivineShield = true; g.Player2.Hero.Armor = 21; }
            if (kind == "immune") g.Player2.Hero.IsImmune = true;
            Check(Probe(View(g)).Result.Outcome != LethalOutcome.Found, kind + " cannot manufacture face damage");
        }
        var weapon = LethalTests.Fixture("fissure"); var state = LethalTests.View(weapon);
        var proof = Probe(state); Check(proof.Result.Outcome == LethalOutcome.Found, "weapon and known hand reach");
        foreach (var a in proof.Result.Line) state = state.Apply(a);
        Check(state.Facts.Own.PlayState == PlayState.WON, "weapon line is lethal");
    }
    public static void TestStealthedTauntDoesNotBlockFace()
    {
        var g = Incident(); var taunt = Board(g, false, "CS2_182"); taunt.HasTaunt = true; taunt.HasStealth = true;
        Check(Probe(View(g)).Result.Outcome == LethalOutcome.Found, "stealthed Taunt is not a legal blocker");
    }
    public static void TestSecretsCannotCertifyRawDamage()
    {
        var g = Fixture(CardClass.WARRIOR, CardClass.MAGE);
        Check(g.Process(EndTurnTask.Any(g.Player1)), "enemy turn");
        var secret = Entity.FromCard(g.Player2, Cards.FromId("EX1_295"), zone: g.Player2.HandZone);
        Check(g.Process(PlayCardTask.Spell(g.Player2, secret)), "armed Ice Block");
        Check(g.Process(EndTurnTask.Any(g.Player2)), "bot turn");
        foreach (var c in g.Player1.HandZone.ToArray()) { g.Player1.HandZone.Remove(c); g.Player1.SetasideZone.Add(c); }
        g.Player1.UsedMana = 10; g.Player2.Hero.Damage = 29;
        Board(g, true, "CS2_182", true);
        var view = new GoldenPilot(NpcCatalog.All[0]).Options.FairViewFactory(g, 1, 71, DeckStyle.Tempo);
        Check(view.Facts.OpponentSecretCount == 1, "public Secret count retained");
        Check(Probe(view).Result.Outcome != LethalOutcome.Found, "uncovered Secret or Ice Block prevents certificate");
    }
    private sealed class TimedNode : INodeJob
    {
        private readonly INodeJob inner; private readonly double[] clock; private readonly double rate;
        internal TimedNode(INodeJob inner, double[] clock, double rate) { this.inner = inner; this.clock = clock; this.rate = rate; }
        public bool IsDone => inner.IsDone;
        public void StepNode() { inner.StepNode(); clock[0] += 1 / rate; }
    }
    private sealed class TimedDecision : IGoldenDecisionJob, INpcLethalSearch, INpcPreparedDecision, INpcSearchContinuation, INpcPlanNodeCap
    {
        private readonly GoldenDecisionJob inner; private readonly TimedNode lethal; private readonly double[] clock; private readonly double rate;
        internal TimedDecision(GoldenDecisionJob inner, double[] clock, double rate)
        { this.inner = inner; this.clock = clock; this.rate = rate; lethal = new TimedNode(inner.LethalSearch, clock, rate); }
        public bool IsDone => inner.IsDone;
        public GoldenDecision Result => inner.Result;
        public string LethalResult => inner.LethalResult;
        public bool PlanReused => inner.PlanReused;
        public bool Preparing => inner.Preparing;
        public INodeJob LethalSearch => lethal;
        public void StepNode() { inner.StepNode(); clock[0] += 1 / rate; }
        public GoldenDecision StopSearch(SearchStop stop) => inner.StopSearch(stop);
        public void CapPlanNodes(Func<int> cap) => inner.CapPlanNodes(cap);
    }
    public static void TestDeckRateRunnerKeepsRetailTurn()
    {
        foreach (double rate in new[] { .6, .8 }) {
            var g = Incident(true);
            var torches = new List<IPlayable>();
            for (int i = 0; i < 2; i++) {
                var c = Entity.FromCard(g.Player1, Cards.FromId("CATA_585"), zone: g.Player1.HandZone);
                c.Cost = 0; c[GameTag.TAG_SCRIPT_DATA_NUM_1] = 5; torches.Add(c);
            }
            var pilot = Exhausted(g, false, out var view);
            var casts = torches.Select(c => view.Actions.Single(a => a.Source == c.Id && a.Target == view.Facts.Opponent.Board.Single(m => m.Taunt).EntityId)).ToArray();
            var after = view.Apply(casts[0]).Apply(casts[1]);
            var giant = after.Facts.Own.Board.Single(c => c.CardId == "DINO_410t" && c.Health == 10);
            var trade = after.Actions.Single(a => a.Source == giant.EntityId && a.Target == after.Facts.Opponent.Board.Single().EntityId);
            pilot.Accept(view, casts.Concat(new[] { trade, after.Actions.First(a => a.Type == PlayerTaskType.END_TURN) }).ToArray(), 0,
                LethalOutcome.Incomplete, interrupted: true);
            view = view.Apply(casts[0]);
            var clock = new double[1]; int decision = 1; bool ended = false; double busyUntil = 0;
            var logs = new List<string>();
            var runner = new NpcTurnRunner(pilot.Profile, "reuse-lethal", () => clock[0], () => !ended,
                () => new GoldenContext(19, ++decision, 71, pilot.Profile),
                ctx => new TimedDecision(pilot.Job(view, ctx), clock, rate),
                reason => new GoldenDecision(view.Actions.First(a => a.Type == PlayerTaskType.END_TURN), 0, 0, 0, "Clock", reason, null),
                result => {
                    view = view.Apply(result.Action); busyUntil = clock[0] + 1500;
                    ended = view.Facts.Own.PlayState != PlayState.PLAYING || result.Action.Type == PlayerTaskType.END_TURN;
                }, () => clock[0] >= busyUntil, logs.Add, turnNodes: 23150, turnDeadlineLeft: () => 75000 - clock[0]);
            runner.Start(); clock[0] = 12870; // observed first-decision wall time already spent
            for (int i = 0; runner.Active && i < 100000; i++) { runner.Tick(); clock[0] += clock[0] < busyUntil ? 40 : 14; }
            Check(!runner.Active && view.Facts.Own.PlayState == PlayState.WON, "Deck-rate runner must finish lethal");
            Check(clock[0] < 75000 && runner.ClockCuts == 0, "retail timer and live guard");
            Check(runner.TurnSearchNodes > 0, "fresh lethal reserve is in runner accounting");
            Console.WriteLine("reuse-lethal runner rate=" + rate + " turnMs=" + clock[0] + " nodes=" + runner.TurnSearchNodes
                + " searchMs=" + runner.SearchMilliseconds + " decisions=" + runner.Decisions);
        }
    }
    public static void TestParallelRunnerCountsProbeAndWorkersOnce()
    {
        bool previous = GoldenPilot.ParallelLethalEnabled;
        try {
            GoldenPilot.ParallelLethalEnabled = true;
            var g = Incident(); g.Player2.Hero.IsImmune = true;
            var p = NpcCatalog.All[0]; var b = p.Budget;
            var profile = new NpcProfile(p.Id, p.Version, p.DisplayName, p.DeckId, p.HeroSkin, p.CardBack, p.Personality,
                new Budget(500, 50, 32, b.ReplyK, b.ReplyL, b.ReplyR, b.OwnBeamWidth), p.PlanId);
            var pilot = new GoldenPilot(profile); var view = View(g); bool ended = false; GoldenDecision result = null;
            double extra = 0; var watch = Stopwatch.StartNew();
            var runner = new NpcTurnRunner(profile, "probe-accounting", () => watch.Elapsed.TotalMilliseconds + extra, () => !ended,
                () => new GoldenContext(19, 1, 71, profile), ctx => pilot.Job(view, ctx),
                reason => throw new Exception("unexpected fallback " + reason),
                made => { result = made; ended = true; }, () => true);
            runner.Start();
            for (int i = 0; runner.Active && i < 100000; i++) { runner.Tick(); if (runner.State == NpcTurnState.Thinking) System.Threading.Thread.Sleep(1); else extra += 100; }
            Check(result != null && !runner.Active, "parallel runner completes");
            Check(result.Value < 999000 && result.Nodes == runner.TurnSearchNodes,
                "serial probe + main + background workers counted exactly once: result=" + result?.Nodes + " runner=" + runner.TurnSearchNodes);
        }
        finally { GoldenPilot.ParallelLethalEnabled = previous; }
    }
    public static void TestProbeRejectsLuckyRandomAndPreservesAuthority()
    {
        var g = LethalTests.Fixture("random"); var before = g.SaveSnapshot();
        Check(Probe(LethalTests.View(g)).Result.Outcome != LethalOutcome.Found, "lucky trigger cannot bypass verification");
        Check(before.SequenceEqual(g.SaveSnapshot()), "probe mutates authority");
    }
}
