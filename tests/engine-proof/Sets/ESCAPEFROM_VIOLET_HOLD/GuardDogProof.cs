using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;
using static VioletHoldPoolProof;

internal static class GuardDogProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_878", (g, p, id) => {
        LocationProofs.Location(p);
        Summon(p, "CS2_182");
        var source = (Minion)Add(p, id);
        p.UsedMana = 0;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source, zonePosition: 1)), "middle play accepted");
        var right = Summon(p, "CS2_182");
        Core2026Proof.Check(p.BoardZone.SlotCount == 4, "no summon on play");
        var expected = OfflineCardPools.Pick(g.CloneExact(), VioletHoldPoolProof.Filter(id), p.HeroClass, source.Card.AssetId);
        Resume(g, next => {
            new ActivateDeathrattleTask(EntityType.SOURCE).Process(next, next.Player1, next.IdEntityDic[source.Id], null, new TaskStack());
            next.ProcessTasks(); next.DeathProcessingAndAuraUpdate();
        });
        var generated = SummonedResults(g, source, id, 1, 2).Single();
        Core2026Proof.Check(generated.Card.Id == expected.Id && right.ZonePosition == 4, "seeded summon right of live source, counting location");
        generated.Silence(); Kill(g, generated);
        Resume(g, next => Kill(next, (Minion)next.IdEntityDic[source.Id]));
        SummonedResults(g, source, id, 1, 1);
        Core2026Proof.Check(p.BoardZone.GetSlot(0) is BoardObject && p.BoardZone.GetSlot(3) == right, "middle death preserves location and right neighbor");
        Quiet(g, p);
        source = Summon(p, id); source.Silence();
        Kill(g, source);
        Core2026Proof.Check(p.BoardZone.SlotCount == 1, "Silence removes Deathrattle");
        source = Summon(p, id); Generic.ReturnToHandBlock(p, source);
        Core2026Proof.Check(p.BoardZone.SlotCount == 1, "source leaves without death, no summon");
        EmptyHand(p);
        foreach (int count in new[] { 0, 1 }) WithPool(g, id, count, () => {
            Quiet(g, p); source = Summon(p, id);
            long before = g.Random.DrawCount;
            Kill(g, source);
            SummonedResults(g, source, id, count, 1);
            Core2026Proof.Check(g.Random.DrawCount == before + count, "empty/singleton pool RNG");
        });
        Quiet(g, p); source = Summon(p, id);
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        long cursor = g.Random.DrawCount;
        new ActivateDeathrattleTask(EntityType.SOURCE).Process(g, p, source, null, new TaskStack());
        g.ProcessTasks(); g.DeathProcessingAndAuraUpdate();
        SummonedResults(g, source, id, 0, 0);
        Core2026Proof.Check(g.Random.DrawCount == cursor + 1 && p.BoardZone.SlotCount == 7, "full board prevents summon but consumes one pick");
        Kill(g, source); SummonedResults(g, source, id, 1, 1);
        Quiet(g, p);
        var left = Summon(p, "CS2_182"); source = Summon(p, id); var second = Summon(p, id); right = Summon(p, "CS2_182");
        Resume(g, next => {
            ((Minion)next.IdEntityDic[source.Id]).ToBeDestroyed = true;
            ((Minion)next.IdEntityDic[second.Id]).ToBeDestroyed = true;
            next.DeathProcessingAndAuraUpdate();
        });
        var firstOutput = SummonedResults(g, source, id, 1, 2).Single();
        var secondOutput = SummonedResults(g, second, id, 1, 3).Single();
        Core2026Proof.Check(p.BoardZone.GetSlot(0) is BoardObject && p.BoardZone.GetSlot(1) == left && p.BoardZone.GetSlot(4) == right,
            "simultaneous Deathrattles refill middle slots rightward, preserving location and neighbors");
        Quiet(g, p);
        int ownSlots = p.BoardZone.SlotCount;
        var enemy = Summon(p.Opponent, id);
        Kill(g, enemy); SummonedResults(g, enemy, id, 1, 0);
        Core2026Proof.Check(p.BoardZone.SlotCount == ownSlots, "enemy Deathrattle only summons for enemy");
        End(g);
    });

    private static void Quiet(Game g, Controller p)
    {
        foreach (var minion in p.BoardZone.ToArray()) minion.Silence();
        ClearBoard(g, p);
    }
}
