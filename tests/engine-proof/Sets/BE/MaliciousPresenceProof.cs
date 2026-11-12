using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class MaliciousPresenceProof
{
    internal static void Run() => StandardSetsProof.Case("BE_325", (g, p, id) => {
        var spell = Add(p, id);
        Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == spell.Id), "minion target required on empty board");
        p.HandZone.Remove(spell); p.SetasideZone.Add(spell);
        p.Hero.Damage = 10; p.Opponent.Hero.Damage = 10;
        var friendly = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        TargetRules(g, p, id, enemy, friendly, false);
        enemy.Damage = 2;
        Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Zone == p.Opponent.GraveyardZone && p.Opponent.Hero.Damage == 7 && p.Hero.Damage == 10,
            "destroy enemy and heal its owner by current three Health");
        Summon(p, "EX1_012");
        Play(g, p, id, friendly);
        Core2026Proof.Check(friendly.Zone == p.GraveyardZone && p.Hero.Damage == 5, "friendly target heals self; Spell Damage never scales healing");
        enemy = Summon(p.Opponent, "CS2_182"); p.Opponent.Hero.Damage = 1; enemy.HasDivineShield = true;
        Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Zone == p.Opponent.GraveyardZone && p.Opponent.Hero.Damage == 0, "destroy bypasses Divine Shield, healing capped at full");
        p.Opponent.Hero.Damage = 8;
        enemy = Summon(p.Opponent, "BE_861"); enemy.Damage = 4;
        Play(g, p, id, enemy);
        var reborn = p.Opponent.BoardZone.Single(m => m.Card.Id == "BE_861");
        Core2026Proof.Check(p.Opponent.Hero.Damage == 4 && reborn.Health == 1 && reborn.Id != enemy.Id,
            "captures one current Health before destruction; deathrattle and Reborn still resolve");
    });
}
