using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class FluxRevenantProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_214", (g, p, id) => {
        var m = (Minion)Play(g, p, id); Core2026Proof.Check(m.HasTaunt, "printed Taunt");
        Play(g, p, "EX1_238", m); Core2026Proof.Check(m.AttackDamage == 3 && m.Health == 5 && m.Damage == 0, "friendly Nature damage replaced with buff");
        m.Damage = 4; Play(g, p, "CORE_EX1_238", m); Core2026Proof.Check(m.AttackDamage == 5 && m.Health == 2 && m.Damage == 4, "lethal Nature damage replaced; existing damage retained");
        m.TakeHeal(p.Hero, 4);
        var clone = g.CloneExact(history: true); Play(clone, clone.Player1, "EX1_238", (Minion)clone.IdEntityDic[m.Id]);
        Core2026Proof.Check(((Minion)clone.IdEntityDic[m.Id]).AttackDamage == 7 && m.AttackDamage == 5, "clone predamage isolated");
        Play(g, p, "CS2_024", m); Core2026Proof.Check(m.AttackDamage == 5 && m.Health == 3, "friendly non-Nature damage applies normally");
        m.TakeHeal(p.Hero, 3); End(g); Play(g, p.Opponent, "EX1_238", m); Core2026Proof.Check(m.AttackDamage == 5 && m.Health == 3, "enemy Nature damage applies normally"); End(g);
        Play(g, p, "BE_228", m); Core2026Proof.Check(m.AttackDamage == 1, "Silence removes buffs");
        m.Damage = 0; Play(g, p, "EX1_238", m); Core2026Proof.Check(m.AttackDamage == 1 && m.Health == 1, "Silence removes replacement trigger"); Kill(g, m);
        var immune = (Minion)Play(g, p, id);
        Play(g, p, "ICC_467", immune);
        Core2026Proof.Check(immune.IsImmune, "Deathspeaker grants Immune");
        Play(g, p, "EX1_238", immune);
        Core2026Proof.Check(immune.AttackDamage == 1 && immune.Health == 4 && immune.Damage == 0,
            "Immune prevents friendly Nature damage before Flux replacement");
        End(g); End(g);
        Core2026Proof.Check(!immune.IsImmune, "Deathspeaker immunity expires");
        Play(g, p, "EX1_238", immune);
        Core2026Proof.Check(immune.AttackDamage == 3 && immune.Health == 5 && immune.Damage == 0,
            "Flux replacement resumes after immunity expires");
    });
}
