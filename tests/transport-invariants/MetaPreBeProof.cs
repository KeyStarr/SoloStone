using System.Text.Json;

namespace TransportInvariants;

// Six immutable replays of stress-deck games from the pre-Black-Empire card pool, each the game that exposed one
// finding (details in meta-pre-be-shape-evidence.md). ice-barrier: a repeated cast installed an already-active
// Secret twice and crashed the game (the original 67 actions are kept, then extended to completion). TIME_876e: a
// held Shapeshifter's transform must not be read as a hand buff. CORE_BT_416, EDR_226, TIME_704: a held card becomes
// its Genn form and gets its old buff layer back, which is a restore, not a new buff. TLC_100t3: an exhausted
// location's Deathrattle arrives as its own block. A re-recorded replay must still pass its witness check below
// (TLC_100t3, TIME_876e).
internal static class MetaPreBeProof
{
    internal static int Run(string only = null)
    {
        int failures = 0;
        foreach (string name in new[] { "ice-barrier", "TIME_876e", "CORE_BT_416", "EDR_226", "TIME_704", "TLC_100t3" })
        {
            if (only != null && name != only) continue;
            try
            {
                var replay = JsonSerializer.Deserialize<TransportReplay>(File.ReadAllText(
                    $"tests/transport-invariants/replays/meta-pre-be/{name}.json"), Program.Json);
                var runner = new Runner();
                SelfChecks.Check(replay.PlayerDeck?.Cards.Length == 30 && replay.OpponentDeck?.Cards.Length == 30,
                    name + " retains original stress decks independently of registry growth");
                var shape = runner.Run(replay, verify: true);
                if (name == "TLC_100t3")
                    SelfChecks.Check(shape.Findings.Any(f => f.Rule == "SPLIT_EFFECT_BLOCK" && f.Card == name
                        && f.Detail == "sibling-POWER under=PLAY header=differs meta=DAMAGE"
                        && f.Conditions?.Contains(HistoryShapeCondition.ExhaustionDeathrattle) == true),
                        name + " retains the original exhaustion Deathrattle regression witness");
                if (name == "TIME_876e")
                    // The original regression: a held Shapeshifter (entity 17) turns into TIME_858 at step 99 (a definition reset, not a buff),
                    // and the classifier must not call it a hand buff. A re-record that drops the transform cannot pass.
                    SelfChecks.Check(runner.ChangeLog.Any(c => c.Step == 99 && c.Entity == 17 && c.Card == "TIME_858")
                        && !shape.Findings.Any(f => f.Rule == "HAND_BUFF_FX"),
                        name + " retains the original Shapeshifter transform witness (CHANGE_ENTITY 17 to TIME_858 at step 99, no HAND_BUFF_FX)");
                SelfChecks.Check(runner.Failures == 0 && runner.ViolationCount == 0, name + " transport clean");
                Console.WriteLine("PASS meta-pre-be replay " + name);
            }
            catch (Exception error) { failures++; Console.Error.WriteLine("FAIL meta-pre-be " + name + ": " + error); }
        }
        bool shapes = HistoryShapeReport.Write(false);
        return failures != 0 ? 2 : shapes ? 0 : 1;
    }
}
