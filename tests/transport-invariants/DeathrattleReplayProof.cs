using System.Text.Json;

namespace TransportInvariants;

// Replays a recorded game (standard-batch-1 deck 21 vs 25, seed s4) where Ravenous Felhunter has two Deathrattles: its
// own empty one and a granted one. An earlier fix pruned empty root Deathrattle TRIGGERs, so only one block was sent
// where the engine ran two, and the nested Lifesteal took the Deathrattle slot. Every engine Deathrattle must keep its
// own keyword-217 boundary (EffectIndex -1 when empty). The fingerprint pins state, history and RNG, so a change that
// alters the game fails loudly.
// Re-recorded for #123 (a mortally wounded minion healed above 0 before the death check survives): the game now differs from step 73 on; it still
// holds the Felhunter case and has no history-shape finding. The accepted TLC_100t1 exhaustion Deathrattle finding is witnessed by HistoryShapeProof's
// location stream cases (history-shape exemption condition streams), not by this game any more.
internal static class DeathrattleReplayProof
{
    internal static void Run()
    {
        const string path = "tests/transport-invariants/replays/deathrattle/standard-batch-1-standard-batch-1-21-vs-standard-batch-1-25-s4-random.json";
        var replay = JsonSerializer.Deserialize<TransportReplay>(File.ReadAllText(path), Program.Json);
        SelfChecks.Check(replay.PlayerDeck?.Cards.Length == 30 && replay.OpponentDeck?.Cards.Length == 30
            && !string.IsNullOrEmpty(replay.EngineFingerprint), "Felhunter replay pins decks and engine state/RNG");
        var runner = new Runner();
        var history = runner.Run(replay, verify: true);
        int felhunterBlocks = FelhunterDeathrattleBlocks(runner.LastGame);
        SelfChecks.Check(felhunterBlocks == 2, "Felhunter runs its own and its granted Deathrattle, each in its own keyword-217 block, with a Lifesteal heal inside: blocks=" + felhunterBlocks);
        SelfChecks.Check(runner.ViolationCount == 0 && runner.Completed.Values.Sum() == 1 && history.Findings.Count == 0,
            "Felhunter empty Deathrattle, granted Deathrattle and Lifesteal retain distinct keyword boundaries: "
            + string.Join("; ", history.Findings.Select(f => $"{f.Rule}@{f.Step}: {f.Card}/{f.BlockCard} {f.Detail}")));
        Console.WriteLine("PASS Felhunter exact replay: legal hashes/actions, state/RNG fingerprint, transport parity and history shape");
    }
    // Ravenous Felhunter (EDR_891) runs its own Deathrattle and a granted one (two keyword-217 TRIGGER blocks from the same entity) and a
    // Lifesteal heal (keyword 685) follows inside that window: the case the proof is about. Returns the number of Felhunter Deathrattle blocks
    // of the entity with most of them, or 0 when the case is absent.
    internal static int FelhunterDeathrattleBlocks(SabberStoneCore.Model.Game game)
    {
        var entries = game.PowerHistory.Full.ToList();
        var felhunters = game.IdEntityDic.Where(pair => pair.Value.Card?.Id == "EDR_891").Select(pair => pair.Key).ToHashSet();
        var starts = entries.Select((entry, index) => (entry, index)).Where(x => x.entry is SabberStoneCore.Kettle.PowerHistoryBlockStart
            { BlockType: SabberStoneCore.Enums.BlockType.TRIGGER, TriggerKeyword: 217 } start && felhunters.Contains(start.Source)).ToList();
        int best = 0;
        foreach (var group in starts.GroupBy(x => ((SabberStoneCore.Kettle.PowerHistoryBlockStart)x.entry).Source))
        {
            var indexes = group.Select(x => x.index).ToList();
            if (indexes.Count < 2) continue;
            bool lifesteal = entries.Skip(indexes[0]).Take(indexes[^1] - indexes[0] + 80).Any(entry => entry is SabberStoneCore.Kettle.PowerHistoryBlockStart
                { BlockType: SabberStoneCore.Enums.BlockType.TRIGGER, TriggerKeyword: 685 });
            if (lifesteal) best = Math.Max(best, indexes.Count);
        }
        return best;
    }
}
