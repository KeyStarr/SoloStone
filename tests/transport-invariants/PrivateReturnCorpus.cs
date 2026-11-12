namespace TransportInvariants;

internal static class PrivateReturnCorpus
{
    internal static int Run(bool sample = false, bool deck31 = false)
    {
        var runner = new Runner();
        var cases = Runner.Cases("standard-batch-1", 9).Where(r =>
            deck31 ? r.Game.Player == "standard-batch-1-31" || r.Game.Opponent == "standard-batch-1-31"
                : r.Game.Player == "standard-batch-1-1" && r.Game.Opponent == "standard-batch-1-31");
        if (sample) cases = cases.Concat(Runner.Fuzz());
        foreach (var replay in cases)
        {
            int before = runner.ViolationCount;
            try { runner.Run(replay); }
            catch (Exception error) { runner.Failures++; Console.Error.WriteLine("FAIL " + replay.Game.Id + ": " + error); }
            Console.WriteLine($"CASE {replay.Game.Id}: violations={runner.ViolationCount - before}");
        }
        foreach (var bucket in runner.Buckets.Values)
            Console.WriteLine($"BUCKET {bucket.Card} {bucket.Tag} expected={bucket.Expected} shown={bucket.Shown} count={bucket.Occurrences} {bucket.Example}");
        bool shapes = HistoryShapeReport.Write(false);
        Program.Save($"{Runner.Output}/private-return-report.json", new { runner.Completed, runner.Failures,
            violations = runner.ViolationCount, buckets = runner.Buckets.Values });
        int code = Program.ResultCode(runner, true, shapes);
        Console.WriteLine($"private-return corpus: games={runner.Completed.Values.Sum()}; violations={runner.ViolationCount}; failures={runner.Failures}; exit={code}");
        return code;
    }
}
