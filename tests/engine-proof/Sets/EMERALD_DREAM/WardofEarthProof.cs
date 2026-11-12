using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;
using static EmeraldDreamBatch9Proof;

internal static class WardofEarthProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_060", (g, p, id) => {
        var left = Summon(p, "CS2_182"); LocationProofs.Location(p, position: 0);
        var source = Add(p, id);
        PoolPilotProof.Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[source.Id]));
        var token = p.BoardZone.Single(m => m.Id != left.Id);
        GeneratedBoard(g, source, new[] { token }, 5, 1);
        Core2026Proof.Check(p.Hero.Armor == 5 && token.HasTaunt && token.ZonePosition == 2 && p.BoardZone.GetSlot(0) is BoardObject,
            "five Armor and Taunt summon appended after physical slots");
        token.Silence(); Core2026Proof.Check(!token.HasTaunt, "Silence removes granted Taunt");
        foreach (int size in new[] { 0, 1, 2 })
        {
            foreach (var m in p.BoardZone.ToArray()) m.Silence(); ClearBoard(g, p);
            EmeraldDreamPoolProof.WithPool(g, id, size, () => {
                long before = g.Random.DrawCount; int armor = p.Hero.Armor;
                var spell = Play(g, p, id);
                GeneratedBoard(g, spell, p.BoardZone, 5, size == 0 ? 0 : 1);
                Core2026Proof.Check(p.Hero.Armor == armor + 5 && g.Random.DrawCount - before == (size == 0 ? 0 : 1), "empty/tiny pool retains Armor, exact RNG");
                Core2026Proof.Check(p.BoardZone.All(m => m.HasTaunt), "all generated minions receive Taunt");
            });
        }
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        long draws = g.Random.DrawCount; int previous = p.Hero.Armor;
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.Hero.Armor == previous + 5 && g.Random.DrawCount == draws + 1,
            "full physical board: no summon, Armor and random pick still resolve");
    });
}
