using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class NozdormuBronzeAspectProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_473", (g, p, id) => {
        var plain = Summon(p, "CS2_182"); var shield = Summon(p, "EX1_008"); var enemy = Summon(p.Opponent, "CS2_182");
        var source = (Minion)Play(g, p, id);
        var clone = g.CloneExact(history: true); End(clone);
        Core2026Proof.Check(((Minion)clone.IdEntityDic[plain.Id]).HasDivineShield && !plain.HasDivineShield, "clone shield trigger isolated");
        End(g);
        Core2026Proof.Check(plain.HasDivineShield && plain.AttackDamage == 4 && source.HasDivineShield && source.AttackDamage == 4, "unshielded minions including self gain shield only");
        Core2026Proof.Check(shield.HasDivineShield && shield.AttackDamage == 4 && shield.Health == 4 && !enemy.HasDivineShield, "already shielded gets +3/+3; enemy unaffected");
        End(g); Core2026Proof.Check(shield.AttackDamage == 4, "opponent end does not trigger");
        End(g); Core2026Proof.Check(plain.AttackDamage == 7 && plain.Health == 8 && shield.AttackDamage == 7, "second friendly end buffs shields");
        End(g); source.Silence(); g.DeathProcessingAndAuraUpdate(); End(g); End(g);
        Core2026Proof.Check(plain.AttackDamage == 7, "Silence stops trigger; buffs persist");
        var removed = (Minion)Play(g, p, id); Kill(g, removed); End(g); End(g);
        Core2026Proof.Check(plain.AttackDamage == 7, "dead source stops trigger");
    });
}
