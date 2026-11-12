using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class SigilofCinderProof
{
    internal static void Run() => StandardSetsProof.Case("FIR_902", (g, p, id) => {
        p.Opponent.Hero.Armor = 100;
        var power = Summon(p, "CS2_052");
        Play(g, p, id);
        Kill(g, power);
        Core2026Proof.Check(p.Opponent.Hero.Armor == 100, "no immediate damage");
        End(g); Core2026Proof.Check(p.Opponent.Hero.Armor == 100, "opponent start does not trigger");
        Summon(p, "CS2_052");
        var clone = g.Clone(); End(clone);
        Core2026Proof.Check(clone.Player2.Hero.Armor == 93 && p.Opponent.Hero.Armor == 100, "spell power evaluated at delayed resolution, clone independent");
        var enemy = Summon(p.Opponent, "CS2_182"); enemy.BaseHealth = 100;
        End(g);
        Core2026Proof.Check(100 - p.Opponent.Hero.Armor + enemy.Damage == 7, "seven total one-damage random missiles");
        int total = p.Opponent.Hero.Armor - enemy.Damage;
        End(g); End(g);
        Core2026Proof.Check(p.Opponent.Hero.Armor - enemy.Damage == total, "sigil fires once");
    });
}
