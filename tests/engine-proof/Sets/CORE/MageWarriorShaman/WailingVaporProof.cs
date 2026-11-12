using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class WailingVaporProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_WC_042", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 1 && source.Health == 3 && source.IsRace(Race.ELEMENTAL), "1/3 Elemental does not trigger on self play");
        Summon(p, "CS2_033"); Play(g, p, "CS2_182");
        Core2026Proof.Check(source.AttackDamage == 1, "summoning Elemental and playing non-Elemental do not trigger");
        var handVapor = (Minion)Add(p, id);
        var clone = g.Clone(); Play(clone, clone.Player1, "CORE_DRG_107");
        Core2026Proof.Check(((Minion)clone.IdEntityDic[source.Id]).AttackDamage == 2 && source.AttackDamage == 1, "dual-type Elemental triggers only clone");
        Play(g, p, "CORE_DRG_107");
        Core2026Proof.Check(source.AttackDamage == 2 && handVapor.AttackDamage == 1, "played dual-type Elemental buffs board Vapor only");
        End(g); Play(g, p.Opponent, "CORE_DRG_107");
        Core2026Proof.Check(source.AttackDamage == 2, "opponent Elemental excluded"); End(g);
        Core2026Proof.Check(source.AttackDamage == 2, "buff persists at end of turn");
        source.Silence(); Play(g, p, "CORE_DRG_107");
        Core2026Proof.Check(source.AttackDamage == 1, "Silence removes buff and trigger");
        Kill(g, source); var other = Summon(p, id); Kill(g, other);
        Play(g, p, "CORE_DRG_107"); Core2026Proof.Check(handVapor.AttackDamage == 1 && other.AttackDamage == 1, "removed unsilenced source no longer gains Attack");
    });
}
