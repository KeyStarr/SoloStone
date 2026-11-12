using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class CleansingLightspawnProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_427", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_182"); var friend = Summon(p, "CS2_182"); TargetRules(g, p, id, enemy, friend, true);
        p.Hero.Damage = 10; var source = (Minion)Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Damage == 3 && p.Hero.Damage == 7 && source.HasLifeSteal, "Health-sized battlecry damage heals through Lifesteal");
        var buffed = (Minion)Add(p, id); buffed.BaseHealth = 6; buffed.Damage = 2; enemy.BaseHealth = 10;
        PlayEntity(g, p, buffed, enemy); Core2026Proof.Check(enemy.Damage == 7 && p.Hero.Damage == 3, "uses current buffed and damaged Health");
        enemy.HasDivineShield = true; Play(g, p, id, enemy);
        Core2026Proof.Check(!enemy.HasDivineShield && enemy.Damage == 7 && p.Hero.Damage == 3, "Shield blocks damage and Lifesteal");
        ClearBoard(g, p.Opponent); ClearBoard(g, p); Play(g, p, id); Core2026Proof.Check(p.BoardZone.Single().HasLifeSteal, "no enemy allows untargeted minion");
    });
}
