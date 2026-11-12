using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class CrystalMerchantProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_ULD_133", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 1 && source.Health == 4 && p.RemainingMana == 8, "printed stats and actual mana payment");
        foreach (string mode in new[] { "one", "zero", "temporary", "locked", "silenced", "dead", "hand" })
        {
            var clone = g.CloneExact(history: true); var owner = clone.Player1; var merchant = (Minion)clone.IdEntityDic[source.Id];
            owner.UsedMana = 9;
            if (mode == "zero" || mode == "temporary") owner.UsedMana = 10;
            if (mode == "temporary") owner.TemporaryMana = 1;
            if (mode == "locked") owner.OverloadLocked = 1;
            if (mode == "silenced") merchant.Silence();
            if (mode == "dead") Kill(clone, merchant);
            if (mode == "hand") { merchant.Zone.Remove(merchant); owner.HandZone.Add(merchant); }
            int hand = owner.HandZone.Count;
            bool draws = mode == "one" || mode == "temporary";
            End(clone);
            Core2026Proof.Check(owner.HandZone.Count == hand + (draws ? 1 : 0), "own end checks unspent mana and active board source: " + mode);
            int after = owner.HandZone.Count; End(clone);
            Core2026Proof.Check(owner.HandZone.Count == after + 1, "opponent end gives only ordinary start-turn draw: " + mode);
            if (draws) {
                after = owner.HandZone.Count; End(clone);
                Core2026Proof.Check(owner.HandZone.Count == after + 1, "permanent end trigger remains next turn: " + mode);
            }
            Core2026Proof.Check(p.HandZone.Count == 0 && p.BoardZone.Single() == source && p.RemainingMana == 8,
                "clone continuation never changes original");
        }
        End(g); Core2026Proof.Check(p.HandZone.Count == 1, "real own end draws one card");
    });
}
