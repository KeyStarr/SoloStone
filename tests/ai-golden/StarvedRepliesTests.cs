using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static ReplyTests;

// starved-replies. Live elite turn (JadeBrewer t21, 2026-11-04): caps 16190, 5033, 561, 100 on replans; below about 3,423
// nodes no reply at all, up to about 7,000 one, which only re-scores the static leader. Capped replans now keep R and take
// fewer samples (never under 4), refitted to what the own beam left, and spend what replies leave on further finalists.
public static class StarvedRepliesTests
{
    private static NpcProfile Tier(bool on, int k = 14, int l = 9, int plan = 70000, int width = 25)
    {
        var p = NpcCatalog.All[0];
        var b = new Budget(plan * 4, plan, 500, k, l, 160, width, true, 45, 55);
        return new NpcProfile(p.Id, p.Version, p.DisplayName, p.DeckId, p.HeroSkin, p.CardBack, p.Personality, b, p.PlanId, p.HeroSkinPower,
            new AiFixes(on ? new string[0] : new[] { "starved-replies" }));
    }
    private static NpcProfile Elite(bool on) => Tier(on);
    private static NpcProfile Master(bool on) => Tier(on, 8, 3, 7000, 16);
    // Three ready attackers into two blockers and an open face, nothing else: a small beam with distinct commitments.
    private static Game Position()
    {
        var g = Fixture();
        g.Player1.UsedMana = 8;
        foreach (string id in new[] { "CS2_120", "CS2_182", "CS2_119" }) Board(g, true, id, true);
        foreach (string id in new[] { "CS2_231", "CS2_121" }) Board(g, false, id);
        foreach (string id in new[] { "CS2_189", "CS2_120" }) Entity.FromCard(g.Player1, Cards.FromId(id), zone: g.Player1.HandZone);
        return g;
    }
    private static TurnPlannerJob Plan(NpcProfile profile, int cap, bool comparePass = false)
    {
        var job = new TurnPlannerJob(View(Position()), profile, DeckPlans.DragonWarrior, 71, cap, null, comparePass: comparePass);
        Run(job);
        return job;
    }
    private static string Fingerprint(TurnPlannerJob j) => j.Result.Nodes + "|" + j.Result.Value.ToString("R") + "|" + string.Join(";", j.Result.Line)
        + "|" + j.DiagnosticFinalists + "|" + j.DiagnosticReplyK;

    public static void TestFitRepliesThresholds()
    {
        var elite = Elite(true).Budget; var master = Master(true).Budget;
        Check(ReplyModel.SearchAllowance(14, 160) == 2565 && ReplyModel.SearchAllowance(8, 160) == 1467 && ReplyModel.SearchAllowance(4, 160) == 735, "allowances moved");
        foreach (var (budget, nodes, k, finalists) in new[] {
            (elite, 0, 14, 0), (elite, 1469, 14, 0),          // two four-sample replies do not fit: no reply, the own beam keeps it
            (elite, 1470, 4, 2), (elite, 1835, 4, 2), (elite, 1836, 5, 2), (elite, 2204, 6, 2),
            (elite, 2205, 4, 3), (elite, 2754, 5, 3), (elite, 7694, 13, 3), (elite, 7695, 14, 3), (elite, 100000, 14, 3),
            (master, 1469, 8, 0), (master, 1470, 4, 2), (master, 2205, 4, 3), (master, 4400, 7, 3), (master, 4401, 8, 3) }) {
            var fit = TurnPlannerJob.FitReplies(nodes, budget, false);
            Check(fit.K == k && fit.Finalists == finalists, $"fit {budget.ReplyK}/{nodes}: {fit.K}x{fit.Finalists}, want {k}x{finalists}");
        }
    }

    public static void TestStarvedReplanGetsReplies()
    {
        foreach (int cap in new[] { 2400, 3000, 3423, 5033, 6078, 7000 }) {
            var off = Plan(Elite(false), cap); var on = Plan(Elite(true), cap);
            Check(off.DiagnosticFinalists <= (cap >= 6732 ? 2 : 1), $"cap {cap} OFF is the starved base: {off.DiagnosticFinalists}");
            Check(on.DiagnosticFinalists >= 2, $"cap {cap} ON must compare at least two lines: {on.DiagnosticFinalists} K{on.DiagnosticReplyK}");
            Check(on.DiagnosticReplyK >= TurnPlannerJob.MinReplySamples && on.DiagnosticReplyK <= 14, $"cap {cap} samples {on.DiagnosticReplyK}");
            Check(on.DiagnosticReplies.All(n => n.Actions != null), "replied lines");
        }
        // Unused own-beam nodes come back: a cheap beam at 6078 affords more samples than the 3/4 plan, three finalists at least.
        var refit = Plan(Elite(true), 6078);
        Check(refit.DiagnosticFinalists >= 3 && refit.DiagnosticReplyK > 7, $"refit at 6078: {refit.DiagnosticFinalists} K{refit.DiagnosticReplyK}");
    }

    public static void TestCappedBeamKeepsItsPlannedReplies()
    {
        // A wide position fills the own beam's share to the node; the replies planned at the start must still run.
        var g = Fixture(); g.Player1.UsedMana = 6;
        foreach (string id in new[] { "CS2_120", "CS2_182", "CS2_119", "CS2_200", "CS2_121", "CS2_120" }) Board(g, true, id, true);
        foreach (string id in new[] { "CS2_231", "CS2_121", "CS2_182", "CS2_200", "CS2_119" }) Board(g, false, id);
        foreach (int cap in new[] { 2400, 3000, 3725, 6078 }) {
            var job = Plan2(g, cap);
            Check(job.DiagnosticFinalists >= 2, $"cap {cap}: capped beam ({job.DiagnosticOwnNodes} nodes) lost its replies: {job.DiagnosticFinalists} K{job.DiagnosticReplyK}");
            Check(job.Result.Nodes <= cap, $"cap {cap}: {job.Result.Nodes} nodes");
        }
    }

    public static void TestLonePassGetsNoReply()
    {
        var g = Fixture(); g.Player1.UsedMana = 10; g.Player1.Hero.HeroPower.IsExhausted = true;
        Board(g, false, "CS2_231");
        var on = Plan2(g, 3000);
        Check(on.DiagnosticFinalists == 0 && on.Result.Line.All(a => a.Type == PlayerTaskType.END_TURN) && on.Result.Nodes < 20, $"nothing to compare: {on.DiagnosticFinalists} finalists, {on.Result.Nodes} nodes");
    }
    private static TurnPlannerJob Plan2(Game g, int cap)
    {
        var job = new TurnPlannerJob(View(g), Elite(true), DeckPlans.DragonWarrior, 71, cap, null);
        Run(job);
        return job;
    }

    public static void TestReducedSamplesFacePassValidation()
    {
        // A pass chosen on fewer samples is not the tier's verdict: the pilot validates it at full strength, as after a capped beam.
        var g = Fixture(); g.Player1.UsedMana = 6; g.Player1.Hero.HeroPower.IsExhausted = true;
        foreach (string id in new[] { "CS2_120", "CS2_182" }) Board(g, true, id);
        Entity.FromCard(g.Player1, Cards.FromId("CS2_062"), zone: g.Player1.HandZone); // Hellfire on an own board and an empty enemy one
        var reduced = Plan2(g, 3000);
        Check(reduced.DiagnosticReplyK < 14 && reduced.DiagnosticFinalists >= 2 && reduced.Result.Line[0].Type == PlayerTaskType.END_TURN, $"fixture: pass at K{reduced.DiagnosticReplyK}: {string.Join(";", reduced.Result.Line)}");
        Check(reduced.Result.Interrupted, "a pass on reduced samples is flagged");
        Check(!Plan2(g, 70000).Result.Interrupted, "the tier's own search is not");
        GoldenDecision Decide(bool on)
        {
            var pilot = new GoldenPilot(Elite(on));
            var view = pilot.Options.FairViewFactory(g, 1, 71, DeckStyle.Tempo);
            var job = pilot.Job(view, new GoldenContext(g.Turn, 1, 71, pilot.Profile, pilot.PublicInputs));
            job.CapPlanNodes(() => 3000);
            for (int i = 0; !job.IsDone && i < 400000; i++) job.StepNode();
            Check(job.IsDone, "decision finished");
            return job.Result;
        }
        var validated = Decide(true);
        Check(validated.SearchInfo.ValidationNodeCap > 0 && validated.SearchInfo.ValidationNodes > 0, "reduced-sample pass validated: " + validated.SearchInfo.ValidationNodes);
        // OFF: the unreplied pass from a finished beam is trusted, the base behaviour.
        Check(Decide(false).SearchInfo.ValidationNodeCap == 0, "OFF keeps the base path");
    }

    public static void TestExtrasOnlyFromTheUnreservedShare()
    {
        // The cap's own finalists run exactly as OFF; extras appear, and stop at the share unless the reserve is off.
        NpcProfile Without(string id) => Elite(true).WithFixes(new AiFixes(new[] { id }));
        var g = Fixture(); g.Player1.UsedMana = 2;
        foreach (string id in new[] { "CS2_120", "CS2_182", "CS2_119" }) Board(g, true, id, true);
        foreach (string id in new[] { "CS2_231", "CS2_121", "CS2_182" }) Board(g, false, id);
        foreach (string id in new[] { "CS2_189", "CS2_120", "CS2_182", "CS2_119", "CS2_029" }) Entity.FromCard(g.Player1, Cards.FromId(id), zone: g.Player1.HandZone);
        TurnPlannerJob Run2(NpcProfile p, int nodes) { var j = new TurnPlannerJob(View(g), p, DeckPlans.DragonWarrior, 71, nodes, null); Run(j); return j; }
        const int cap = 14000;
        var off = Run2(Elite(false), cap); var on = Run2(Elite(true), cap); var free = Run2(Without("replan-reserve"), cap);
        Check(on.DiagnosticFinalists >= off.DiagnosticFinalists, $"never fewer than the cap paid: {on.DiagnosticFinalists} < {off.DiagnosticFinalists}");
        Check(free.DiagnosticFinalists > off.DiagnosticFinalists, $"unused nodes buy extras: {free.DiagnosticFinalists} vs {off.DiagnosticFinalists}");
        Check(free.Result.Nodes > on.Result.Nodes || on.Result.Nodes <= off.Result.Nodes, $"the reserve holds extras back: {free.Result.Nodes} vs {on.Result.Nodes}");
        Check(on.Result.Nodes <= Math.Max(off.Result.Nodes, TurnPlannerJob.ExtraFinalistShare * cap + ReplyModel.SearchAllowance(14, 160)), $"extras stop at the share: {on.Result.Nodes}");
    }

    public static void TestStarvedQuestLinesMatchTheTierSearch()
    {
        // Real quest positions: where four samples reply, the first action is the one the tier search takes.
        foreach (var (quest, card) in new[] { ("TLC_817t", "CORE_CS1_130"), ("TLC_817t2", "JAIL_940"), ("TLC_513", "TLC_518"), ("TLC_631", "DS1_185") }) {
            var g = Fixture(); QuestValueTests.Install(g, quest); Board(g, false, "CS2_200");
            Entity.FromCard(g.Player1, Cards.FromId(card), zone: g.Player1.HandZone);
            var master = Master(true);
            var starved = new TurnPlannerJob(View(g), master, DeckPlans.DragonWarrior, 71, 3000, null); Run(starved);
            var tier = new TurnPlannerJob(View(g), master, DeckPlans.DragonWarrior, 71, 7000, null); Run(tier);
            Check(starved.DiagnosticReplyK < 8 && starved.DiagnosticFinalists >= 2, $"{quest}: replies at reduced samples K{starved.DiagnosticReplyK} x{starved.DiagnosticFinalists}");
            Check(starved.Result.Line[0].Equals(tier.Result.Line[0]), $"{quest}: {starved.Result.Line[0]} vs tier {tier.Result.Line[0]}");
        }
    }

    public static void TestNoReplyWithoutBudget()
    {
        foreach (int cap in new[] { 100, 300, 561, 1000, 1500 }) {
            var on = Plan(Elite(true), cap); var off = Plan(Elite(false), cap);
            Check(on.DiagnosticFinalists == 0 || cap >= 1500, $"cap {cap} cannot pay two replies: {on.DiagnosticFinalists}");
            Check(on.Result.Line.Count > 0, $"cap {cap} still decides");
            Check(on.DiagnosticFinalists > 0 || string.Join(";", on.Result.Line) == string.Join(";", off.Result.Line), $"cap {cap} static choice moved");
        }
    }

    public static void TestCapIsNeverExceeded()
    {
        foreach (var profile in new[] { Elite(true), Master(true) })
            foreach (int cap in new[] { 100, 561, 1000, 1469, 1748, 1749, 2000, 2400, 3000, 3422, 3423, 3424, 4000, 5033, 5776, 5777, 6078, 6731, 6732, 7000, 10096, 10097, 16190 }) {
                var job = Plan(profile, Math.Min(cap, profile.Budget.PlanNodes));
                Check(job.Result.Nodes <= Math.Min(cap, profile.Budget.PlanNodes), $"{profile.Budget.ReplyK} cap {cap}: {job.Result.Nodes} nodes");
                Check(job.DiagnosticFinalists <= profile.Budget.ReplyL + 1, $"{profile.Budget.ReplyK} cap {cap}: {job.DiagnosticFinalists} finalists");
            }
    }

    public static void TestTierBudgetUnchanged()
    {
        // A cap that pays the tier's finalists searches exactly as before: flags OFF and ON are one path.
        Check(Fingerprint(Plan(Elite(true), 70000)) == Fingerprint(Plan(Elite(false), 70000)), "elite full plan changed");
        Check(Fingerprint(Plan(Elite(true), 30284)) == Fingerprint(Plan(Elite(false), 30284)), "elite at the L=9 threshold changed");
        foreach (int cap in new[] { 5777, 6500, 7000 })
            Check(Fingerprint(Plan(Master(true), cap)) == Fingerprint(Plan(Master(false), cap)), "master at " + cap + " changed");
        // Below master's threshold the flag is the only difference, and it is visible.
        Check(Plan(Master(false), 3000).DiagnosticFinalists <= 1 && Plan(Master(true), 3000).DiagnosticFinalists >= 2, "master starved replan");
    }

    public static void TestPassValidationAndNoReplyVariantsUntouched()
    {
        foreach (int cap in new[] { 3000, 7000 })
            Check(Fingerprint(Plan(Elite(true), cap, true)) == Fingerprint(Plan(Elite(false), cap, true)), "pass validation keeps full reply strength at " + cap);
        // Mistake pricing compares two forced-first-action arms: samples stay the tier's, whatever each arm's beam left.
        var game = Position(); var root = View(game); var forced = root.Actions.First(a => a.Type != PlayerTaskType.END_TURN);
        foreach (int cap in new[] { 3000, 6078 }) {
            var on = TurnPlanner.Job(View(game), Elite(true), DeckPlans.DragonWarrior, 71, cap, firstAction: forced); Run(on);
            var off = TurnPlanner.Job(View(game), Elite(false), DeckPlans.DragonWarrior, 71, cap, firstAction: forced); Run(off);
            Check(Fingerprint(on) == Fingerprint(off), "pricing arm changed at " + cap);
        }
        // IgnoreAoeRisk's variant (SkilledPilot.Tune) plans with ReplyL = 0: it must stay blind to replies however many nodes are spare.
        var p = Elite(true); var e = p.Budget;
        var blind = new NpcProfile(p.Id, p.Version, p.DisplayName, p.DeckId, p.HeroSkin, p.CardBack, p.Personality,
            new Budget(e.TurnNodes, e.PlanNodes, e.LethalNodes, 1, 0, Math.Max(e.ReplyR, 2 * e.PlanNodes + 64), e.OwnBeamWidth, e.Dedupe, 45, 55), p.PlanId, p.HeroSkinPower, p.Fixes);
        foreach (int cap in new[] { 300, 3000, 7000, 70000 }) Check(Plan(blind, cap).DiagnosticFinalists == 0, "no-reply variant replied at " + cap);
    }

    public static void TestSoftmaxTiersAreGated()
    {
        var soft = new AiFixes().ForSoftmax(.6); var exact = new AiFixes().ForSoftmax(0);
        Check(!soft.On("starved-replies") && !soft.On("replan-reserve") && soft.On("plan-reuse"), "a softmax tier keeps the old capped search");
        Check(exact.On("starved-replies") && exact.On("replan-reserve"), "a noiseless tier gets the fixes");
        Check(new AiFixes(new[] { "starved-replies" }).ForSoftmax(0).Disabled.SequenceEqual(new[] { "starved-replies" }), "explicit OFF arm kept");
        var p = Elite(true); var gated = p.WithFixes(p.Fixes.ForSoftmax(.6));
        Check(Fingerprint(Plan(gated, 3000)) == Fingerprint(Plan(Elite(false), 3000)), "a gated tier plans like OFF");
    }

    public static void TestReusedPlanAgreesWithFresh()
    {
        // Deterministic attacks from a starved replan: the next decision reuses the plan and a fresh starved plan agrees.
        foreach (int cap in new[] { 2400, 3423, 6078 }) {
            var g = Fixture(); g.Turn = 9; g.Player1.UsedMana = 10;
            Board(g, true, "CS2_120", true); Board(g, true, "CS2_182", true);
            var profile = Elite(true);
            var pilot = new GoldenPilot(profile);
            GoldenDecision Decide(GoldenPilot p, int index)
            {
                var view = p.Options.FairViewFactory(g, 1, unchecked(997 * g.Turn + index * 31), DeckStyle.Tempo);
                var job = p.Job(view, new GoldenContext(g.Turn, index, unchecked(997 * g.Turn + index * 31), p.Profile, p.PublicInputs));
                job.CapPlanNodes(() => cap);
                for (int i = 0; !job.IsDone && i < 200000; i++) job.StepNode();
                Check(job.IsDone, "decision did not finish");
                return job.Result;
            }
            var first = Decide(pilot, 1);
            Check(!first.SearchInfo.Reused && pilot.LastLine.Count >= 2, "a multi-step plan at cap " + cap);
            Check(g.Process(first.Action.Resolve(g.Player1.Options())), "action rejected");
            var fresh = new GoldenPilot(profile);
            var freshChoice = Decide(fresh, 2);
            var reused = Decide(pilot, 2);
            Check(reused.SearchInfo.Reused, $"cap {cap}: replan on a deterministic step: {reused.SearchInfo.Replan}");
            Check(reused.Action.Equals(freshChoice.Action), $"cap {cap}: reused {reused.Action}, fresh {freshChoice.Action}");
        }
    }
}
