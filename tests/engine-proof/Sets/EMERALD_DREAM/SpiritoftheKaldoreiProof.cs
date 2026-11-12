using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static EmeraldDreamBatch3Proof;

internal static class SpiritoftheKaldoreiProof
{
    internal static void Run() => StandardSetsProof.Case("FIR_777", (g, p, id) => {
        var first = (Minion)Play(g, p, id);
        Core2026Proof.Check(first.AttackDamage == 1 && first.Health == 3 && first.HasTaunt && first.HasLifeSteal, "no power: base stats and keywords");
        UsePower(g, p); var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 4 && source.Health == 6, "power this turn grants +3/+3");
        Core2026Proof.Check(source.AppliedEnchantments.Single().Card.Id == "FIR_777e2", "exact +3/+3 enchantment");
        source.Silence(); Core2026Proof.Check(source.AttackDamage == 1 && source.Health == 3 && !source.HasTaunt && !source.HasLifeSteal, "buff and keywords removed by Silence");
        End(g); UsePower(g, p.Opponent); End(g);
        var next = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.HeroPowerActivationsThisTurn == 0 && next.AttackDamage == 1 && next.Health == 3, "last turn and opponent powers do not qualify");
    });
}
