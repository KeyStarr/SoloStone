namespace TransportInvariants;

internal static class PrivateReturnCorpus
{
    // A focused slice of the strict corpus for the Godfrey private-return aliasing. Deck 31 is the stress deck that
    // holds Godfrey. Default: deck 1 against deck 31, seeds 0 to 9, the pairing where seeds 2, 5 and 6 failed. `sample`
    // adds the 200 fuzz games; `deck31` takes every game with deck 31 on either side. Run it after touching how
    // NativeMatch aliases returned cards or resends them after a Rewind reset, instead of the whole corpus.
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
