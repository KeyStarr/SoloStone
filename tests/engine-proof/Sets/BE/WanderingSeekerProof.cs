using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class WanderingSeekerProof
{
    internal static void Run() => StandardSetsProof.Case("BE_723", (g, p, id) => {
        p.Opponent.Hero.Armor = 100;
        var source = (Minion)Play(g, p, id); Play(g, p, "CS2_182");
        Core2026Proof.Check(p.Opponent.Hero.Armor == 100, "noncombo no trigger");
        Play(g, p, "EX1_124", p.Opponent.Hero); Core2026Proof.Check(p.Opponent.Hero.Armor == 93, "Combo spell and three triggered damage");
        End(g); End(g); int armor = p.Opponent.Hero.Armor;
        Play(g, p, "EX1_134"); Core2026Proof.Check(p.Opponent.Hero.Armor == armor - 3, "first card Combo keyword triggers without active Combo effect");
        var enemy = Summon(p.Opponent, "CS2_182"); armor = p.Opponent.Hero.Armor;
        Play(g, p, "EX1_134", p.Opponent.Hero);
        Core2026Proof.Check((enemy.Health == 2 && p.Opponent.Hero.Armor == armor - 2) || (enemy.Health == 5 && p.Opponent.Hero.Armor == armor - 5), "random trigger selects only enemy minion or hero");
        var clone = g.Clone(); var cp = clone.Player1; ClearBoard(clone, cp.Opponent); int cloneArmor = cp.Opponent.Hero.Armor;
        Play(clone, cp, "EX1_124", cp.Opponent.Hero);
        Core2026Proof.Check(cp.Opponent.Hero.Armor == cloneArmor - 7, "clone combines four Eviscerate and three Seeker damage against sole enemy hero");
        Core2026Proof.Check(p.Opponent.Hero.Armor == (enemy.Health == 2 ? armor - 2 : armor - 5), "original unaffected by clone trigger");
        End(g); armor = p.Opponent.Hero.Armor; Play(g, p.Opponent, "EX1_124", p.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Armor == armor, "opponent Combo no trigger"); End(g);
        Play(g, p, "BE_228", source); armor = p.Opponent.Hero.Armor; int enemyHealth = enemy.Health;
        Play(g, p, "EX1_124", p.Opponent.Hero); Core2026Proof.Check(p.Opponent.Hero.Armor == armor - 4 && enemy.Health == enemyHealth, "silence removes own trigger");
        Kill(g, source); armor = p.Opponent.Hero.Armor;
        Play(g, p, "EX1_124", p.Opponent.Hero); Core2026Proof.Check(p.Opponent.Hero.Armor == armor - 4, "departed source does not trigger");
        var fresh = Summon(p, id); Kill(g, fresh); armor = p.Opponent.Hero.Armor;
        Play(g, p, "EX1_124", p.Opponent.Hero); Core2026Proof.Check(p.Opponent.Hero.Armor == armor - 4 && fresh.ActivatedTrigger == null, "death removes unsilenced source trigger");
    });
}
