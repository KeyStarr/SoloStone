using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class AmberPriestessProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_431", (g, p, id) => {
        p.Hero.Damage = 10; var source = (Minion)Play(g, p, id, p.Hero);
        Core2026Proof.Check(p.Hero.Damage == 6 && source.HasTaunt, "heals friendly hero for current Health");
        var enemy = Summon(p.Opponent, "CS2_182"); enemy.BaseHealth = 12; enemy.Damage = 9;
        var buffed = (Minion)Add(p, id); buffed.BaseHealth = 7; buffed.Damage = 2; PlayEntity(g, p, buffed, enemy);
        Core2026Proof.Check(buffed.Health == 5 && enemy.Damage == 4, "restores exactly 5 current Health, not printed 4 or maximum 7");
        enemy.BaseHealth = 5; enemy.Damage = 2; Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Damage == 0 && enemy.Health == 5, "healing capped at target maximum Health");
        p.Opponent.Hero.Damage = 6; Play(g, p, id, p.Opponent.Hero); Core2026Proof.Check(p.Opponent.Hero.Damage == 2, "enemy hero legal");
        source.Damage = 1; Play(g, p, id, source); Core2026Proof.Check(source.Damage == 0, "friendly minion legal");
        Play(g, p, id, enemy); Core2026Proof.Check(enemy.Health == 5, "healthy target gains no maximum Health");
    });
}
