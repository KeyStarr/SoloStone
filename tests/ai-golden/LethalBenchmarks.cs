using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using SabberStoneCore.Model;

// Focused, device-independent benchmark entry point; snapshot input never touches a profile or client.
// Usage: --lethal-benchmark <snapshot|-> [node cap] [wall ms]. Defaults: a 4000-node cap for the fixed-work runs, 500 ms
// for the fixed-wall runs. "captured-turn28" is a saved game snapshot (not in the repo) of a live turn from
// 2026-10-27, taken just before its END_TURN (action 149); pass "-" to skip it. Compares serial lethal search, matched
// serial partitions and two workers; the tiny cases are latency controls, not speed-up evidence. Snapshot and runtime
// must come from the same engine build.
internal static class LethalBenchmarks
{
    internal static int Run(string[] args)
    {
        int cap = args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 4000;
        int wall = args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 500;
        Console.WriteLine("case\tmode\tbudget\toutcome\tnodes\twallMs\tcpuMs\tallocatedBytes");
        foreach (var kind in new[] { "fissure", "near", "torch", "captured-turn28" })
        {
            if (kind == "captured-turn28" && (args.Length == 0 || args[0] == "-")) continue;
            var game = kind == "captured-turn28" ? Game.LoadSnapshot(File.ReadAllBytes(args[0])) : LethalTests.Fixture(kind);
            var state = FairBotState.Create(game, game.CurrentPlayer.PlayerId, 71, DeckStyle.Tempo,
                new FairOptions { TrackOwnDeck = true, MaterializeOwnDeckTriggers = true, RemoveOpponentSecrets = true });
            // Warm definitions/JIT outside measurements; every measured run still creates its own fair roots.
            LethalSolver.Find(state, 16);
            var warm = new ParallelLethalSearch(state, 16); while (!warm.IsDone) { warm.Poll(); Thread.Sleep(1); }
            foreach (string budget in new[] { "fixed-work", "fixed-wall" })
            foreach (string mode in new[] { "serial-dfs", "serial-partitions", "two-workers" })
            {
                int limit = budget == "fixed-work" ? cap : 1000000;
                double milliseconds = budget == "fixed-work" ? double.PositiveInfinity : wall;
                var cpu = Process.GetCurrentProcess().TotalProcessorTime; long allocated = GC.GetTotalAllocatedBytes();
                var watch = Stopwatch.StartNew(); LethalResult result;
                if (mode == "two-workers")
                {
                    var job = new ParallelLethalSearch(state, limit);
                    while (!job.IsDone && watch.Elapsed.TotalMilliseconds < milliseconds) { job.Poll(); Thread.Sleep(1); }
                    if (!job.IsDone) job.Stop();
                    result = job.Result;
                }
                else if (mode == "serial-dfs")
                {
                    var job = LethalSolver.Job(state.ForkForSearch(null), limit);
                    while (!job.IsDone && watch.Elapsed.TotalMilliseconds < milliseconds) job.StepNode();
                    result = job.Result;
                }
                else result = SerialPartitions(state, limit, watch, milliseconds);
                watch.Stop();
                Console.WriteLine(string.Join("\t", kind, mode, budget, result.Outcome, result.Nodes,
                    watch.Elapsed.TotalMilliseconds.ToString("F2", CultureInfo.InvariantCulture),
                    (Process.GetCurrentProcess().TotalProcessorTime - cpu).TotalMilliseconds.ToString("F2", CultureInfo.InvariantCulture),
                    GC.GetTotalAllocatedBytes() - allocated));
            }
            var minimum = new BoundedFallbackJob(state, state.Actions);
            while (!minimum.IsDone) minimum.StepNode();
            Console.WriteLine("minimum-answer\t" + kind + "\tnodes=" + minimum.Nodes + "\taction=" + minimum.Result);
        }
        return 0;
    }

    private static LethalResult SerialPartitions(FairBotState state, int cap, Stopwatch watch, double wall)
    {
        var actions = LethalJob.OrderedActions(state);
        var jobs = Enumerable.Range(0, 2).Select(i => new LethalJob(state.ForkForSearch(null), cap / 2 + (i < cap % 2 ? 1 : 0),
            null, actions.Where((action, index) => index % 2 == i).ToArray())).ToArray();
        while (!jobs.All(job => job.IsDone) && watch.Elapsed.TotalMilliseconds < wall)
        {
            foreach (var job in jobs)
            {
                if (!job.IsDone && watch.Elapsed.TotalMilliseconds < wall) job.StepNode();
                if (jobs[0].Result.Outcome == LethalOutcome.Found) break;
            }
            if (jobs[0].Result.Outcome == LethalOutcome.Found) break;
        }
        var found = jobs.FirstOrDefault(job => job.Result.Outcome == LethalOutcome.Found);
        bool complete = jobs[0].Result.Outcome == LethalOutcome.Found || jobs.All(job => job.IsDone);
        return new LethalResult(complete && found != null ? LethalOutcome.Found : jobs.All(job => job.Result.Outcome == LethalOutcome.None)
            ? LethalOutcome.None : LethalOutcome.Incomplete, complete ? found?.Result.Line : null, jobs.Sum(job => job.Result.Nodes));
    }
}
