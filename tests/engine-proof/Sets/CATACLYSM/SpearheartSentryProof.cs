using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;
using static CataclysmPoolProof;

internal static class SpearheartSentryProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_474", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.HandZone.IsEmpty, "no Battlecry generation");
        PoolPilotProof.Resume(g, next => End(next));
        Generated(g, id, source, p.HandZone, 1, Zone.HAND, discount: 3);
        var card = p.HandZone.Single(); int cost = card.Cost;
        var copied = Generic.Copy(p, source, card, Zone.HAND);
        Core2026Proof.Check(copied.Id != card.Id && copied.Card.Id == card.Card.Id && copied.Cost == cost && copied[GameTag.COST] == cost,
            "hand copy retains permanent discount (Actions/Copy.cs copies modified cost)");
        End(g);
        Core2026Proof.Check(card.Cost == cost && copied.Cost == cost && p.HandZone.Count == 3, "discount persists next turn; opponent end produces no spell");
        PoolPilotProof.EmptyHand(p);
        WithPool(g, id, 0, () => {
            long before = g.Random.DrawCount; End(g);
            Core2026Proof.Check(p.HandZone.IsEmpty && g.Random.DrawCount == before, "empty pool creates no card/RNG");
        });
        End(g); PoolPilotProof.EmptyHand(p);
        WithPool(g, id, 1, () => {
            End(g);
            Generated(g, id, source, p.HandZone, 1, Zone.HAND, discount: 3);
        });
        End(g); PoolPilotProof.EmptyHand(p); PoolPilotProof.FillHand(p);
        long rng = g.Random.DrawCount; End(g);
        Core2026Proof.Check(p.HandZone.Count == 10 && g.Random.DrawCount - rng == 1, "full hand still picks and burns output");
        End(g); PoolPilotProof.EmptyHand(p); source.Silence(); g.DeathProcessingAndAuraUpdate(); End(g);
        Core2026Proof.Check(p.HandZone.IsEmpty, "Silence removes trigger");
        End(g); PoolPilotProof.EmptyHand(p); var dead = (Minion)Play(g, p, id); Kill(g, dead); End(g);
        Core2026Proof.Check(p.HandZone.IsEmpty, "source leaving play stops generation");
        End(g); PoolPilotProof.EmptyHand(p); Add(p, id); End(g);
        Core2026Proof.Check(p.HandZone.Count == 1 && p.HandZone.Single().Card.Id == id, "held source has no turn-end trigger");
    });
}
