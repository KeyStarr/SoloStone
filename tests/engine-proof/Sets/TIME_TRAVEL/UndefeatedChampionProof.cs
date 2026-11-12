using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;
using static TimeTravelPoolProof;
using static PoolPilotProof;

internal static class UndefeatedChampionProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_872", (g, p, id) => {
        var enemy = p.Opponent;
        var location = Entity.FromCard(enemy, Cards.FromId("CORE_REV_990")); enemy.BoardZone.Add((SabberStoneCore.Model.Entities.BoardObject)location);
        var existing = Summon(enemy, "CS2_182");
        var source = (Minion)Play(g, p, id);
        var outputs = enemy.BoardZone.Where(m => m.Id != existing.Id).OrderBy(m => m.Id).ToArray();
        RandomGenerated(g, source, outputs, 5, enemy);
        Core2026Proof.Check(enemy.BoardZone.SlotCount == 7 && p.BoardZone.SlotCount == 1 && source.IsRush &&
            outputs.Select(m => m.ZonePosition).SequenceEqual(Enumerable.Range(2, 5)), "opponent slots filled in creation order; printed Rush");
        source.Silence(); Core2026Proof.Check(!source.IsRush && source.IsExhausted, "silence-readiness removes printed Rush");
        long draws = g.Random.DrawCount; Play(g, p, id);
        Core2026Proof.Check(enemy.BoardZone.SlotCount == 7 && g.Random.DrawCount == draws, "already-full board consumes no picks");
        ClearQuietly(g, p); ClearQuietly(g, enemy);
        foreach (int count in new[] { 0, 1 }) WithPool(g, id, count, () => {
            long before = g.Random.DrawCount; source = (Minion)Play(g, p, id);
            Core2026Proof.Check(enemy.BoardZone.SlotCount == (count == 0 ? 0 : 7) && g.Random.DrawCount == before + (count == 0 ? 0 : 7),
                $"empty/singleton opponent fill: candidates={count}, slots={enemy.BoardZone.SlotCount}, picks={g.Random.DrawCount-before}, ids={string.Join(",",enemy.BoardZone.Select(m=>m.Card.Id))}");
            if (count == 1) RandomGenerated(g, source, enemy.BoardZone, 7, enemy);
            ClearQuietly(g, p); ClearQuietly(g, enemy);
        });
        var pending = Add(p, id);
        Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[pending.Id]));
        RandomGenerated(g, pending, enemy.BoardZone, 7, enemy);
    });
}
