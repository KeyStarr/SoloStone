using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using static Core2026Proof;
using static DarkGiftConsumerProof;

internal static class DragonTurtleProof
{
    internal static void Run() => StandardSetsProof.Case("FIR_956", (g, p, id) => {
        Core2026Proof.Check(Cards.FromId(id).IsRace(Race.BEAST) && Cards.FromId(id).IsRace(Race.DRAGON), "both printed tribes");
        Play(g, p, id); Core2026Proof.Check(p.Hero.AttackDamage == 0 && p.Hero.Armor == 0, "empty hand: neither reward"); Clear(p);
        InvalidHoldings(p); Play(g, p, id);
        Core2026Proof.Check(p.Hero.AttackDamage == 0 && p.Hero.Armor == 0, "only held gifted minions count"); Clear(p);
        var self = Gift(p, id); PlayEntity(g, p, self);
        Core2026Proof.Check(p.Hero.AttackDamage == 0 && p.Hero.Armor == 0, "gifted played source no longer in hand"); Clear(p);
        Gift(p); Play(g, p, id);
        Core2026Proof.Check(p.Hero.AttackDamage == 3 && p.Hero.Armor == 6 && p.Hero.AppliedEnchantments.Any(e => e.Card.Id == "FIR_956e"),
            "exact hero Attack and Armor");
        Core2026Proof.Check(Cards.FromId("FIR_956e").AssetId == 122044 && Cards.FromId("FIR_956e").Text == "+3 Attack this turn.", "exact attachment");
        PoolPilotProof.Resume(g, next => {
            End(next);
            Core2026Proof.Check(next.Player1.Hero.AttackDamage == 0 && next.Player1.Hero.Armor == 6, "Attack expires, Armor persists after clone/snapshot");
        });
    });
}
