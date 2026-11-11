using SabberStoneCore.Enums;
using static Core2026Proof;
using static StandardSetsProof;
using static CataclysmPoolProof;

internal static class LifebloomProof
{
    internal static void Run() => StandardSetsProof.Case("MEND_042", (g, p, id) => {
        var friendly = Summon(p, "CS2_200"); var enemy = Summon(p.Opponent, "CS2_200");
        friendly.Damage = 5; enemy.Damage = 5; p.Hero.Damage = 12; p.Opponent.Hero.Damage = 12;
        var source = Add(p, id);
        PoolPilotProof.Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[source.Id]));
        Core2026Proof.Check(friendly.Damage == 0 && p.Hero.Damage == 4 && enemy.Damage == 5 && p.Opponent.Hero.Damage == 12,
            "heal all friendly characters by eight capped at maximum; opponents untouched");
        Generated(g, id, source, p.BoardZone.Where(m => m[GameTag.DISPLAYED_CREATOR] == source.Id), 2, Zone.PLAY);
        Core2026Proof.Check(p.BoardZone.GetSlot(0) == friendly, "spell summons append");
        PutBoardAside(p);
        WithPool(g, id, 0, () => {
            long rng = g.Random.DrawCount; p.Hero.Damage = 10; Play(g, p, id);
            Core2026Proof.Check(p.BoardZone.IsEmpty && p.Hero.Damage == 2 && g.Random.DrawCount == rng, "heal still works with empty pool");
        });
        WithPool(g, id, 1, () => {
            source = Play(g, p, id);
            Generated(g, id, source, p.BoardZone, 2, Zone.PLAY);
            Core2026Proof.Check(p.BoardZone.Select(m => m.Card.Id).Distinct().Count() == 1 && p.Hero.Damage == 0, "duplicate independent picks; undamaged boundary");
        });
        PutBoardAside(p); LocationProofs.Location(p);
        for (int i = 0; i < 5; i++) Summon(p, "CS2_182");
        long before = g.Random.DrawCount; source = Play(g, p, id);
        Generated(g, id, source, p.BoardZone.Where(m => m[GameTag.DISPLAYED_CREATOR] == source.Id), 1, Zone.PLAY);
        Core2026Proof.Check(p.BoardZone.IsFull && g.Random.DrawCount - before == 2, "location counts toward capacity; both picks sampled");
        before = g.Random.DrawCount; p.Hero.Damage = 10; Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.Hero.Damage == 2 && g.Random.DrawCount - before == 2, "full board still heals and samples");
    });
}
