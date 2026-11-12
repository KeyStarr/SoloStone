using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;
using static EmeraldDreamBatch3Proof;

internal static class MonstrousMosquitoProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_816", (g, p, id) => {
        var held = (Minion)Add(p, id); var source = (Minion)Play(g, p, id);
        var ally = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        Core2026Proof.Check(source.IsRace(Race.BEAST), "printed Beast");
        var clone = g.Clone(); End(clone);
        Core2026Proof.Check(((Minion)clone.IdEntityDic[ally.Id]).AttackDamage == 5 && ally.AttackDamage == 4, "clone end-turn buff isolated");
        End(g);
        Core2026Proof.Check(ally.AttackDamage == 5 && source.AttackDamage == 1 && held.AttackDamage == 1 && enemy.AttackDamage == 4
            && ally.AppliedEnchantments.Single().Card.Id == "EDR_816e", "own end buffs only other friendly board minions with exact enchantment");
        End(g);
        Core2026Proof.Check(ally.AttackDamage == 5, "opponent end does not trigger; permanent buff persists");
        source.Silence(); End(g); End(g);
        Core2026Proof.Check(ally.AttackDamage == 5, "Silence removes source trigger");
        Kill(g, source); source = Summon(p, id); Kill(g, source); End(g); End(g);
        Core2026Proof.Check(ally.AttackDamage == 5, "dead source cannot buff");
        ally.Silence(); Core2026Proof.Check(ally.AttackDamage == 4, "recipient Silence removes permanent buff");
    });
}
