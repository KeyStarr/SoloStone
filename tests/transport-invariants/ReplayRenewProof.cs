using System.Text.Json;

namespace TransportInvariants;

// Headless checks of the tail renewal tool (ReplayRenewal.cs) on the pinned replays and temporary copies of the Felhunter one.
internal static class ReplayRenewProof
{
    private static string Dump(TransportReplay replay) => JsonSerializer.Serialize(replay, Program.Json) + "\n";

    internal static void Run()
    {
        const string source = "tests/transport-invariants/replays/deathrattle/standard-batch-1-standard-batch-1-21-vs-standard-batch-1-25-s4-random.json";
        string text = File.ReadAllText(source);
        var original = JsonSerializer.Deserialize<TransportReplay>(text, Program.Json);
        int total = original.Game.Actions.Count;

        // A replay that still plays writes nothing, whatever order its keys are in: every step-by-step pinned replay, as it is.
        int checkedFiles = 0;
        foreach (string file in Directory.GetFiles("tests/transport-invariants/replays", "*.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            string fileText = File.ReadAllText(file);
            if (JsonSerializer.Deserialize<TransportReplay>(fileText, Program.Json).Game.Actions.Count == 0) continue; // transport-debt: replayed from the seed
            var same = ReplayRenewal.Renew(fileText, null, null);
            SelfChecks.Check(same.Kind == ReplayRenewal.Kind.Unchanged && same.Text == fileText && same.Divergence < 0, "an unchanged replay renews to its own bytes: " + file);
            checkedFiles++;
        }
        SelfChecks.Check(checkedFiles >= 17, "every pinned step-by-step replay was checked: " + checkedFiles);

        const int cut = 40;
        var forced = ReplayRenewal.Renew(text, cut, null);
        SelfChecks.Check(forced.Kind == ReplayRenewal.Kind.Tail && forced.Divergence == cut && forced.Kept == cut
            && forced.Replay.Renewals is [{ FromStep: cut }]
            && forced.Replay.Game.Actions.Take(cut).SequenceEqual(original.Game.Actions.Take(cut))
            && forced.Replay.Game.Actions.Count == forced.Replay.Game.LegalShas.Count
            && forced.Replay.Game.Seed == original.Game.Seed && forced.Replay.PlayerDeck != null,
            "--from-step keeps the recorded steps before it and records the seed of the tail");

        // A step that is no longer legal: the tail starts exactly there.
        var broken = JsonSerializer.Deserialize<TransportReplay>(text, Program.Json);
        broken.Game.Actions[cut] = 100000;
        string brokenText = Dump(broken);
        var renewed = ReplayRenewal.Renew(brokenText, null, null);
        SelfChecks.Check(renewed.Divergence == cut && renewed.Kept == cut && renewed.Replay.Renewals is [{ FromStep: cut, Seed: not 0 }]
            && renewed.Replay.Game.Actions.Take(cut).SequenceEqual(original.Game.Actions.Take(cut))
            && renewed.Replay.Game.Actions[cut] != 100000 && renewed.Replay.Game.Turns > 0,
            "a replay whose step is no longer legal renews from that step");
        SelfChecks.Check(ReplayRenewal.Renew(brokenText, null, null).Text == renewed.Text && ReplayRenewal.Renew(brokenText, null, 7).Text != renewed.Text,
            "a renewal is deterministic for its seed and follows a different seed");

        // Legal moves that changed while the action number stays in range: the tail starts at the first drifted hash, not later.
        const int drift = 30;
        var drifted = JsonSerializer.Deserialize<TransportReplay>(text, Program.Json);
        drifted.Game.LegalShas[drift] = new string('0', 64);
        string driftText = Dump(drifted);
        var hashOnly = ReplayRenewal.Renew(driftText, null, null);
        SelfChecks.Check(hashOnly.Kind == ReplayRenewal.Kind.HashOnly && hashOnly.FirstHashDrift == drift && hashOnly.Text == null,
            "a game that plays identically with only legal-move hashes drifted is refused, not tail-renewed");
        drifted.Game.Actions[50] = drifted.Game.Actions[50] == 0 ? 1 : 0;
        var moved = ReplayRenewal.Renew(Dump(drifted), null, null);
        SelfChecks.Check(moved.Kind == ReplayRenewal.Kind.Tail && moved.Divergence == drift && moved.FirstHashDrift == drift && moved.Kept == drift,
            "a changed game with drifted legal moves renews from the first drift (here " + moved.Divergence + ")");
        SelfChecks.Check(Throws(() => ReplayRenewal.Renew(Dump(drifted), drift + 5, null)), "--from-step later than the first legal-move drift is refused");

        // A changed game with no drifted hash and no illegal action has no step to start from: say so and ask for --from-step.
        var tampered = JsonSerializer.Deserialize<TransportReplay>(text, Program.Json);
        tampered.EngineFingerprint = new string('0', 64);
        SelfChecks.Check(ThrowsWith(() => ReplayRenewal.Renew(Dump(tampered), null, null), "--from-step"), "a changed game with no step to start from asks for --from-step");
        tampered.EngineFingerprint = original.EngineFingerprint; tampered.FindingsSha = new string('1', 64);
        SelfChecks.Check(ThrowsWith(() => ReplayRenewal.Renew(Dump(tampered), null, null), "transport findings changed"), "a moved findings hash on an identical game is not 'nothing drifted'");

        // A renewed file replays and verifies as an ordinary pinned replay, is its own renewal, and a second tail keeps the first one's record.
        new Runner().Run(JsonSerializer.Deserialize<TransportReplay>(renewed.Text, Program.Json), verify: true);
        SelfChecks.Check(true, "a renewed replay passes the verifying replay");
        SelfChecks.Check(ReplayRenewal.Renew(renewed.Text, null, null) is { Kind: ReplayRenewal.Kind.Unchanged } again && again.Text == renewed.Text,
            "a renewed replay renews to its own bytes, record included");
        var twice = ReplayRenewal.Renew(renewed.Text, 100, 5);
        SelfChecks.Check(twice.Replay.Renewals is [{ FromStep: cut }, { FromStep: 100, Seed: 5 }], "a second tail keeps the first renewal's record");

        // The switch: no write without --write or --out, the file is replaced only with --write, a refused renewal writes nothing.
        string dir = Path.Combine(Path.GetTempPath(), "renew-replay-tail-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            string temp = Path.Combine(dir, "replay.json"); File.WriteAllText(temp, brokenText);
            ReplayRenewal.Run(new[] { "--renew-replay-tail", temp });
            SelfChecks.Check(File.ReadAllText(temp) == brokenText, "without --write nothing is written");
            string copy = Path.Combine(dir, "copy.json");
            ReplayRenewal.Run(new[] { "--renew-replay-tail", temp, "--out", copy });
            SelfChecks.Check(File.ReadAllText(temp) == brokenText && File.ReadAllText(copy) == renewed.Text, "--out writes a copy and leaves the source alone");
            ReplayRenewal.Run(new[] { "--renew-replay-tail", temp, "--write" });
            SelfChecks.Check(File.ReadAllText(temp) == renewed.Text, "--write replaces the file with the renewal");
            File.WriteAllText(temp, driftText);
            SelfChecks.Check(ReplayRenewal.Run(new[] { "--renew-replay-tail", temp, "--write" }) == 1 && File.ReadAllText(temp) == driftText, "a hash-only drift exits 1 and writes nothing");
        }
        finally { Directory.Delete(dir, true); }
        Console.WriteLine("PASS replay tail renewal");
    }

    private static bool ThrowsWith(Action action, string part) { try { action(); return false; } catch (ArgumentException error) { return error.Message.Contains(part, StringComparison.Ordinal); } }
    private static bool Throws(Action action) { try { action(); return false; } catch (ArgumentException) { return true; } }
}
