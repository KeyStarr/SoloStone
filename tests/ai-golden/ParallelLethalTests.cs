using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using SabberStoneCore.Enums;
using static ReplyTests;

public static class ParallelLethalTests
{
    private static ParallelLethalSearch Run(FairBotState state, int cap, Action<int> before = null)
    {
        int charged = 0;
        var job = new ParallelLethalSearch(state, cap, () => charged++, before);
        var watch = Stopwatch.StartNew();
        while (!job.IsDone)
        {
            job.Poll(); Thread.Yield();
            if (watch.ElapsedMilliseconds > 30000) { job.Stop(); throw new Exception("parallel test exceeded deadline"); }
        }
        Check(job.Nodes == charged && job.Nodes == job.Result.Nodes && job.Nodes <= cap, "aggregate node cap/charge");
        return job;
    }
    private static string Signature(LethalResult result) => result.Outcome + "/" + string.Join(";", result.Line);

    public static void TestPartitionsAndSampledReplay()
    {
        foreach (string kind in new[] { "fissure", "torch", "warptooth", "volcano", "near", "random" })
        {
            var game = LethalTests.Fixture(kind); var before = game.SaveSnapshot();
            var state = LethalTests.View(game); var job = Run(state, 4000);
            if (kind == "near") Check(job.Result.Outcome == LethalOutcome.None, "complete no-lethal partitions must return None");
            else if (kind == "random") Check(job.Result.Outcome != LethalOutcome.Found, "random candidate bypassed four-seed replay");
            else
            {
                Check(job.Result.Outcome == LethalOutcome.Found, "partition missed " + kind);
                for (int sample = 0; sample <= 4; sample++)
                {
                    var replay = sample == 0 ? state.ForkForSearch(null) : state.WithArtificialSeed(sample);
                    foreach (var action in job.Result.Line)
                    { Check(replay.Actions.Contains(action), "sampled replay illegal action"); replay = replay.Apply(action); }
                    Check(replay.Facts.Own.PlayState == PlayState.WON, "sampled replay did not win");
                }
            }
            Check(before.SequenceEqual(game.SaveSnapshot()), "parallel search mutated authority");
        }
        foreach (int cap in new[] { 0, 1, 2 })
            Check(Run(LethalTests.View(LethalTests.Fixture("torch")), cap).Result.Outcome == LethalOutcome.Incomplete,
                "allocated cap exhaustion reported None/partial lethal");
    }

    public static void TestHiddenStateIndependentRootsAndCompletionOrder()
    {
        foreach (int seat in new[] { 1, 2 })
        {
            var game = LethalTests.Fixture("volcano", seat);
            var view = LethalTests.View(game); var hidden = LethalTests.View(LethalTests.HiddenVariant(game));
            string expected = Signature(Run(view, 4000).Result);
            foreach (int slow in new[] { 0, 1 })
            {
                Check(Signature(Run(view, 4000, i => { if (i == slow) Thread.Sleep(20); }).Result) == expected,
                    "worker completion order changed accepted line");
                Check(Signature(Run(hidden, 4000, i => { if (i == slow) Thread.Sleep(20); }).Result) == expected,
                    "private identity/order/RNG changed lethal");
            }
            var twin = view.ForkForSearch(null); var snapshot = GoldenPublicKey.For(twin);
            var changed = view.ForkForSearch(null); changed.Apply(changed.Actions.First()); changed.WithArtificialSeed(3);
            Check(snapshot == GoldenPublicKey.For(twin), "workers shared game/options");
            var random = typeof(SabberStoneCore.Model.Game).GetProperty("Random", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Check(!ReferenceEquals(random.GetValue(twin.FingerprintGame), random.GetValue(changed.FingerprintGame)), "workers shared RNG");
        }
    }

    public static void TestStopRetainsVerifiedLethalWithoutOwnerPoll()
    {
        var state = LethalTests.View(LethalTests.Fixture("fissure"));
        var job = new ParallelLethalSearch(state, 4000);
        job.Poll();
        // Deliberately skip the owner's next Poll, as when the runner cuts its clock first.
        var tasks = (System.Threading.Tasks.Task[])typeof(ParallelLethalSearch)
            .GetField("running", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(job);
        Check(System.Threading.Tasks.Task.WaitAll(tasks, 5000), "controlled workers did not complete");
        Check(job.Result.Outcome == LethalOutcome.Incomplete, "fixture accidentally polled worker results");
        job.Stop();
        Check(job.Result.Outcome == LethalOutcome.Found, "cutoff discarded completed four-seed lethal");
        for (int sample = 1; sample <= 4; sample++)
        {
            var replay = state.WithArtificialSeed(sample);
            foreach (var action in job.Result.Line) replay = replay.Apply(action);
            Check(replay.Facts.Own.PlayState == PlayState.WON, "cutoff promoted an unverified candidate");
        }
    }

    public static void TestSharedDeadlineIsIncompleteWithoutFurtherPump()
    {
        var job = new ParallelLethalSearch(LethalTests.View(LethalTests.Fixture("near")), 4000);
        job.LimitMilliseconds(0); job.Poll(); Thread.Sleep(20);
        while (!job.IsDone) { job.Poll(); Thread.Yield(); }
        Check(job.Nodes == 0 && job.Result.Outcome == LethalOutcome.Incomplete,
            "expired shared deadline did work or reported None");
    }

    public static void TestDeadlineCannotBeExtended()
    {
        var job = new ParallelLethalSearch(LethalTests.View(LethalTests.Fixture("near")), 4000);
        job.LimitMilliseconds(0);
        job.LimitMilliseconds(100000);
        while (!job.IsDone) { job.Poll(); Thread.Yield(); }
        Check(job.Nodes == 0 && job.Result.Outcome == LethalOutcome.Incomplete,
            "later deadline update revived expired search");
    }

    public static void TestDeadlineJoinsAndChargesAtomicExpansionWithoutVerifyingPartialWin()
    {
        var game = Fixture(); game.Player1.Hero.HeroPower.IsExhausted = true;
        using (var entered = new CountdownEvent(2))
        using (var release = new ManualResetEventSlim())
        {
            int calls = 0, charged = 0;
            var definition = SabberStoneCore.Model.Cards.FromId("CS2_029").Clone();
            definition.Power = new SabberStoneCore.Enchants.Power {
                PowerTask = new SabberStoneCore.Tasks.SimpleTasks.CustomTask((g, controller, source, target, stack) => {
                    Interlocked.Increment(ref calls); entered.Signal();
                    if (!release.Wait(5000)) throw new Exception("controlled atomic expansion not released");
                    controller.Opponent.Hero.Damage = 30;
                }) };
            var card = SabberStoneCore.Model.Entities.Entity.FromCard(game.Player1, definition, zone: game.Player1.HandZone); card.Cost = 0;
            var authority = GoldenPublicKey.For(View(game));
            var job = new ParallelLethalSearch(View(game), 4000, () => charged++);
            job.Poll();
            Check(entered.Wait(5000), "both isolated workers did not enter controlled engine action");
            job.LimitMilliseconds(0);
            var unblock = System.Threading.Tasks.Task.Run(() => { Thread.Sleep(40); release.Set(); });
            var watch = Stopwatch.StartNew(); job.Stop(); watch.Stop(); unblock.Wait();
            Check(watch.ElapsedMilliseconds >= 20 && calls == 2, "stop did not join exactly the two in-flight actions");
            Check(job.Nodes == charged && charged == 4, "atomic nodes were omitted or extra verification started: " + charged);
            Check(job.IsDone && job.Result.Outcome == LethalOutcome.Incomplete,
                "deadline promoted a win without all four sampled replays");
            Check(authority == GoldenPublicKey.For(View(game)), "deadline join mutated authoritative game");
            int stopped = job.Nodes; Thread.Sleep(20); job.Poll();
            Check(job.Nodes == stopped && calls == 2, "deadline left an orphan worker");
        }
    }

    public static void TestCancellationAndIsolatedFailure()
    {
        var state = LethalTests.View(LethalTests.Fixture("near"));
        var entered = new ManualResetEventSlim(); var release = new ManualResetEventSlim();
        var job = new ParallelLethalSearch(state, 4000, beforeWorker: i => { entered.Set(); release.Wait(); });
        job.Poll(); Check(entered.Wait(5000), "workers not started"); release.Set(); job.Stop();
        int stopped = job.Nodes; Thread.Sleep(20); job.Poll();
        Check(job.IsDone && job.Result.Outcome == LethalOutcome.Incomplete && job.Nodes == stopped,
            "cancelled workers retained work or claimed None");
        int owner = Thread.CurrentThread.ManagedThreadId, reports = 0;
        state.FaultReporter = line => { Check(Thread.CurrentThread.ManagedThreadId == owner, "off-thread host logger"); reports++; };
        var failed = Run(state, 4000, i => { if (i == 0) throw new InvalidOperationException("isolated-worker"); });
        Check(failed.Result.Outcome == LethalOutcome.Incomplete && reports == 1, "isolated failure escaped or claimed complete no-lethal");
    }

    public static void TestDecisionCanStopWithMinimumAnswerWhileParallelRuns()
    {
        Check(!GoldenPilot.ParallelLethalEnabled, "parallel candidate enabled without native acceptance");
        try
        {
            GoldenPilot.ParallelLethalEnabled = true;
            var game = Fixture(); SabberStoneCore.Model.Entities.Entity.FromCard(game.Player1,
                SabberStoneCore.Model.Cards.FromId("CS2_182"), zone: game.Player1.HandZone).Cost = 0;
            var view = View(game); var profile = NpcCatalog.All[0]; var pilot = new GoldenPilot(profile);
            var job = pilot.Job(view, new GoldenContext(game.Turn, 1, 71, profile));
            while (job.Preparing) job.StepNode();
            var decision = job.StopSearch(SearchStop.Clock);
            Check(decision.Action.Type == SabberStoneCore.Tasks.PlayerTasks.PlayerTaskType.PLAY_CARD
                && decision.FallbackReason == null && job.LethalResult == "incomplete", "parallel clock cut discarded usable answer");
            Check(pilot.RemainingNodes < profile.Budget.TurnNodes && pilot.RemainingLethalNodes == profile.Budget.TurnLethalNodes,
                "minimum answer not charged, or idle workers charged fictitious nodes");
        }
        finally { GoldenPilot.ParallelLethalEnabled = false; }
    }
}
