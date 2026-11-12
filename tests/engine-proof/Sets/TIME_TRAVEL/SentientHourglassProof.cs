using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class SentientHourglassProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_050", (g, p, id) => {
        var m = (Minion)Play(g, p, id); Core2026Proof.Check(m.IsRush && m.AttackDamage == 4 && m.Health == 9, "printed Rush and stats");
        m.HasDivineShield = true; Play(g, p, "CS2_008", m);
        Core2026Proof.Check(!m.HasDivineShield && m.AttackDamage == 4 && m.Health == 9, "absorbed damage does not swap");
        Play(g, p, "CS2_008", m); Core2026Proof.Check(m.AttackDamage == 8 && m.Health == 4 && m.Damage == 0, "surviving damage swaps current stats");
        var clone = g.CloneExact(history: true); var cm = (Minion)clone.IdEntityDic[m.Id]; Play(clone, clone.Player1, "CS2_008", cm);
        Core2026Proof.Check(cm.AttackDamage == 3 && cm.Health == 8 && m.AttackDamage == 8 && m.Health == 4, "clone trigger isolated");
        End(g); var enemyBody = Summon(p.Opponent, "CS2_182"); Play(g, p.Opponent, "CS2_008", enemyBody);
        Core2026Proof.Check(m.AttackDamage == 8 && m.Health == 4, "other minion damage does not trigger");
        Play(g, p.Opponent, "CS2_008", m); Core2026Proof.Check(m.AttackDamage == 3 && m.Health == 8, "enemy damage still swaps"); End(g);
        Play(g, p, "BE_228", m); Core2026Proof.Check(m.AttackDamage == 4 && m.Health == 9, "Silence removes swaps");
        Play(g, p, "CS2_008", m); Core2026Proof.Check(m.AttackDamage == 4 && m.Health == 8, "Silence removes trigger");
        Kill(g, m); var lethal = Summon(p, id); Play(g, p, "CS2_029", lethal); Play(g, p, "CS2_029", lethal);
        Core2026Proof.Check(lethal.Zone.Type == Zone.GRAVEYARD, "lethal damage does not rescue source by swapping");
    });
}
