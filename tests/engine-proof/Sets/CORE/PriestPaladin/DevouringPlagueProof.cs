using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class DevouringPlagueProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_BAR_311", (g, p, id) => {
        var friendly = Summon(p, "CS2_182");
        var enemy = Summon(p.Opponent, "CS2_182"); enemy.BaseHealth = 30;
        p.Hero.Damage = 15;
        var plague = Play(g, p, id);
        Core2026Proof.Check(plague.HasLifeSteal && enemy.Damage == 4 && p.Hero.Damage == 11, "four one-damage missiles with Lifesteal");
        Summon(p, "EX1_012"); Play(g, p, id);
        Core2026Proof.Check(enemy.Damage == 9 && p.Hero.Damage == 6, "Spell Damage increases missile count, not individual damage");
        ClearBoard(g, p); ClearBoard(g, p.Opponent);
        friendly = Summon(p, "CS2_182"); p.Hero.Damage = 15;
        enemy = Summon(p.Opponent, "CS2_182"); enemy.HasDivineShield = true;
        Play(g, p, id);
        Core2026Proof.Check(enemy.Damage == 3 && !enemy.HasDivineShield && p.Hero.Damage == 12, "shield prevents one missile and its healing");
        ClearBoard(g, p.Opponent);
        var first = Summon(p.Opponent, "EX1_506a");
        var second = Summon(p.Opponent, "EX1_506a");
        var dormant = Summon(p.Opponent, "CS2_182"); dormant[GameTag.DORMANT] = 1;
        var clone = g.CloneExact(history: true); Play(clone, clone.Player1, id);
        Core2026Proof.Check(first.Zone == p.Opponent.BoardZone && p.Hero.Damage == 12, "clone random resolution leaves original untouched");
        Play(g, p, id);
        Core2026Proof.Check(first.Zone.Type == Zone.GRAVEYARD && second.Zone.Type == Zone.GRAVEYARD && dormant.Damage == 0, "reselects living enemies; Dormant excluded");
        Core2026Proof.Check(HsSim.Canonical.State(g) == HsSim.Canonical.State(clone), "seeded random continuation identical");
        Core2026Proof.Check(friendly.Damage == 0 && p.Opponent.Hero.Damage == 0, "friendly minions and enemy hero excluded");
        int damage = p.Hero.Damage; Play(g, p, id);
        Core2026Proof.Check(p.Hero.Damage == damage && dormant.Damage == 0, "empty eligible board does no damage or healing");
        var golem = Summon(p.Opponent, "EX1_556"); golem.Damage = 2;
        Play(g, p, id);
        Core2026Proof.Check(golem.Zone.Type == Zone.GRAVEYARD && p.Opponent.BoardZone.Count == 1, "deathrattle summons available to later missiles");
        dormant[GameTag.DORMANT] = 0;
        ClearBoard(g, p); ClearBoard(g, p.Opponent);
        enemy = Summon(p.Opponent, "CS2_182"); enemy.BaseHealth = 60;
        var velen = Summon(p, "EX1_350"); Play(g, p, id);
        Core2026Proof.Check(enemy.Damage == 8, "Velen doubles four missiles to eight");
        Summon(p, "EX1_012"); Play(g, p, id);
        Core2026Proof.Check(enemy.Damage == 18, "Velen doubles base plus Spell Damage to ten missiles");
        Summon(p, "EX1_350"); Play(g, p, id);
        Core2026Proof.Check(enemy.Damage == 38, "two Velens quadruple base plus Spell Damage to twenty missiles");
        velen.Silence(); g.DeathProcessingAndAuraUpdate(); Play(g, p, id);
        Core2026Proof.Check(enemy.Damage == 48, "silenced Velen no longer contributes to missile multiplier");
    });
}
