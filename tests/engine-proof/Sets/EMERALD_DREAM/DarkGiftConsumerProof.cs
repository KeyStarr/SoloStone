using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class DarkGiftConsumerProof
{
    internal static IPlayable Gift(Controller p, string id = "CS2_182", Zone zone = Zone.HAND)
    {
        var m = Entity.FromCard(p, Cards.FromId(id), zone: zone == Zone.HAND ? p.HandZone :
            zone == Zone.DECK ? p.DeckZone : p.BoardZone);
        Core2026Proof.Check(DarkGifts.Give(m, p.Hero) && DarkGifts.Has(m), "real gift fixture");
        return m;
    }

    // D5 / data/rules-evidence/dark-gift/dg16a-holding-hand-only.yaml and dg16b-attached-gift-identity.yaml.
    internal static void InvalidHoldings(Controller p)
    {
        Add(p, "CS2_182")[GameTag.HAS_DARK_GIFT] = 1;
        Add(p, "CS2_029")[GameTag.HAS_DARK_GIFT] = 1;
        Gift(p, zone: Zone.PLAY); Gift(p, zone: Zone.DECK); Gift(p.Opponent);
        var silenced = (Minion)Gift(p, zone: Zone.PLAY);
        silenced.Silence();
        Generic.AddHandPhase(p, p.BoardZone.Remove(silenced));
        Core2026Proof.Check(!p.HandZone.Any(DarkGifts.Has), "flag alone/silenced gift never satisfies Has");
    }

    internal static void Clear(Controller p)
    {
        foreach (var slot in Enumerable.Range(0, p.BoardZone.SlotCount).Select(p.BoardZone.GetSlot).ToArray())
        {
            Generic.RemoveFromZone(p, slot);
            p.SetasideZone.Add(slot);
        }
        PoolPilotProof.EmptyHand(p);
    }
}
