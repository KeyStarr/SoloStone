using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;

internal static class SeedingDragonProof
{
    internal static void Run() => StandardSetsProof.Case("MEND_045", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.HasTaunt && p.HandZone.IsEmpty, "Taunt and no Battlecry generation");
        Resume(g, next => Kill(next, (Minion)next.IdEntityDic[source.Id]));
        CataclysmPoolProof.Generated(g, id, source, p.HandZone, 1, Zone.HAND, discount: 2);
        Core2026Proof.Check(p.HandZone[0].Card.IsRace(Race.DRAGON), "Deathrattle generates Dragon");
        var generated = (Minion)p.HandZone[0];
        // Two full rounds: past the Kindred window opened by playing Seeding Dragon (Windpeak Wyrm costs (2) less there).
        End(g); End(g); End(g); End(g);
        Core2026Proof.Check(generated.Cost == Math.Max(0, generated.Card.Cost - 2), "discount persists across turns");
        p.HandZone.Remove(generated); SabberStoneCore.Actions.Generic.SummonBlock(g, generated, -1, source);
        generated.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(generated.Cost == generated.Card.Cost, "Silence clears generated minion discount");
        CataclysmPoolProof.PutBoardAside(p); EmptyHand(p);
        CataclysmPoolProof.WithPool(g, id, 0, () => {
            source = Summon(p, id); var rng = g.Random.CaptureState(); Kill(g, source);
            Core2026Proof.Check(p.HandZone.IsEmpty && rng.Equals(g.Random.CaptureState()), "empty pool still kills source without generation/RNG");
        });
        CataclysmPoolProof.WithPool(g, id, 1, () => {
            source = Summon(p, id); Kill(g, source);
            CataclysmPoolProof.Generated(g, id, source, p.HandZone, 1, Zone.HAND, discount: 2);
            Core2026Proof.Check(p.HandZone[0].Card.Cost <= 2 && p.HandZone[0].Cost == 0, "discount floors at zero");
        });
        EmptyHand(p); FillHand(p, 9); source = Summon(p, id); Kill(g, source);
        Core2026Proof.Check(p.HandZone.Count == 10, "last hand slot gets discounted Dragon");
        var before = g.Random.DrawCount; source = Summon(p, id); Kill(g, source);
        Core2026Proof.Check(p.HandZone.Count == 10 && g.Random.DrawCount - before == 1, "full hand consumes pick without overflow");
        EmptyHand(p); source = Summon(p, id); source.Silence(); Kill(g, source);
        Core2026Proof.Check(p.HandZone.IsEmpty, "Silence removes source Deathrattle");
        source = Summon(p, id); p.SetasideZone.Add(p.BoardZone.Remove(source)); End(g); End(g);
        Core2026Proof.Check(!p.HandZone.Any(c => c[GameTag.DISPLAYED_CREATOR] == source.Id), "leaving play without dying never generates");
        EmptyHand(p); int enemyHand = p.Opponent.HandZone.Count;
        var enemy = Summon(p.Opponent, id); Kill(g, enemy);
        Core2026Proof.Check(p.HandZone.IsEmpty && p.Opponent.HandZone.Count == enemyHand + 1, "opponent death generates only for opponent");
    });
}
