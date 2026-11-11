using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;
using static CataclysmPoolProof;

internal static class DisposableAcolytesProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_499", (g, p, id) => {
        var left = Summon(p, "CS2_182");
        var source = Add(p, id);
        PoolPilotProof.Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[source.Id]));
        Generated(g, id, source, p.BoardZone.Skip(1), 2, Zone.PLAY);
        Core2026Proof.Check(p.BoardZone.GetSlot(0) == left, "spell summons append after existing physical slots");
        PutBoardAside(p);
        source = Add(p, id);
        PoolPilotProof.Resume(g, next => Generic.DiscardBlock(next.Player1, next.IdEntityDic[source.Id]));
        Generated(g, id, source, p.BoardZone, 2, Zone.PLAY);
        Core2026Proof.Check(source.Zone == p.GraveyardZone && p.LastCardDiscarded == source.Id, "actual discard resolves own summon effect");
        PutBoardAside(p);
        var waiting = Add(p, id); var other = Add(p, "CS2_182");
        Generic.DiscardBlock(p, other);
        Generic.DiscardBlock(p.Opponent, Add(p.Opponent, "CS2_182"));
        Core2026Proof.Check(p.BoardZone.IsEmpty && waiting.Zone == p.HandZone, "other friendly/opponent discards do not fire held Acolytes");
        WithPool(g, id, 0, () => {
            var rng = g.Random.CaptureState();
            Generic.DiscardBlock(p, waiting);
            Core2026Proof.Check(p.BoardZone.IsEmpty && g.Random.CaptureState().Equals(rng), "empty discard pool uses no RNG");
        });
        WithPool(g, id, 1, () => {
            source = Play(g, p, id);
            Generated(g, id, source, p.BoardZone, 2, Zone.PLAY);
            Core2026Proof.Check(p.BoardZone.Select(m => m.Card.Id).Distinct().Count() == 1, "independent summons may duplicate");
        });
        PutBoardAside(p);
        // A location consumes one of seven physical slots, despite not being a minion.
        var location = LocationProofs.Location(p, "CORE_REV_990", 0);
        for (int i = 0; i < 5; i++) Summon(p, "CS2_182");
        long before = g.Random.DrawCount;
        source = Play(g, p, id);
        Generated(g, id, source, p.BoardZone.Where(m => m[GameTag.DISPLAYED_CREATOR] == source.Id), 1, Zone.PLAY);
        Core2026Proof.Check(p.BoardZone.IsFull && p.BoardZone.GetSlot(0) == location && g.Random.DrawCount - before == 2, "one physical slot; two picks");
        before = g.Random.DrawCount; source = Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && g.Random.DrawCount - before == 2, "full board still samples both picks");
    });
}
