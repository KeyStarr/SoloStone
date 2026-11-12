using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class RavenousDevilsaurProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_829", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_200");
        TargetRules(g, p, id, enemy, friend, false);
        var source = (Minion)Play(g, p, id, friend);
        Core2026Proof.Check(friend.Zone == p.GraveyardZone && source.AttackDamage == 3 && source.Health == 3, "destroy friendly without Kindred and gain nothing");
        ClearBoard(g, p); Play(g, p, "CS2_171"); End(g); End(g); ClearBoard(g, p);
        enemy.Damage = 2; source = (Minion)Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Zone == p.Opponent.GraveyardZone && source.AttackDamage == 9 && source.Health == 8, "Kindred gains target current 6/5 stats before destruction");
        source.Silence(); Core2026Proof.Check(source.AttackDamage == 3 && source.Health == 3, "Silence removes gained stats");
        ClearBoard(g, p); End(g); End(g); End(g); End(g); var target = Summon(p.Opponent, "CS2_182"); Play(g, p, "CS2_171");
        source = (Minion)Play(g, p, id, target); Core2026Proof.Check(source.AttackDamage == 3 && source.Health == 3, "current-turn Beast insufficient");
    });
}
