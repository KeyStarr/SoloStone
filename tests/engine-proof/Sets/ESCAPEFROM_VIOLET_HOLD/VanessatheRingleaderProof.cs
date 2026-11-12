using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;
using static VioletHoldPoolProof;

internal static class VanessatheRingleaderProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_407", (g, p, id) => {
        var source = Add(p, id); p.BaseMana = 1;
        Core2026Proof.Check(source[GameTag.PREPARE] == 1 && g.Process(PrepareCardTask.Any(p, source))
            && source.Cost == 5 && source[GameTag.PREPARING] == 1, "printed Prepare discount and lock");
        Resume(g, next => End(next));
        Core2026Proof.Check(source[GameTag.PREPARING] == 0 && source.Cost == 5, "Prepare lock expires");
        End(g); PlayEntity(g, p, source);
        Core2026Proof.Check(!p.HandZone.Any(c => c[GameTag.DISPLAYED_CREATOR] == source.Id), "Vanessa never triggers on herself");
        EmptyHand(p);
        Core2026Proof.Check(!p.HandZone.Any(), "Vanessa does not trigger on her own play");
        var expected = OfflineCardPools.Pick(g.CloneExact(), VioletHoldPoolProof.Filter(id), p.HeroClass, source.Card.AssetId);
        Resume(g, next => Play(next, next.Player1, "GAME_005"));
        var result = HandResults(g, source, 1, reduction: 2).Single();
        Core2026Proof.Check(result.Card.Id == expected.Id, "seeded Battlecry minion after spell");
        Resume(g, next => Core2026Proof.Check(next.IdEntityDic[result.Id].Cost == Math.Max(0, result.Card.Cost - 2), "discount survives clone/snapshot"));
        EmptyHand(p); Play(g, p, "CS2_120"); HandResults(g, source, 1, reduction: 2);
        EmptyHand(p); End(g); Play(g, p.Opponent, "GAME_005");
        Core2026Proof.Check(!p.HandZone.Any(c => c[GameTag.DISPLAYED_CREATOR] == source.Id), "opponent card does not trigger Vanessa");
        End(g); EmptyHand(p);
        Core2026Proof.Check(p.HandZone.Count == 0, "turn changes do not generate Battlecry cards");
        foreach (int count in new[] { 0, 1 }) WithPool(g, id, count, () => {
            EmptyHand(p); long before = g.Random.DrawCount;
            Play(g, p, "GAME_005"); HandResults(g, source, count, reduction: 2);
            Core2026Proof.Check(g.Random.DrawCount == before + count, "empty/singleton pool RNG");
        });
        foreach (int filled in new[] { 9, 10 })
        {
            EmptyHand(p); FillHand(p, filled);
            int graveyard = p.GraveyardZone.Count; long before = g.Random.DrawCount;
            source.Card.Power.Trigger.SingleTask.Process(g, p, source, null, new TaskStack());
            Core2026Proof.Check(p.HandZone.Count == 10 && p.GraveyardZone.Count == graveyard + filled - 9
                && g.Random.DrawCount == before + 1, "generation fills final hand slot or burns with full hand");
            HandResults(g, source, 10 - filled, reduction: 2);
        }
        EmptyHand(p); LocationProofs.Location(p); while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        Play(g, p, "GAME_005"); HandResults(g, source, 1, reduction: 2);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7, "full physical board still generates in hand");
        EmptyHand(p); ((Minion)source).Silence(); Play(g, p, "GAME_005");
        Core2026Proof.Check(p.HandZone.Count == 0, "Silence removes trigger");
        Kill(g, (Minion)source);
        var bounced = Summon(p, id); Generic.ReturnToHandBlock(p, bounced);
        Play(g, p, "GAME_005"); Core2026Proof.Check(p.HandZone.Count == 1 && p.HandZone.Single().Id == bounced.Id, "departed Vanessa cannot trigger");
        EmptyHand(p); source = Summon(p, id); Kill(g, (Minion)source);
        Play(g, p, "GAME_005"); Core2026Proof.Check(p.HandZone.Count == 0, "dead Vanessa cannot trigger");
    });
}
