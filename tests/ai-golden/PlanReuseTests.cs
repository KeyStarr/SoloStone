using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static ReplyTests;

// Plan once per turn, then continue the cached line while the real public state equals the plan's prediction
// (GoldenPilot.Reuse); replan on random outcomes, divergence, an illegal next step. Live 2026-11-01 journals never
// reused: every decision's artificial seed resampled the own-deck prior that the reuse key used to contain.
public static class PlanReuseTests
{
    private static GoldenPilot Pilot(params string[] off) => new GoldenPilot(NpcCatalog.All[0].WithFixes(new AiFixes(off)));
    private static string Key(FairBotState view) => GoldenPilot.WithoutDeckPrior(GoldenPublicKey.For(view), view.Facts);
    // Test-only: the same public state under a different sampled own-deck prior (what the next decision's seed produces
    // once the own deck has unidentified slots).
    private static FairBotState Resampled(FairBotState view)
    {
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var prior = new Dictionary<string, int>(view.Facts.OwnDeckKnowledge, StringComparer.Ordinal);
        Check(prior.Count > 0, "fixture: the golden view tracks the own deck");
        var first = prior.Keys.OrderBy(k => k, StringComparer.Ordinal).First();
        if (prior[first] > 1) prior[first]--; else prior.Remove(first);
        prior["CS2_029"] = prior.TryGetValue("CS2_029", out int n) ? n + 1 : 1;
        typeof(FairBotState).GetField("ownDeckKnowledge", flags).SetValue(view, new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(prior));
        typeof(FairBotState).GetField("facts", flags).SetValue(view, null);
        return view;
    }
    // Live 2026-11-05 (ead250ca #96): after Bashana the plan's rolled Treants matched the real ones, with other spells inside.
    public static void TestCarvedSpellIdentityJoinsTheReuseKey()
    {
        Game Carved(int seed)
        {
            var g = Fixture(CardClass.DRUID, CardClass.MAGE); g.Player1.BaseMana = 10; g.Player1.UsedMana = 0;
            var bashana = Entity.FromCard(g.Player1, Cards.FromId("MEND_046"), zone: g.Player1.HandZone);
            g.SetRandomSeed(seed);
            Check(g.Process(g.Player1.Options().First(t => t.Source?.Id == bashana.Id)), "Bashana rejected");
            return g;
        }
        string Spells(Game g) => string.Join(",", g.Player1.HandZone.Where(c => c.Card.Id == "MEND_046t").Select(c => g.IdEntityDic[c[GameTag.TAG_SCRIPT_DATA_NUM_1]].Card.Id));
        string Plan(GoldenPilot pilot, Game g) => pilot.PlanKey(pilot.Options.FairViewFactory(g, 1, 71, DeckStyle.Tempo));
        var on = Pilot(); var off = Pilot("carried-reuse");
        // Worlds whose generated cards allocate the same ids: their keys matched although the spells differ, the bug's shape.
        var twins = Enumerable.Range(1, 60).Select(Carved).GroupBy(g => Plan(off, g)).Select(k => k.GroupBy(Spells).Select(x => x.First()).ToArray()).First(k => k.Length > 1);
        Check(Plan(on, twins[0]) != Plan(on, twins[1]), "different carved spells must not share a plan key");
    }
    // The hit path: the same carve in the plan's child and the next real view still reuses, so the key never disables reuse for Treant decks.
    public static void TestCarvedTreantsInHandStillReuse()
    {
        var g = Fixture(CardClass.DRUID, CardClass.MAGE); g.Turn = 9; g.Player1.BaseMana = 10; g.Player1.UsedMana = 0;
        var bashana = Entity.FromCard(g.Player1, Cards.FromId("MEND_046"), zone: g.Player1.HandZone);
        Check(g.Process(g.Player1.Options().First(t => t.Source?.Id == bashana.Id)), "Bashana rejected");
        Check(g.Player1.HandZone.Any(c => c.Card.Id == "MEND_046t"), "fixture: carved Treants in hand");
        // No mana left: the line is deterministic attacks, the Treants stay in hand and in the key.
        g.Player1.UsedMana = 10;
        Board(g, true, "CS2_120", true); Board(g, true, "CS2_182", true);
        var pilot = Pilot();
        var first = Decide(pilot, g, 1);
        Check(pilot.LastLine.Count >= 3, "a multi-step line: " + string.Join(",", pilot.LastLine));
        Act(g, first.Action);
        var second = Decide(pilot, g, 2);
        Check(second.SearchInfo.Reused && pilot.PlanReused, "same carve must continue the plan; replan=" + second.SearchInfo.Replan);
    }
    private static int Seed(Game g, int index) => unchecked(997 * g.Turn + index * 31);
    private static FairBotState View(GoldenPilot pilot, Game g, int index) => pilot.Options.FairViewFactory(g, 1, Seed(g, index), DeckStyle.Tempo);
    private static GoldenDecisionJob Begin(GoldenPilot pilot, Game g, int index, out FairBotState view)
    {
        view = View(pilot, g, index);
        return pilot.Job(view, new GoldenContext(g.Turn, index, Seed(g, index), pilot.Profile, pilot.PublicInputs));
    }
    private static GoldenDecision Decide(GoldenPilot pilot, Game g, int index)
    {
        var job = Begin(pilot, g, index, out _);
        for (int i = 0; !job.IsDone && i < 2_000_000; i++) job.StepNode();
        Check(job.IsDone, "decision did not finish");
        return job.Result;
    }
    private static void Act(Game g, BotAction action) => Check(g.Process(action.Resolve(g.Player1.Options())), "action rejected: " + action);
    // Two ready attackers, nothing in hand, no mana: every line is deterministic attacks then END_TURN.
    private static Game Attackers()
    {
        var g = Fixture(); g.Turn = 9; g.Player1.UsedMana = 10;
        Board(g, true, "CS2_120", true); Board(g, true, "CS2_182", true);
        return g;
    }

    public static void TestReuseHitAcrossDeterministicActions()
    {
        var g = Attackers(); var pilot = Pilot();
        var first = Decide(pilot, g, 1);
        Check(!first.SearchInfo.Reused && first.SearchInfo.Replan == "turn-start", "first decision plans: " + first.SearchInfo.Replan);
        Check(pilot.LastLine.Count >= 3, "a multi-step line: " + string.Join(",", pilot.LastLine));
        Act(g, first.Action);
        var second = Decide(pilot, g, 2);
        Check(second.SearchInfo.Reused && second.SearchInfo.Replan == "reused" && pilot.PlanReused,
            "deterministic step must continue the plan; replan=" + second.SearchInfo.Replan);
    }

    public static void TestReplanOnDrawOutcome()
    {
        var g = Fixture(); g.Turn = 9; g.Player1.UsedMana = 7;
        var draw = Entity.FromCard(g.Player1, Cards.FromId("CS2_023"), zone: g.Player1.HandZone);   // Arcane Intellect
        Board(g, true, "CS2_120", true);
        var pilot = Pilot();
        var job = Begin(pilot, g, 1, out var view);
        for (int i = 0; !job.IsDone && i < 2_000_000; i++) job.StepNode();
        var face = view.Actions.First(a => a.Type == PlayerTaskType.MINION_ATTACK && a.Target == g.Player2.Hero.Id);
        var cast = view.Actions.First(a => a.Source == draw.Id);
        pilot.Accept(view, new[] { cast, face, view.Actions.First(a => a.Type == PlayerTaskType.END_TURN) }, 0, LethalOutcome.None);
        Act(g, cast);
        Begin(pilot, g, 2, out _);
        Check(pilot.ReplanReason.StartsWith("random-outcome:", StringComparison.Ordinal), "a draw must replan as a random outcome: " + pilot.ReplanReason);
    }

    public static void TestReplanOnRandomDamage()
    {
        // Arcane Missiles into two enemy minions: the real split differs from the planned one for some engine seeds.
        foreach (int seed in Enumerable.Range(1, 12))
        {
            var g = Fixture(); g.Turn = 9; g.Player1.UsedMana = 9; g.SetRandomSeed(seed);
            var missiles = Entity.FromCard(g.Player1, Cards.FromId("EX1_277"), zone: g.Player1.HandZone);
            Board(g, true, "CS2_120", true);
            Board(g, false, "CS2_182"); Board(g, false, "CS2_120");
            var pilot = Pilot();
            var job = Begin(pilot, g, 1, out var view);
            for (int i = 0; !job.IsDone && i < 2_000_000; i++) job.StepNode();
            var cast = view.Actions.First(a => a.Source == missiles.Id);
            var expected = view.Apply(cast);
            pilot.Accept(view, new[] { cast, view.Actions.First(a => a.Type == PlayerTaskType.END_TURN) }, 0, LethalOutcome.None);
            Act(g, cast);
            Begin(pilot, g, 2, out var real);
            if (Key(real) == Key(expected))
            { Check(pilot.ReplanReason == "reused", "matching random outcome continues: " + pilot.ReplanReason); continue; }
            Check(pilot.ReplanReason.StartsWith("random-outcome:", StringComparison.Ordinal), "random damage must replan as random: " + pilot.ReplanReason);
            return;
        }
        throw new Exception("no seed produced a different Arcane Missiles split");
    }

    public static void TestReplanOnDivergence()
    {
        var g = Attackers(); var pilot = Pilot();
        var job = Begin(pilot, g, 1, out var view);
        for (int i = 0; !job.IsDone && i < 2_000_000; i++) job.StepNode();
        var attacks = view.Actions.Where(a => a.Type == PlayerTaskType.MINION_ATTACK && a.Target == g.Player2.Hero.Id).OrderBy(a => a.Source).ToArray();
        pilot.Accept(view, new[] { attacks[0], attacks[1], view.Actions.First(a => a.Type == PlayerTaskType.END_TURN) }, 0, LethalOutcome.None);
        Act(g, attacks[1]);   // the host played something else
        Begin(pilot, g, 2, out _);
        Check(pilot.ReplanReason.StartsWith("divergence:", StringComparison.Ordinal), "a different state must replan as divergence: " + pilot.ReplanReason);
    }

    public static void TestReplanOnIllegalNextAction()
    {
        var g = Attackers(); var pilot = Pilot();
        var job = Begin(pilot, g, 1, out var view);
        for (int i = 0; !job.IsDone && i < 2_000_000; i++) job.StepNode();
        var attack = view.Actions.First(a => a.Type == PlayerTaskType.MINION_ATTACK && a.Target == g.Player2.Hero.Id);
        // The same attacker swinging twice is not legal after the first swing.
        pilot.Accept(view, new[] { attack, attack }, 0, LethalOutcome.None);
        Act(g, attack);
        var next = Begin(pilot, g, 2, out _);
        for (int i = 0; !next.IsDone && i < 2_000_000; i++) next.StepNode();
        Check(pilot.ReplanReason == "illegal-next" && !next.Result.SearchInfo.Reused && next.Result.SearchInfo.Replan == "illegal-next",
            "illegal next step must replan: " + pilot.ReplanReason);
    }

    public static void TestReuseIsDeterministic()
    {
        string Turn()
        {
            var g = Attackers(); Entity.FromCard(g.Player1, Cards.FromId("CS2_120"), zone: g.Player1.HandZone); g.Player1.UsedMana = 6;
            var pilot = Pilot(); var trace = new List<string>();
            for (int index = 1; index < 20 && g.CurrentPlayer == g.Player1; index++)
            {
                var decision = Decide(pilot, g, index);
                Act(g, decision.Action);
                trace.Add(decision.Action + " " + decision.SearchInfo.Replan + " " + MatchRecorder.Fingerprint(g));
            }
            return string.Join("\n", trace);
        }
        string first = Turn(), second = Turn();
        Check(first == second && first.Contains(" reused "), "same game twice: same actions, reuse and fingerprints\n" + first + "\n--\n" + second);
    }

    // plan-reuse on: a resampled own-deck prior alone never blocks the continuation. Off (base): it does, as live.
    public static void TestDeckPriorDoesNotBlockReuse()
    {
        foreach (bool on in new[] { true, false })
        {
            var g = Attackers(); var pilot = on ? Pilot() : Pilot("plan-reuse");
            var first = Decide(pilot, g, 1);
            Act(g, first.Action);
            var view = Resampled(View(pilot, g, 2));
            var job = pilot.Job(view, new GoldenContext(g.Turn, 2, Seed(g, 2), pilot.Profile, pilot.PublicInputs));
            for (int i = 0; !job.IsDone && i < 2_000_000; i++) job.StepNode();
            if (on) Check(job.Result.SearchInfo.Reused && job.Result.SearchInfo.Replan == "reused", "prior must not block reuse: " + job.Result.SearchInfo.Replan);
            else Check(!job.Result.SearchInfo.Reused && job.Result.SearchInfo.Replan.EndsWith(":deck-prior", StringComparison.Ordinal),
                "flag off keeps the base key and names the prior: " + job.Result.SearchInfo.Replan);
        }
    }

    // The strip removes exactly GoldenPublicKey's own-deck segment, and keeps the full key when the layout check fails.
    public static void TestDeckPriorStripIsExact()
    {
        var g = Attackers(); var view = View(Pilot(), g, 1);
        string full = GoldenPublicKey.For(view), stripped = Key(view);
        string prior = string.Concat(view.Facts.OwnDeckKnowledge.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => "|" + p.Key + "=" + p.Value));
        Check(prior.Length > 0 && full.Contains(prior + "|effects:") && stripped == full.Replace(prior + "|effects:", "|effects:"), "segment removed exactly");
        string damaged = full.Replace("|effects:", "|fx:");
        Check(GoldenPilot.WithoutDeckPrior(damaged, view.Facts) == damaged, "unexpected layout keeps the full key");
    }

    // A Discover opens a choice: no continuation is armed across it (choice, then not-cached after the pick).
    public static void TestReplanOnDiscover()
    {
        var g = Attackers(); g.Player1.UsedMana = 4;
        var discover = Entity.FromCard(g.Player1, Cards.FromId("EDR_856"), zone: g.Player1.HandZone);
        var pilot = Pilot();
        var job = Begin(pilot, g, 1, out var view);
        for (int i = 0; !job.IsDone && i < 2_000_000; i++) job.StepNode();
        var play = view.Actions.First(a => a.Source == discover.Id);
        pilot.Accept(view, new[] { play, view.Actions.First(a => a.Type == PlayerTaskType.MINION_ATTACK), view.Actions.First(a => a.Type == PlayerTaskType.END_TURN) }, 0, LethalOutcome.None);
        Act(g, play);
        Check(g.Player1.Choice != null, "fixture: Discover pending");
        var pick = Begin(pilot, g, 2, out var choiceView);
        Check(pilot.ReplanReason == "choice", "a pending Discover must replan: " + pilot.ReplanReason);
        for (int i = 0; !pick.IsDone && i < 2_000_000; i++) pick.StepNode();
        var answer = (ChooseTask)pick.Result.Action.Resolve(g.Player1.Options());
        var pending = g.GetPendingDecisions().Single(x => x.ControllerId == g.Player1.Id);
        Check(g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(pending.Id, pending.ControllerId, answer.Choices), out var error), "pick accepted: " + error);
        Begin(pilot, g, 3, out _);
        Check(pilot.ReplanReason == "not-cached", "nothing is cached across the pick: " + pilot.ReplanReason);
    }

    // A clock cut on a continuable step takes the cached plan (StopSearch reuses before any fallback), so fast finish
    // only ever runs when no valid plan is left.
    public static void TestClockCutContinuesPlan()
    {
        var g = Attackers(); var pilot = Pilot();
        var first = Decide(pilot, g, 1);
        var planned = pilot.LastLine[1];
        Act(g, first.Action);
        var job = Begin(pilot, g, 2, out _);
        var cut = job.StopSearch(SearchStop.Clock);
        Check(cut != null && cut.Action.Equals(planned) && cut.SearchInfo.Reused && cut.FallbackReason == null,
            "clock cut continues the plan: " + cut?.Action + " fallback=" + cut?.FallbackReason);
    }

    // The planned line ran out (an unfinished prefix): plan-end, not a generic miss.
    public static void TestReplanAtPlanEnd()
    {
        var g = Attackers(); var pilot = Pilot();
        var job = Begin(pilot, g, 1, out var view);
        for (int i = 0; !job.IsDone && i < 2_000_000; i++) job.StepNode();
        var attack = view.Actions.First(a => a.Type == PlayerTaskType.MINION_ATTACK && a.Target == g.Player2.Hero.Id);
        pilot.Accept(view, new[] { attack }, 0, LethalOutcome.None);
        Act(g, attack);
        Begin(pilot, g, 2, out _);
        Check(pilot.ReplanReason == "plan-end", "an exhausted prefix replans as plan-end: " + pilot.ReplanReason);
    }

    // Owner-displayed stored payloads (a carved Treant's "Cast {0}") are the owner's knowledge in the fair view.
    public static void TestCarvedTreantCastsItsDisplayedSpell()
    {
        var g = Fixture(CardClass.DRUID, CardClass.DEATHKNIGHT); g.Turn = 17; g.Player1.UsedMana = 9;
        for (int i = 0; i < 4; i++) Board(g, true, "CS2_182", true);
        Board(g, false, "CS2_179");
        var typhoon = Cards.FromId("EDR_232");
        var treant = Entity.FromCard(g.Player1, Cards.FromId("MEND_046t"), zone: g.Player1.HandZone);
        // Exactly what Bashana's Battlecry does: the client reads tag 2 as the carved entity's id, never its dbfId.
        var carved = SabberStoneCore.Actions.PayloadRules.StoreCard(treant, 1, typhoon,
            SabberStoneCore.Actions.PayloadPolicy.Copy | SabberStoneCore.Actions.PayloadPolicy.Silence | SabberStoneCore.Actions.PayloadPolicy.ShowOwner);
        treant[GameTag.TAG_SCRIPT_DATA_NUM_1] = carved.Id;
        g.AuraUpdate();
        Check(treant[GameTag.TAG_SCRIPT_DATA_NUM_1] == carved.Id && carved.Id != typhoon.AssetId, "fixture displays the carved entity id");
        FairBotState Carried(Game game, bool show) => FairBotState.Create(game, 1, 71, DeckStyle.Tempo,
            new FairOptions { RemoveOpponentSecrets = true, TrackOwnDeck = true, MaterializeOwnDeckTriggers = true, ShowOwnCarriedCards = show });
        // carried-cards off (base): the payload is masked and the Treant resolves as a plain 2/2.
        var masked = Carried(g, false);
        var maskedCast = masked.Apply(masked.Actions.First(a => a.Type == PlayerTaskType.PLAY_CARD && a.Source == treant.Id));
        Check(maskedCast.Facts.Own.Board.Count == 5, "flag off keeps the payload private (base)");
        Check(new GoldenPilot(NpcCatalog.All[0]).Options.FairViewFactory != null && NpcCatalog.All[0].Fixes.On("carried-cards"), "golden factory default on");
        var view = Carried(g, true);
        var play = view.Actions.First(a => a.Type == PlayerTaskType.PLAY_CARD && a.Source == treant.Id);
        var cast = view.Apply(play);
        Check(cast.Facts.Own.Board.Count == 0 && cast.Facts.Opponent.Board.Count == 0, "the carved Typhoon must resolve in the fair search");
        var evaluator = new DeckPlanEvaluator(DeckPlans.DragonWarrior);
        double end = view.Apply(view.Actions.First(a => a.Type == PlayerTaskType.END_TURN)).Evaluate(evaluator);
        Check(cast.Evaluate(evaluator) < end - 20, "shuffling away a winning board must evaluate well below passing");
        Check(FastFinish.Choose(view, view.Actions, out _)?.Source != treant.Id, "fast finish never plays a board wipe");
        // The opponent's carved Treant stays hidden: its hand card is unknown in our view.
        var h = Fixture(CardClass.DRUID, CardClass.DRUID);
        var theirs = Entity.FromCard(h.Player2, Cards.FromId("MEND_046t"), zone: h.Player2.HandZone);
        theirs[GameTag.TAG_SCRIPT_DATA_NUM_1] = typhoon.AssetId;
        SabberStoneCore.Actions.PayloadRules.StoreCard(theirs, 1, typhoon);
        Check(Carried(h, true).Facts.Opponent.Hand.All(c => c.Unknown), "opponent payload stays private");
    }
}
