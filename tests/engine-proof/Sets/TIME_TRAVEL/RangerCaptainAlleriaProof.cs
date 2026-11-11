using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;
using static RangerGeneralSylvanasProof;

internal static class RangerCaptainAlleriaProof
{
    internal static void Run() => CompanionCase("TIME_609t1", (g, p) => {
        var drawn = p.HandZone.Single(); PlayEntity(g, p, drawn);
        var filter = OfflinePoolFilter.Discover(type: CardType.SPELL);
        var candidates = OfflineCardPools.Candidates(g, p.HeroClass, filter, drawn.Card.AssetId);
        Core2026Proof.Check(candidates.Count >= 3 && p.Choice.Choices.Count == 3
            && p.Choice.Choices.Select(x => g.IdEntityDic[x].Card.Id).Distinct().Count() == 3
            && p.Choice.Choices.All(x => candidates.Any(c => c.Id == g.IdEntityDic[x].Card.Id)), "distinct admitted Hunter/neutral spell offers");
        int chosen = p.Choice.Choices[1];
        PlayerDecisionProof.Answer(g, chosen);
        Core2026Proof.Check(p.HandZone.Single().Id == chosen && p.Choice == null, "zero partners grants exactly selected card");
        PoolPilotProof.EmptyHand(p); ParkBoard(p);
        End(g); Play(g, p.Opponent, "TIME_609"); Play(g, p.Opponent, "TIME_609t2"); End(g);
        Play(g, p, "TIME_609t1"); AnswerAll(g, 1);
        ParkBoard(p); PoolPilotProof.EmptyHand(p);
        Play(g, p, "TIME_609"); ParkBoard(p);
        Play(g, p, "TIME_609t1"); AnswerAll(g, 2);
        Core2026Proof.Check(p.HandZone.Count == 2, "one friendly distinct partner gives two spells");
        ParkBoard(p); PoolPilotProof.EmptyHand(p);
        Play(g, p, "TIME_609"); Play(g, p, "TIME_609t2"); ParkBoard(p);
        Play(g, p, "TIME_609t1");
        var clone = g.CloneExact(history: true); var restored = Game.LoadSnapshot(g.SaveSnapshot());
        AnswerAll(clone, 3); AnswerAll(restored, 3);
        Core2026Proof.Check(p.HandZone.IsEmpty && p.Choice != null, "suspended clone leaves original choice intact");
        AnswerAll(g, 3);
        Core2026Proof.Check(p.HandZone.Count == 3 && Canonical.State(g) == Canonical.State(clone)
            && Canonical.State(g) == Canonical.State(restored), "duplicate Sylvanas counts once; three sequential decisions survive snapshot");
        ParkBoard(p); PoolPilotProof.EmptyHand(p);
        while (p.HandZone.Count < 9) Add(p, "CS2_182");
        Play(g, p, "TIME_609t1"); AnswerAll(g, 3);
        Core2026Proof.Check(p.HandZone.Count == 10, "all repetitions resolve at hand capacity without overflow");
        ParkBoard(p); PoolPilotProof.EmptyHand(p);
        PoolPilotProof.Pool(g, "TIME_609t1", 0, () => {
            var rng = g.Random.CaptureState();
            Play(g, p, "TIME_609t1");
            Core2026Proof.Check(p.Choice == null && p.HandZone.IsEmpty && rng.Equals(g.Random.CaptureState()), "empty pool consumes no RNG and opens no choice");
        });
        ParkBoard(p); Summon(p, "LOE_077");
        Play(g, p, "TIME_609t1"); AnswerAll(g, 6);
        Core2026Proof.Check(p.HandZone.Count == 6, "Brann repeats entire three-Discover Battlecry sequentially");
        FullBoardRejects(g, p, "TIME_609t1");
    });
}
