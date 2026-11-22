using System.Text.Json;

namespace TransportInvariants;

// Tail renewal of a pinned transport replay (replays/) after an intended rules change, when the recorded game legitimately no
// longer plays the same. It never runs in a normal test run: only (from the repository root)
//   dotnet TransportInvariants.dll --renew-replay-tail PATH [--from-step N] [--seed N] [--write | --out FILE]
// The recorded actions are replayed on the current engine up to the first step where the game differs: the first recorded action that is
// no longer legal, or the first legal-move hash that differs (the same action number would then stand for another move), whichever
// comes first; --from-step N starts earlier, never later. From there a seeded random policy plays the game to its end. The legal-move
// hashes, the engine fingerprint (when the file has one), the findings hash, the turns and the winner are re-recorded; the decks, the pool
// pin and the seed of the game are not. Each tail is added to "renewals": [{"fromStep","seed"}] in the file, so it can be redone.
// Without --write or --out nothing is written. A game that comes out identical writes nothing; one whose legal-move hashes alone drifted
// is refused (the engine-proof renewal tool re-records those, with its guards). The tool re-records; it does not decide. A renewal needs
// a reviewed pinned retail rule, as a golden does (docs/TESTING.md, "Goldens").
internal static class ReplayRenewal
{
    internal enum Kind { Unchanged, HashOnly, Tail }
    internal sealed record Outcome(Kind Kind, string Text, string Before, string After, int Divergence, int Kept, int FirstHashDrift, TransportReplay Replay,
        string FindingsBefore, string FindingsAfter, int Violations, string[] HistoryFindings);

    internal const string Usage = "usage (from the repository root): dotnet TransportInvariants.dll --renew-replay-tail PATH [--from-step N] [--seed N] [--write | --out FILE]";

    internal static int Run(string[] args)
    {
        string path = args.Length > 1 && !args[1].StartsWith("--") ? Path.GetFullPath(args[1]) : throw new ArgumentException(Usage);
        int? fromStep = null; long? seed = null; string output = null; bool write = false;
        for (int i = 2; i < args.Length; i++)
            switch (args[i])
            {
                case "--from-step" when i + 1 < args.Length && int.TryParse(args[i + 1], out int n) && n >= 0: fromStep = n; i++; break;
                case "--seed" when i + 1 < args.Length && long.TryParse(args[i + 1], out long s): seed = s; i++; break;
                case "--out" when i + 1 < args.Length: output = Path.GetFullPath(args[++i]); break;
                case "--write": write = true; break;
                default: throw new ArgumentException(Usage);
            }
        if (write && output != null) throw new ArgumentException("--write and --out are exclusive");
        string current = File.ReadAllText(path);
        var outcome = Renew(current, fromStep, seed);
        string name = Path.GetFileName(path);
        if (outcome.Kind == Kind.HashOnly)
        {
            Console.WriteLine($"{name}: the game still plays as recorded (same actions, turns, winner, fingerprint); only legal-move hashes drifted, first at step {outcome.FirstHashDrift}.");
            Console.WriteLine("  No tail was re-recorded and nothing was written: re-record the hashes with the engine-proof renewal tool (EngineProof.dll --renew-replay), which guards them.");
            return 1;
        }
        Console.WriteLine(name + ": " + (outcome.Kind == Kind.Unchanged ? "nothing drifted; no tail was re-recorded" : $"tail re-recorded from step {outcome.Divergence}"));
        Console.WriteLine("  before: " + outcome.Before);
        Console.WriteLine("  after:  " + outcome.After);
        Console.WriteLine("  first legal-move hash drift: " + (outcome.FirstHashDrift < 0 ? "none" : "step " + outcome.FirstHashDrift)
            + "; unchanged steps: " + outcome.Kept + "; file " + (outcome.Text == current ? "unchanged" : "changes"));
        Console.WriteLine($"  findings hash: {outcome.FindingsBefore ?? "none"} -> {outcome.FindingsAfter}; transport violations: {outcome.Violations}; history-shape findings: "
            + (outcome.HistoryFindings.Length == 0 ? "none" : string.Join("; ", outcome.HistoryFindings)));
        string target = write ? path : output;
        if (target == null) { Console.WriteLine("  dry run: nothing written (--write rewrites the file, --out FILE writes a copy)"); return 0; }
        if (target == path && outcome.Text == current) { Console.WriteLine("  nothing to write"); return 0; }
        File.WriteAllText(target, outcome.Text);
        Console.WriteLine("  wrote " + target);
        return 0;
    }

    // The renewed file text and the before/after summary; writes nothing.
    internal static Outcome Renew(string text, int? fromStep, long? seed)
    {
        var replay = JsonSerializer.Deserialize<TransportReplay>(text, Program.Json);
        if (replay?.Schema != 1 || replay.Game?.Actions == null || replay.Game.Actions.Count == 0 || replay.Game.LegalShas == null)
            throw new ArgumentException("not a step-by-step transport replay (transport-debt replays store no actions)");
        var record = replay.Game;
        if (fromStep > record.Actions.Count) throw new ArgumentException($"--from-step {fromStep} is past the {record.Actions.Count} recorded actions");
        if ((replay.Focused || record.Player.StartsWith("standard-coverage-", StringComparison.Ordinal)) && fromStep < 3)
            throw new ArgumentException("a forced-card replay is arranged at step 2; renew from step 3 or later");
        string Summary(TransportReplay r) => $"turns {r.Game.Turns}, winner {r.Game.Winner}, {r.Game.Actions.Count} actions, fingerprint {r.EngineFingerprint ?? "none"}";
        string before = Summary(replay);
        var stored = (Count: record.Actions.Count, record.Turns, record.Winner, Fingerprint: replay.EngineFingerprint, Findings: replay.FindingsSha);
        TransportReplay Fresh() => JsonSerializer.Deserialize<TransportReplay>(text, Program.Json);

        if (fromStep == null)
        {
            // Probe: the recorded actions as they are, wherever they are still legal. A game that comes out identical needs no tail.
            var probe = Fresh(); var probePlan = new RenewPlan(); var probeRunner = new Runner();
            probeRunner.Run(probe, renew: probePlan);
            bool gameSame = probePlan.Divergence < 0 && probe.Game.Actions.Count == stored.Count && probe.Game.Turns == stored.Turns && probe.Game.Winner == stored.Winner
                && (stored.Fingerprint != null ? probePlan.Fingerprint == stored.Fingerprint : probePlan.FirstHashDrift < 0);
            if (gameSame && probePlan.FirstHashDrift >= 0)
                return new(Kind.HashOnly, null, before, before, -1, stored.Count, probePlan.FirstHashDrift, replay, stored.Findings, probe.FindingsSha, 0, Array.Empty<string>());
            if (gameSame)
            {
                // Same game. A moved transport findings hash is no presentation-only change to substitute: it means the fake client's picture or
                // the history-shape findings differ (the proofs need none), so it needs a look and an explicit --from-step.
                if (stored.Findings != null && probe.FindingsSha != stored.Findings)
                    throw new ArgumentException($"the game is unchanged but the transport findings changed ({probeRunner.ViolationCount} violations; findings hash {stored.Findings} -> {probe.FindingsSha}): "
                        + "look at them before renewing; pass --from-step N to re-record from a step");
                return new(Kind.Unchanged, text, before, before, -1, stored.Count, -1, replay, stored.Findings, probe.FindingsSha, probeRunner.ViolationCount, Array.Empty<string>());
            }
        }

        long tailSeed = seed ?? unchecked(record.Seed ^ 0x52454E4557L);
        var plan = new RenewPlan { FromStep = fromStep, TailSeed = unchecked((ulong)tailSeed), TailAtDrift = true };
        var runner = new Runner();
        var tail = Fresh();
        var history = runner.Run(tail, renew: plan);
        if (plan.Divergence < 0)
            throw new ArgumentException($"the game changed ({before} -> {Summary(tail)}) but no legal-move hash drifted and every recorded action is still legal, so there is no step to start from: "
                + "pass --from-step N to re-record the tail from a step you choose");
        (tail.Renewals ??= new()).Add(new TailRenewal { FromStep = plan.Divergence, Seed = tailSeed });
        string renewed = JsonSerializer.Serialize(tail, Program.Json);
        // Pinned files predate the golden-only decisionsSha field and do not carry it; do not add a null one.
        if (tail.Game.DecisionsSha == null && !text.Contains("\"decisionsSha\"", StringComparison.Ordinal))
            renewed = renewed.Replace(",\"decisionsSha\":null", "", StringComparison.Ordinal);
        return new(Kind.Tail, renewed + "\n", before, Summary(tail), plan.Divergence, plan.Divergence, plan.FirstHashDrift, tail, stored.Findings, tail.FindingsSha,
            runner.ViolationCount, history.Findings.Select(f => $"{f.Rule}@{f.Step}: {f.Card}").ToArray());
    }
}
