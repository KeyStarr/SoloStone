using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class QuantumDestabilizerProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_060", (g, p, id) => {
        var m = (Minion)Play(g, p, id);
        Core2026Proof.Check(m.IsRace(Race.MECHANICAL), "Mech identity");
        Play(g, p, "CS2_008", m);
        Core2026Proof.Check(m.Damage == 2, "friendly spell damage doubled");
        End(g); Play(g, p.Opponent, "CS2_008", m); End(g);
        Core2026Proof.Check(m.Damage == 4, "opponent spell damage doubled across turns");
        var attacker = Summon(p.Opponent, "CS2_171");
        End(g); Attack(g, attacker, m); End(g);
        Core2026Proof.Check(m.Damage == 6, "combat damage doubled");
        var clone = g.CloneExact(history: true); Play(clone, clone.Player1, "CS2_008", (Minion)clone.IdEntityDic[m.Id]);
        Core2026Proof.Check(((Minion)clone.IdEntityDic[m.Id]).Damage == 8 && m.Damage == 6, "clone damage isolated");
        m.HasDivineShield = true; Play(g, p, "CS2_008", m);
        Core2026Proof.Check(!m.HasDivineShield && m.Damage == 6, "shield prevents all doubled damage");
        Play(g, p, "BE_228", m); Play(g, p, "CS2_008", m);
        Core2026Proof.Check(m.Damage == 7, "Silence removes damage multiplier");
        Kill(g, m); Core2026Proof.Check(m.ActivatedTrigger == null, "leaving play removes trigger");
        var gone = Summon(p, id); Kill(g, gone);
        Core2026Proof.Check(gone.ActivatedTrigger == null && gone.TakeDamage(p.Hero, 1) == 0, "unsilenced source leaving play removes trigger and cannot take damage");
    });
}
