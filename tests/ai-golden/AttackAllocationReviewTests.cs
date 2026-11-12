using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static ReplyTests;

public static class AttackAllocationReviewTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Set(object value, string field, object data) => value.GetType().GetField(field, Fields).SetValue(value, data);
    private static T Get<T>(object value, string field) => (T)value.GetType().GetField(field, Fields).GetValue(value);

    private static IPlayable Hand(Game g, string id) => Entity.FromCard(g.Player1, Cards.FromId(id), zone: g.Player1.HandZone);

    public static void TestPartialCombatKeepsReservedFrontierSlots()
    {
        var g = Fixture(); Board(g, false, "CS2_120"); Hand(g, "CS2_231");
        var root = View(g);
        var beam = new TurnBeam(root, s => s.Evaluate(), 1000, 16, false, attackAllocation: true);
        var ranked = Enumerable.Range(0, 40).Select(i => {
            var line = new TurnLine { State = root, Value = 100 - i };
            Set(line, "effectsKey", "effect-" + (i < 5 ? i : 0));
            Set(line, "survivorsKey", "survivor-" + (i < 8 ? i : 0));
            Set(line, "attacksKey", "attack-" + (i < 10 ? i : 0));
            Set(line, "combatKey", i == 38 ? "partial-damage" : i == 39 ? "shield-popped" : "same");
            return line;
        }).ToList();
        Set(beam, "pendingScore", null); Set(beam, "next", ranked); Set(beam, "parent", 1);
        beam.StepNode();
        var frontier = Get<List<TurnLine>>(beam, "frontier");
        Check(frontier.Count == 16, "fixed width must not grow");
        Check(frontier.Contains(ranked[38]) && frontier.Contains(ranked[39]), "damage/shield diversity survives effects, kills and attack resource groups");
        Check(frontier.Take(10).All(n => ranked.IndexOf(n) < 10), "earlier protection keeps its reserved share");
    }

    public static void TestAttachedFutureDiscountKeepsHandIdentity()
    {
        var g = Fixture();
        var carver = Board(g, true, "CATA_566");
        var plain = Hand(g, "CS2_120"); var carved = Hand(g, "CS2_120");
        Generic.AddEnchantmentBlock(g, Cards.FromId("CATA_566e"), carver, carved);
        var root = View(g); var kinds = TurnPlannerJob.HandKinds(root.Facts);
        Check(plain.Cost == carved.Cost, "future discount has no current cost difference");
        var playPlain = root.Apply(root.Actions.First(a => a.Source == plain.Id));
        var playCarved = root.Apply(root.Actions.First(a => a.Source == carved.Id));
        Check(playPlain.OwnVisibleEffectsKey() == playCarved.OwnVisibleEffectsKey(), "global effects key alone cannot distinguish host zone");
        Check(TurnPlannerJob.HandCommitment(playPlain.Facts.Own.Hand.Select(c => c.EntityId), kinds)
            != TurnPlannerJob.HandCommitment(playCarved.Facts.Own.Hand.Select(c => c.EntityId), kinds), "attached future cost trigger preserves host identity");
        var gamePlain = Get<Game>(playPlain, "game").CloneExact(); var gameCarved = Get<Game>(playCarved, "game").CloneExact();
        foreach (var game in new[] { gamePlain, gameCarved }) {
            game.Process(EndTurnTask.Any(game.CurrentPlayer)); game.Process(EndTurnTask.Any(game.CurrentPlayer));
        }
        Check(gamePlain.Player1.HandZone.Single(c => c.Id == carved.Id).Cost
            == gameCarved.Player1.HandZone.Single(c => c.Id == plain.Id).Cost - 1, "Carver really discounts the retained copy next turn");
    }

    public static void TestInSearchEnchantmentUsesLeafHandCommitment()
    {
        var g = Fixture(); Board(g, false, "CS2_120");
        var carver = Hand(g, "CATA_566");
        var a = Hand(g, "CS2_120"); var b = Hand(g, "CS2_120");
        var root = View(g); var kinds = TurnPlannerJob.HandKinds(root.Facts);
        Check(kinds[a.Id] == kinds[b.Id], "copies are equivalent at the turn root");
        var play = root.Actions.First(x => x.Source == carver.Id);
        var pending = root.Apply(play);
        var carve = pending.Actions.First(x => x.Type == PlayerTaskType.CHOOSE && x.ToString().EndsWith(":" + a.Id));
        var carved = pending.Apply(carve);
        Check(carved.Facts.Own.Hand.Single(c => c.EntityId == a.Id).HasAttachedEffects,
            "Carver attaches its trigger during the searched line");
        var origin = new TurnLine { State = carved, Actions = new List<BotAction> { play, carve } };
        TurnLine Leaf(int id) {
            var action = carved.Actions.First(x => x.Source == id);
            return new TurnLine { State = carved.Apply(action), Origin = origin,
                Actions = origin.Actions.Concat(new[] { action }).ToList() };
        }
        var playedA = Leaf(a.Id); var playedB = Leaf(b.Id);
        Check(playedA.EffectsKey == playedB.EffectsKey, "global effects omit host-zone movement");
        Check(TurnPlannerJob.HandCommitment(playedA.HandIds, kinds) == TurnPlannerJob.HandCommitment(playedB.HandIds, kinds),
            "root hand signatures reproduce the reviewed bug");
        playedA.Release(keepCombatKey: true); playedB.Release(keepCombatKey: true);
        Check(playedA.HandCommitment != playedB.HandCommitment, "leaf commitments preserve the retained future discount");
        Check(!playedA.Holds && !playedB.Holds && playedA.RebuildNodes == 1 && playedB.RebuildNodes == 1,
            "commitment reads never rebuild a released leaf");
        // Saturate the normal slots: root-based grouping skips playedB for lower-ranked commitments.
        playedA.Value = 100; playedB.Value = 90;
        var playBoth = playedA.State.Actions.First(x => x.Source == b.Id);
        var empty = new TurnLine { State = playedA.State.Apply(playBoth), Value = 80,
            Actions = playedA.Actions.Concat(new[] { playBoth }).ToList() };
        origin.Value = 70;
        var job = TurnPlanner.Job(root, NpcCatalog.All[0], DeckPlans.DragonWarrior, 71, 10000);
        var beam = Get<TurnBeam>(job, "beam"); Set(beam, "<IsDone>k__BackingField", true);
        beam.Leaves.Clear(); beam.Leaves.AddRange(new[] { playedA, playedB, empty, origin });
        job.StepNode();
        Check(Get<List<TurnLine>>(job, "leaves").Contains(playedB),
            "finalist selection uses leaf commitments, preserving the retained enchanted copy");
        var gameA = Get<Game>(playedA.State, "game").CloneExact();
        var gameB = Get<Game>(playedB.State, "game").CloneExact();
        foreach (var game in new[] { gameA, gameB }) {
            game.Process(EndTurnTask.Any(game.CurrentPlayer)); game.Process(EndTurnTask.Any(game.CurrentPlayer));
        }
        Check(gameB.Player1.HandZone.Single(c => c.Id == a.Id).Cost == gameA.Player1.HandZone.Single(c => c.Id == b.Id).Cost - 1,
            "only the retained carved copy receives next-turn discount");
    }

    public static void TestMaskedDrawsKeepLeafHandCommitment()
    {
        var g = Fixture(); Board(g, false, "CS2_120");
        var draw = Hand(g, "CS2_023"); Hand(g, "CS2_120");
        // Opponent-generated cards in our deck stay hidden from us, so both draws are masked.
        foreach (var card in g.Player1.DeckZone.ToArray()) { g.Player1.DeckZone.Remove(card); g.Player1.SetasideZone.Add(card); }
        for (int i = 0; i < 2; i++) g.Player1.DeckZone.AddGenerated(Cards.FromId("CS2_182"), g.Player2.Hero);
        var root = View(g);
        var play = root.Actions.First(a => a.Source == draw.Id);
        var drawn = root.Apply(play);
        Check(drawn.Facts.Own.Hand.Count(c => c.Unknown) >= 2, "fixture draws two masked cards (all EntityId 0)");
        var line = new TurnLine { State = drawn, Origin = new TurnLine { State = root }, Actions = new List<BotAction> { play } };
        line.Release(keepCombatKey: true);
        Check(line.HandCommitment.Split(',').Count(k => k == "<new>") >= 2, "masked draws share an unknown commitment");
    }

    public static void TestOnlyInterceptingTauntsGetExtraOutcomeCoverage()
    {
        var g = Fixture(); Board(g, false, "CS2_120");
        var taunt = Board(g, true, "CS2_121", true); taunt.HasTaunt = true; taunt.HasStealth = true;
        var hidden = View(g);
        var attack = hidden.Actions.First(a => a.Source == taunt.Id && a.Target == g.Player2.Hero.Id);
        var exposed = hidden.Apply(attack);
        Check(!exposed.Facts.Own.Board.Single().Stealth, "attacking exposes the same Taunt without changing its stats");
        var hiddenLine = new TurnLine { State = hidden, Value = 100 };
        var exposedLine = new TurnLine { State = exposed, Actions = new List<BotAction> { attack }, Value = 0 };
        Check(hiddenLine.CombatOutcomeKey != exposedLine.CombatOutcomeKey, "Stealth Taunt does not intercept attacks");
        var job = TurnPlanner.Job(hidden, NpcCatalog.All[0], DeckPlans.DragonWarrior, 71, 10000, comparePass: true);
        var beam = Get<TurnBeam>(job, "beam"); Set(beam, "<IsDone>k__BackingField", true);
        beam.Leaves.Clear();
        int normal = Get<int>(job, "normalReplyLimit");
        beam.Leaves.Add(hiddenLine);
        for (int i = 0; i < normal; i++) beam.Leaves.Add(new TurnLine {
            State = hidden, Actions = new List<BotAction> { attack }, Value = 90 - i });
        beam.Leaves.Add(exposedLine); job.StepNode();
        var finalists = Get<List<TurnLine>>(job, "leaves");
        Check(finalists[0] == hiddenLine && finalists.Contains(exposedLine) && finalists.Count == normal + 1,
            "pass stays first; exposed blocker receives the optional slot after normal commitments");
        taunt.HasStealth = false; taunt.IsImmune = true;
        Check(new TurnLine { State = View(g) }.CombatOutcomeKey == hiddenLine.CombatOutcomeKey,
            "Immune Taunts do not intercept attacks");
        taunt.IsImmune = false;
        taunt[SabberStoneCore.Enums.GameTag.DORMANT] = 2;
        Check(new TurnLine { State = View(g) }.CombatOutcomeKey == hiddenLine.CombatOutcomeKey,
            "Untouchable Taunts do not intercept attacks");
    }

    public static void TestSpellOnlyDiscoverKeepsExistingBeamCoverage()
    {
        var g = Fixture(); Board(g, false, "CS2_120"); Board(g, false, "CS2_182");
        Hand(g, "JAIL_319"); Hand(g, "CS2_029");
        var root = View(g);
        string Search(bool enabled) {
            var beam = new TurnBeam(root, state => state.Evaluate(), 2000, 16, false, attackAllocation: enabled);
            Run(beam);
            return string.Join(";", beam.ScoredLeaves.OrderBy(n => n.Key, StringComparer.Ordinal).Select(n => n.Key + "=" + n.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
        }
        Check(Search(false) == Search(true), "spell-only Skeleton Key branches must keep existing coverage until legal attacks exist");
    }

    private static TurnLine Chain(FairBotState root)
    {
        var lines = new List<TurnLine> { new TurnLine { State = root } };
        for (int i = 0; i < 3; i++) {
            var parent = lines.Last(); var action = parent.State.OrderedActions.First(a => a.Type == PlayerTaskType.PLAY_CARD);
            lines.Add(new TurnLine { State = parent.State.Apply(action), Origin = parent,
                Actions = parent.Actions.Concat(new[] { action }).ToList(), Value = 10 });
        }
        for (int i = 3; i > 0; i--) lines[i].Release();
        return lines.Last();
    }
    public static void TestFinalistReplaysAreChargedAndInterruptible()
    {
        var g = Fixture(); for (int i = 0; i < 4; i++) Hand(g, "CS2_231");
        var root = View(g); var line = Chain(root);
        var job = TurnPlanner.Job(root, NpcCatalog.All[0], DeckPlans.DragonWarrior, 71, 10000);
        var beam = Get<TurnBeam>(job, "beam"); Set(beam, "<IsDone>k__BackingField", true);
        beam.Leaves.Add(line); Set(job, "leaves", new List<TurnLine> { line }); Set(job, "steps", 9998);
        job.StepNode();
        Check(!job.IsDone && !line.Holds && line.RebuildNodes == 2, "one replay per budgeted step");
        Check(Get<ReplyJob>(job, "reply") == null, "reply cannot start during replay");
        job.StepNode();
        Check(job.IsDone && job.Result.Nodes == 10000 && !line.Holds && line.RebuildNodes == 1,
            "budget stop returns prefix without synchronously rebuilding the rest");
    }
    public static void TestExtraFinalistNeedsAdmissionBudget()
    {
        var g = Fixture(); Board(g, false, "CS2_120"); for (int i = 0; i < 4; i++) Hand(g, "CS2_231");
        var root = View(g); var line = Chain(root);
        var job = TurnPlanner.Job(root, NpcCatalog.All[0], DeckPlans.DragonWarrior, 71, 10000);
        var beam = Get<TurnBeam>(job, "beam"); Set(beam, "<IsDone>k__BackingField", true); beam.Leaves.Add(line);
        int normal = Get<int>(job, "normalReplyLimit");
        Set(job, "leaves", Enumerable.Repeat(line, normal + 1).ToList()); Set(job, "index", normal); Set(job, "steps", 9998);
        job.StepNode();
        Check(job.IsDone && !line.Holds && line.RebuildNodes == 3 && Get<ReplyJob>(job, "reply") == null,
            "unreserved extra finalist cannot replay or start a reply");
    }

    // Review round 3. Two Whirlwinds stay playable after the best Wisp/Wisp/Armor Up turn, so the own beam runs two depths
    // past that complete leaf and releases its clone (frontier leaves from depth <= d-2 are always released).
    private static FairBotState DeepAfterBest(bool enemy)
    {
        var g = Fixture(); if (enemy) Board(g, false, "CS2_182");
        Hand(g, "CS2_231"); Hand(g, "CS2_231"); Hand(g, "EX1_400"); Hand(g, "EX1_400");
        return View(g);
    }
    private static TurnLine Best(TurnPlannerJob job) => TurnLine.Rank((IEnumerable<TurnLine>)typeof(TurnPlannerJob)
        .GetMethod("Candidates", Fields).Invoke(job, null)).FirstOrDefault();
    private static void EndsLike(PlannedTurn result, TurnLine best, string why) =>
        Check(result.Line.Count == best.Actions.Count + 1 && result.Line.Take(best.Actions.Count).SequenceEqual(best.Actions)
            && result.Line[result.Line.Count - 1].Type == PlayerTaskType.END_TURN,
            why + ": " + string.Join(";", result.Line) + " best=" + best.Key);
    public static void TestReleasedBestKeepsEndTurnAtReplyLimitZero()
    {
        var root = DeepAfterBest(false);
        PlannedTurn Search(bool allocation) {
            var profile = allocation ? NpcCatalog.All[0] : NpcCatalog.All[0].WithFixes(new AiFixes(new[] { "attack-allocation" }));
            // A late-turn live cap below ~1,960 nodes leaves no reply finalists (as do NoReply and CapPlanNodes).
            var job = TurnPlanner.Job(root, profile, DeckPlans.DragonWarrior, 71, 1500);
            Check(Get<int>(job, "replyLimit") == 0, "late live cap leaves no reply finalists");
            var beam = Get<TurnBeam>(job, "beam");
            while (!beam.IsDone) job.StepNode();
            var best = Best(job);
            Check(best != null && best.Completed && !best.Holds, "fixture: best complete leaf released its clone");
            Run(job);
            EndsLike(job.Result, best, "replyLimit 0 keeps END_TURN on a released complete best, allocation=" + allocation);
            return job.Result;
        }
        var on = Search(true); var off = Search(false);
        Check(on.Line.SequenceEqual(off.Line) && on.Value == off.Value, "without enemy minions ON equals OFF");
    }
    public static void TestWallCutDuringOwnBeamKeepsReleasedBestEndTurn()
    {
        var root = DeepAfterBest(true);
        var job = TurnPlanner.Job(root, NpcCatalog.All[0], DeckPlans.DragonWarrior, 71, 28000);
        Check(Get<int>(job, "replyLimit") > Get<int>(job, "normalReplyLimit"), "full cap scores replies plus the combat slot");
        var beam = Get<TurnBeam>(job, "beam");
        TurnLine best = null;
        while (!beam.IsDone) {
            job.StepNode(); best = Best(job);
            if (best != null && best.Completed && !best.Holds) break;
        }
        Check(!beam.IsDone && best != null && best.Completed && !best.Holds,
            "fixture: the wall arrives mid-beam while the best complete leaf is released");
        EndsLike(job.StopSearch(), best, "wall cut keeps END_TURN on a released complete best");
    }
    public static void TestFailedExtraAdmissionNeverBecomesSafeEscape()
    {
        var g = Fixture(); Board(g, false, "CS2_120"); for (int i = 0; i < 4; i++) Hand(g, "CS2_231");
        var root = View(g); var extra = Chain(root); extra.PublicBoardSafe = true;
        var job = TurnPlanner.Job(root, NpcCatalog.All[0], DeckPlans.DragonWarrior, 71, 28000);
        int normal = Get<int>(job, "normalReplyLimit");
        Check(Get<int>(job, "replyLimit") == normal + 1, "fixture has the optional combat slot");
        var beam = Get<TurnBeam>(job, "beam"); Set(beam, "<IsDone>k__BackingField", true); beam.Leaves.Add(extra);
        var power = root.Actions.First(a => a.Type == PlayerTaskType.HERO_POWER);
        // Every normal finalist completed its reply and loses (Survival 0).
        var priced = Enumerable.Range(0, normal).Select(i => new TurnLine { State = root, Actions = new List<BotAction> { power },
            Value = -1000000 + i, Survival = 0 }).ToList();
        beam.Leaves.AddRange(priced); Get<List<TurnLine>>(job, "scored").AddRange(priced);
        Set(job, "leaves", priced.Concat(new[] { extra }).ToList()); Set(job, "index", normal); Set(job, "steps", 27998);
        job.StepNode();
        Check(job.IsDone && Get<ReplyJob>(job, "reply") == null, "extra finalist fails admission");
        // Since 4a1a88d4 (reply-terminal) every unchecked public-board-safe beam leaf may escape when all priced replies
        // lose, so the extra competes only as an ordinary beam leaf, exactly as it would with the flag OFF.
        Check(job.Result.Line.Count > 0 && job.Result.Line.Take(extra.Actions.Count).SequenceEqual(extra.Actions),
            "safe-escape rule picks the unchecked safe beam leaf: " + string.Join(";", job.Result.Line));
        Check(!Get<List<TurnLine>>(job, "leaves").Contains(extra), "unpriced extra finalist leaves the finalist list");
    }
}
