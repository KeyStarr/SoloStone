using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Model.Events;
using static Core2026Proof;

// The death loop and the Summon Resolution Step share one cycle budget per outermost cleanup (Game.MaxCleanupCycles).
// The rulebook repeats Death Phases "until no new Deaths occur" and names no limit; a loop that never ends on its own
// ends the game in a draw (engine choice). #210 let the two steps feed each other inside one cleanup with no bound, and
// the Rewind F5 fixture grew to 18 GB. Each case here checks the outcome, that nothing is left pending, and that the
// cleanup allocates a bounded amount.
internal static class CleanupCycleBudgetProof
{
    private const long AllocationLimit = 64L << 20; // the 1000-cycle loop allocates about 4 MB
    private static void Verify(bool condition, string message) => Core2026Proof.Check(condition, message);

    [GroupProof("cleanup-cycle-budget")]
    internal static void Run()
    {
        SummonDeathCycleEndsInDraw();
        PlayedMinionCycleEndsInDraw();
        DeathLoopWithoutSummonReactionEndsInDraw();
        NestedCleanupsShareTheBudget();
        HeroLostBeforeTheBudgetKeepsTheResult();
        FiniteCycleUnderTheBudgetResolves();
        Console.WriteLine($"PASS cleanup cycle budget: summon/death loops end in a draw after {Game.MaxCleanupCycles} cycles with bounded memory; finite chains resolve in one cleanup");
    }

    private static Game Fixture() => HistoryFactsProof.Fixture();

    private static Minion Wisp(Controller owner) => (Minion)Entity.FromCard(owner, Cards.FromId("CS2_231"));

    // Every summon reaction kills the summoned minion; every death summons a new one.
    private static Func<int> LoopForever(Game g, Controller owner)
    {
        int deaths = 0;
        g.TriggerManager.Events.Subscribe<SummonEvent>(sender => ((Minion)sender).ToBeDestroyed = true);
        g.TriggerManager.Events.Subscribe<DeathEvent>(_ => {
            deaths++;
            Generic.SummonBlock(g, Wisp(owner), -1, null);
        });
        return () => deaths;
    }

    private static void Queue(Game g, Controller owner, Action action) =>
        g.TaskQueue.Enqueue(new CustomTask((_, _, _, _, _) => action()), owner, owner.Hero, null);

    private static List<string> Diagnostics(Game g)
    {
        var lines = new List<string>();
        g.CycleBudgetExhaustedDiagnostic = lines.Add;
        return lines;
    }

    private static long Allocated(Action action)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        action();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private static void Drawn(Game g, List<string> diagnostics, long allocated, string name)
    {
        Verify(g.State == State.COMPLETE && g.Player1.PlayState == PlayState.TIED && g.Player2.PlayState == PlayState.TIED,
            $"{name}: the loop ends the game in a draw, got state={g.State} p1={g.Player1.PlayState} p2={g.Player2.PlayState}");
        Verify(g.CycleBudgetExhausted && diagnostics.Count == 1 && diagnostics[0] == $"Cleanup cycle limit={Game.MaxCleanupCycles}",
            $"{name}: one cycle-limit diagnostic, got [{string.Join("|", diagnostics)}]");
        Verify(g.SummonedMinions.Count == 0 && g.DeadMinions.Count == 0 && g.DeadBoardObjects.Count == 0,
            $"{name}: nothing left pending, summoned={g.SummonedMinions.Count} dead={g.DeadMinions.Count}");
        Verify(allocated < AllocationLimit, $"{name}: the stopped loop allocates under {AllocationLimit >> 20} MB, got {allocated >> 20} MB");
    }

    private static void SummonDeathCycleEndsInDraw()
    {
        var g = Fixture(); var p = g.Player1;
        var first = Summon(p, "CS2_231");
        var deaths = LoopForever(g, p); var diagnostics = Diagnostics(g);
        int history = g.PowerHistory.Full.Count;
        first.ToBeDestroyed = true;
        long allocated = Allocated(g.DeathProcessingAndAuraUpdate);
        Drawn(g, diagnostics, allocated, "SummonDeathCycle");
        // One cycle per Death Phase and one per Summon Resolution pass: half the budget each.
        Verify(deaths() == Game.MaxCleanupCycles / 2, $"SummonDeathCycle: {Game.MaxCleanupCycles / 2} deaths before the stop, got {deaths()}");
        Verify(g.PowerHistory.Full.Count - history < 15 * Game.MaxCleanupCycles,
            $"SummonDeathCycle: history stays bounded, got {g.PowerHistory.Full.Count - history} entries");
        // A later cleanup of the finished game resolves nothing and fires no second diagnostic.
        Summon(p, "CS2_231");
        Verify(diagnostics.Count == 1 && g.SummonedMinions.Count == 0 && deaths() == Game.MaxCleanupCycles / 2,
            "SummonDeathCycle: a cleanup after the draw resolves nothing");
    }

    private static void PlayedMinionCycleEndsInDraw()
    {
        var g = Fixture(); var p = g.CurrentPlayer;
        var card = Add(p, "CS2_231");
        LoopForever(g, p); var diagnostics = Diagnostics(g);
        bool accepted = false;
        long allocated = Allocated(() => accepted = g.Process(PlayCardTask.Any(p, card)));
        Verify(accepted, "PlayedMinionCycle: the play is accepted");
        Drawn(g, diagnostics, allocated, "PlayedMinionCycle");
        var terminal = g.HistoryFacts.Query.Events<SabberStoneCore.Model.MatchTerminalFact>().ToArray();
        Verify(terminal.Length == 1 && terminal[0].Player1State == PlayState.TIED && terminal[0].Player2State == PlayState.TIED,
            $"PlayedMinionCycle: one terminal fact records the draw, got {terminal.Length}");
        var clone = g.CloneExact(history: false);
        Verify(clone.CycleBudgetExhausted && clone.State == State.COMPLETE, "PlayedMinionCycle: an exact clone keeps the exhausted budget");
    }

    private static void DeathLoopWithoutSummonReactionEndsInDraw()
    {
        var g = Fixture(); var p = g.Player1;
        var first = Summon(p, "CS2_231");
        int deaths = 0; var diagnostics = Diagnostics(g);
        // Like a Deathrattle, the death queues a task for the Death Resolution Phase: summon a minion and destroy it.
        g.TriggerManager.Events.Subscribe<DeathEvent>(_ => {
            deaths++;
            Queue(g, p, () => { var next = Wisp(p); Generic.SummonBlock(g, next, -1, null); next.ToBeDestroyed = true; });
        });
        Verify(!g.TriggerManager.HasOnSummonTrigger, "DeathLoop: no summon listener, so only the death loop runs");
        first.ToBeDestroyed = true;
        long allocated = Allocated(g.DeathProcessingAndAuraUpdate);
        Drawn(g, diagnostics, allocated, "DeathLoop");
        Verify(deaths == Game.MaxCleanupCycles, $"DeathLoop: {Game.MaxCleanupCycles} Death Phases before the stop, got {deaths}");
    }

    // A death reaction that runs its own cleanup (as an overdrawn IMPFERNAL! does) spends the outer cleanup's budget.
    private static void NestedCleanupsShareTheBudget()
    {
        var g = Fixture(); var p = g.Player1;
        var first = Summon(p, "CS2_231");
        int deaths = 0, nested = 0; var diagnostics = Diagnostics(g);
        g.TriggerManager.Events.Subscribe<DeathEvent>(_ => {
            int death = ++deaths;
            Queue(g, p, () => {
                var next = Wisp(p); Generic.SummonBlock(g, next, -1, null); next.ToBeDestroyed = true;
                if (death % 10 == 0) { nested++; g.DeathProcessingAndAuraUpdate(); }
            });
        });
        first.ToBeDestroyed = true;
        long allocated = Allocated(g.DeathProcessingAndAuraUpdate);
        Drawn(g, diagnostics, allocated, "NestedCleanups");
        Verify(nested > 0 && deaths == Game.MaxCleanupCycles, $"NestedCleanups: {Game.MaxCleanupCycles} Death Phases across the nested cleanups, got {deaths} ({nested} nested)");
    }

    // A hero that dies in the cleanup decides the game; running out of budget later in that cleanup keeps that result.
    private static void HeroLostBeforeTheBudgetKeepsTheResult()
    {
        var g = Fixture(); var p = g.Player1;
        var first = Summon(p, "CS2_231");
        var deaths = LoopForever(g, p); var diagnostics = Diagnostics(g);
        g.TriggerManager.Events.Subscribe<DeathEvent>(_ => { if (deaths() == 10) g.Player2.Hero.ToBeDestroyed = true; });
        first.ToBeDestroyed = true;
        g.DeathProcessingAndAuraUpdate();
        Verify(g.State == State.COMPLETE && g.Player1.PlayState == PlayState.WON && g.Player2.PlayState == PlayState.LOST,
            $"HeroLost: the earlier hero death decides the game, got p1={g.Player1.PlayState} p2={g.Player2.PlayState}");
        Verify(g.CycleBudgetExhausted && diagnostics.Count == 1 && g.SummonedMinions.Count == 0 && g.DeadMinions.Count == 0,
            $"HeroLost: the loop still stops at the budget, diagnostics={diagnostics.Count}");
    }

    // A long chain that stops on its own resolves completely in one cleanup, and the next cleanup gets a fresh budget:
    // two chains of 2/5 of the budget each spend 4/5 of it per cleanup, more than the budget together.
    private static void FiniteCycleUnderTheBudgetResolves()
    {
        int rounds = Game.MaxCleanupCycles * 2 / 5;
        var g = Fixture(); var p = g.Player1;
        int deaths = 0, stop = rounds; var diagnostics = Diagnostics(g);
        g.TriggerManager.Events.Subscribe<SummonEvent>(sender => ((Minion)sender).ToBeDestroyed = true);
        g.TriggerManager.Events.Subscribe<DeathEvent>(_ => { if (++deaths < stop) Generic.SummonBlock(g, Wisp(p), -1, null); });
        for (int cleanup = 1; cleanup <= 2; cleanup++, stop += rounds)
        {
            var first = Summon(p, "CS2_231"); // dies in the Summon Resolution Step of its own cleanup
            Verify(g.State == State.RUNNING && !g.CycleBudgetExhausted && diagnostics.Count == 0,
                $"FiniteCycle: a chain of {rounds} rounds is under the budget (cleanup {cleanup})");
            Verify(deaths == cleanup * rounds && g.SummonedMinions.Count == 0 && g.DeadMinions.Count == 0 && first.Zone == p.GraveyardZone,
                $"FiniteCycle: the chain resolves in one cleanup, deaths={deaths} summoned={g.SummonedMinions.Count} (cleanup {cleanup})");
        }
    }
}
