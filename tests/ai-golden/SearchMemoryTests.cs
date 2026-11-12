using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
using static ReplyTests;

// Own-beam lines release their game clones once scored (docs/roadmap/2026-10-28-elite-search-memory.md): same search,
// bounded retained states.
public static class SearchMemoryTests
{
    private static readonly BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic;
    // A wide late-turn position: full own hand and board, a wide enemy board, 10 mana.
    private static Game Wide()
    {
        var g = Fixture();
        foreach (var id in new[] { "CS2_182", "CS2_120", "CS2_119", "CS2_231", "CS2_121", "CS2_200", "CS2_182" }) Board(g, true, id, true);
        foreach (var id in new[] { "CS2_120", "CS2_119", "CS2_231", "CS2_121", "CS2_200", "CS2_182" }) Board(g, false, id);
        foreach (var id in new[] { "CS2_182", "CS2_120", "CS2_119", "CS2_231", "CS2_121", "CS2_200", "CS2_029", "CS2_032", "EX1_559", "CS2_182" })
            SabberStoneCore.Model.Entities.Entity.FromCard(g.Player1, Cards.FromId(id), zone: g.Player1.HandZone);
        return g;
    }
    private static NpcProfile Elite(NpcProfile p) => new NpcProfile(p.Id, p.Version, p.DisplayName, p.DeckId, p.HeroSkin, p.CardBack, p.Personality,
        new Budget(280000, 6000, 4000, 4, 3, 160, 25, dedupe: true), p.PlanId, p.HeroSkinPower);
    private static int Held(TurnPlannerJob job)
    {
        var beam = (TurnBeam)typeof(TurnPlannerJob).GetField("beam", Any).GetValue(job);
        var lines = new HashSet<TurnLine>(((List<TurnLine>)typeof(TurnBeam).GetField("next", Any).GetValue(beam))
            .Concat((List<TurnLine>)typeof(TurnBeam).GetField("frontier", Any).GetValue(beam)).Concat(beam.Leaves));
        return lines.Count(l => l.Holds);
    }
    private static (string Result, int Peak, int Nodes) Search(bool hold)
    {
        GoldenAblation.HoldLineStates = hold;
        try {
            var job = TurnPlanner.Job(View(Wide()), Elite(NpcCatalog.All[0]), DeckPlans.DragonWarrior, 71);
            int peak = 0;
            while (!job.IsDone) { job.StepNode(); peak = Math.Max(peak, Held(job)); }
            // Replaying released finalists now consumes nodes; compare decisions separately from that work.
            return (string.Join(";", job.Result.Line) + "|" + job.Result.Value.ToString("R") + "|" + job.Result.Stop
                + "|" + string.Join(",", job.Result.Candidates.Select(c => c.Key + "=" + c.Value.ToString("R"))), peak, job.Result.Nodes);
        }
        finally { GoldenAblation.HoldLineStates = false; }
    }
    public static void TestReleasedLinesSearchIdenticallyWithBoundedStates()
    {
        var held = Search(true); var released = Search(false);
        Check(released.Result == held.Result, "released own-beam lines must not change the plan: " + released.Result + " vs " + held.Result);
        Check(released.Nodes > held.Nodes && released.Nodes <= held.Nodes + 4 * TurnBeam.MaxLineActions,
            "released finalists must charge their bounded replay work: " + released.Nodes + " vs " + held.Nodes);
        // Frontier (25) + its END_TURN leaves + the dedupe-keyed prefix of one depth, never one clone per scored child.
        Check(released.Peak <= 4 * 25 && held.Peak > 4 * 25, $"retained clones held {held.Peak}, released {released.Peak}");
    }
    public static void TestReleasedLineRebuildsTheSameState()
    {
        var root = View(Wide());
        var origin = new TurnLine { State = root }; _ = root.OrderedActions; origin.Expanded = true;
        foreach (var action in root.OrderedActions.Take(12)) {
            var child = root.Apply(action);
            var line = new TurnLine { State = child, Origin = origin, Actions = new List<BotAction> { action } };
            string key = GoldenPublicKey.For(child), effects = child.OwnVisibleEffectsKey();
            line.Release();
            Check(!line.Holds && line.EffectsKey == effects, "release keeps summaries without the clone");
            Check(GoldenPublicKey.For(line.State) == key && line.Holds, "rebuilt clone equals the scored child: " + action);
        }
    }
    // Fix Wo's offers to Ancestral Healing: expanding the real Past pool must not remove this cap regression.
    private static Game FreeSpellLoop()
    {
        var g = Fixture(); g.Player1.UsedMana = 9;
        var wo = Cards.FromId("TIME_013").Clone();
        wo.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_CAST) {
            TriggerSource = TriggerSource.FRIENDLY,
            SingleTask = new CustomTask((game, owner, source, target, stack) =>
                new EffectContext(game, owner.Id, source.Id).Discover(new[] { Cards.FromId("CS2_041") }))
        } };
        var body = (Minion)Entity.FromCard(g.Player1, wo, zone: g.Player1.BoardZone);
        body.IsExhausted = false; g.SummonedMinions.Clear();
        Board(g, false, "CS2_120");
        Entity.FromCard(g.Player1, Cards.FromId("CS2_041"), zone: g.Player1.HandZone);
        return g;
    }
    // Farseer Wo + a free Nature spell is an endless in-turn loop (game 741 decision 97).
    public static void TestFreeSpellLoopStopsAtTheLineCap()
    {
        foreach (int seed in new[] { 71, 72, 73 }) {
            var g = FreeSpellLoop();
            var job = TurnPlanner.Job(View(g), Elite(NpcCatalog.All[0]), DeckPlans.DragonWarrior, seed);
            Run(job);
            var beam = (TurnBeam)typeof(TurnPlannerJob).GetField("beam", Any).GetValue(job);
            int deepest = beam.Leaves.Max(l => l.Actions.Count);
            Check(deepest <= TurnBeam.MaxLineActions, "line exceeded the cap: " + deepest);
            if (deepest == TurnBeam.MaxLineActions) {
                Check(job.Result.Stop == SearchStop.Exhausted, "capped loop lines end the search early, not at the node cap: " + job.Result.Stop);
                return;
            }
        }
        throw new Exception("fixture no longer loops: no seed reached the line cap");
    }
    // A finalist three released levels deep rebuilds through its chain, re-reading options where the original expanded.
    public static void TestReleasedChainRebuildsTheSameState()
    {
        var root = View(Wide());
        var lines = new List<TurnLine> { new TurnLine { State = root } };
        var states = new List<FairBotState> { root };
        for (int depth = 0; depth < 3; depth++) {
            var parent = lines[depth]; var at = states[depth];
            var actions = at.OrderedActions; parent.Expanded = true;
            var action = actions.First(a => a.Type != SabberStoneCore.Tasks.PlayerTasks.PlayerTaskType.END_TURN);
            var child = at.Apply(action);
            lines.Add(new TurnLine { State = child, Origin = parent, Actions = parent.Actions.Concat(new[] { action }).ToList() });
            states.Add(child);
        }
        string key = GoldenPublicKey.For(states[3]);
        var next = states[3].OrderedActions.First(a => a.Type != SabberStoneCore.Tasks.PlayerTasks.PlayerTaskType.END_TURN);
        string after = GoldenPublicKey.For(states[3].Apply(next));
        for (int depth = 3; depth >= 1; depth--) lines[depth].Release();
        Check(!lines[1].Holds && !lines[2].Holds && !lines[3].Holds, "chain released");
        int steps = 1; while (!lines[3].RebuildStep()) steps++;
        Check(steps == 3 && !lines[1].Holds && !lines[2].Holds, "one Apply per rebuild step, ancestors stay released: " + steps);
        Check(GoldenPublicKey.For(lines[3].State) == key && GoldenPublicKey.For(lines[3].State.Apply(next)) == after,
            "rebuilt deep line equals the scored one and plays on identically");
    }
    // The lethal DFS stops at the same line cap in the Farseer Wo loop instead of holding a state per looped action.
    public static void TestLethalSearchStopsAtTheLineCap()
    {
        var g = FreeSpellLoop(); g.Player2.Hero.BaseHealth = 200; g.Player2.Hero.Damage = 0;
        var job = new LethalJob(View(g), 20000);
        var frontier = (System.Collections.ICollection)typeof(LethalJob).GetField("frontier", Any).GetValue(job);
        int deepest = 0;
        while (!job.IsDone) {
            job.StepNode();
            foreach (var node in frontier) deepest = Math.Max(deepest, (int)node.GetType().GetField("Depth", Any).GetValue(node));
        }
        // The loop drives the DFS to the cap and no further (a pending node sits one action past its expanded parent).
        Check(deepest == TurnBeam.MaxLineActions, "lethal DFS depth should stop at the line cap: " + deepest);
        Check(job.Result.Outcome == LethalOutcome.Incomplete, "a capped loop is not a proof of no lethal: " + job.Result.Outcome);
    }
}
