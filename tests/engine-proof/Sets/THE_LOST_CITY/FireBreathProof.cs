using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class FireBreathProof
{
    internal static void Run() => StandardSetsProof.Case("DINO_406", (g, p, id) => {
        var elemental = Summon(p, "DINO_413"); var ordinary = Summon(p, "CS2_182");
        var enemy = Summon(p.Opponent, "DINO_413");
        Play(g, p, id, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 4 && elemental.AttackDamage == 4 && elemental.BaseHealth == 5
            && ordinary.AttackDamage == 4 && enemy.AttackDamage == 3, "four to hero, buffs friendly secondary Elemental only");
        var hand = Add(p, "DINO_413"); Play(g, p, "BE_228", elemental);
        Core2026Proof.Check(elemental.AttackDamage == 3 && elemental.BaseHealth == 4, "Silence removes pinned +1/+1");
        var power = Summon(p, "EX1_012"); Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Zone == p.Opponent.GraveyardZone && ((Minion)hand).AttackDamage == 3, "spell power increases damage; hand not buffed");
        Play(g, p, id, elemental); Core2026Proof.Check(elemental.Zone == p.GraveyardZone, "lethally damaged friendly Elemental dies before buff");
        Play(g, p, id, p.Hero); Core2026Proof.Check(p.Hero.Damage == 5, "friendly hero target and spell damage");
    });
}
