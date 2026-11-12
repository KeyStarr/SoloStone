using System.Text.Json;
using HsSim;

namespace TransportInvariants;

internal static class TransportHarnessChecks
{
    internal static void Focused()
    {
        var runner = new Runner();
        var replay = Runner.Focused();
        runner.Run(replay);
        SelfChecks.Check(runner.LightspawnPlayed && !runner.LightspawnDetected && runner.ViolationCount == 0,
            "focused Lightspawn publishes effective attack without transport violations");
        string path = $"{Runner.Output}/{replay.Game.Id}.json";
        SelfChecks.Check(File.Exists(path) && File.ReadLines(Path.ChangeExtension(path, ".packets.jsonl")).Count() > 0,
            "focused replay retains packet evidence");
        runner.Run(replay, verify: true);
        replay.Game.LegalShas[0] = new string('0', 64);
        try { runner.Run(replay, verify: true); throw new Exception("tampered transport replay accepted"); }
        catch (InvalidOperationException error) when (error.Message.StartsWith("replay.legal:", StringComparison.Ordinal)) { }
        SelfChecks.Check(Program.ResultCode(runner, true, true) == 0, "strict clean transport succeeds");
        SelfChecks.Check(Program.ResultCode(runner, true, false) == 1, "strict history failure exits one");
        runner.Buckets.Add(("fixture", "ATK", "1", "0"), new Bucket { Occurrences = 1 });
        SelfChecks.Check(Program.ResultCode(runner, true, true) == 1
            && Program.ResultCode(runner, false, true) == 0, "strict violations control exit status");
        runner.Failures++;
        SelfChecks.Check(Program.ResultCode(runner, true, true) == 2, "transport replay failure exits two");
        Console.WriteLine("PASS transport replay tampering, packet evidence and strict exit checks");
    }

    internal static void Corpus(Runner runner)
    {
        foreach (var (suite, count) in new[] { ("classic", 320), ("modern", 40), ("fuzz", 200) })
            SelfChecks.Check(runner.Completed.GetValueOrDefault(suite) == count, "transport corpus count: " + suite);
        SelfChecks.Check(runner.Failures == 0 && runner.LightspawnPlayed && !runner.LightspawnDetected,
            "transport corpus completes and exercises repaired Lightspawn");
        SelfChecks.Check(runner.EntryCounts.GetValueOrDefault("CHANGE_ENTITY") > 0
            && runner.EntryCounts.GetValueOrDefault("HIDE_ENTITY") > 0, "transport corpus covers change and hide");
        var goldens = new Dictionary<string, GameRecord>();
        foreach (string name in new[] { "classic-source", "modern" })
        {
            using var document = JsonDocument.Parse(File.ReadAllText($"tests/goldens/{name}.json"));
            foreach (var row in document.RootElement.GetProperty("games").EnumerateArray())
            {
                var game = row.Deserialize<GameRecord>(Program.Json);
                goldens.Add(game.Id, game);
            }
        }
        foreach (var bucket in runner.Buckets.Values)
        {
            string path = bucket.Example.Replay;
            var replay = JsonSerializer.Deserialize<TransportReplay>(File.ReadAllText(path), Program.Json).Game;
            SelfChecks.Check(File.ReadLines(Path.ChangeExtension(path, ".packets.jsonl")).Count() >= bucket.Example.Packets,
                "representative replay retains every checkpoint packet");
            if (replay.Suite is "classic" or "modern")
                SelfChecks.Check(replay.Actions.SequenceEqual(goldens[replay.Id].Actions)
                    && replay.LegalShas.SequenceEqual(goldens[replay.Id].LegalShas), "transport keeps seeded golden actions/legal hashes");
        }
    }
}
