using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class PurifyingBreathProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_303", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_182"); var friend = Summon(p, "CS2_182");
        TargetRules(g, p, id, enemy, friend, false); p.Opponent.Hero.Damage = 15;
        Play(g, p, id, enemy); Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD && p.Opponent.Hero.Damage == 10, "lethal five damage heals enemy hero by five");
        friend.BaseHealth = 8; Play(g, p, id, friend);
        Core2026Proof.Check(friend.Health == 3 && p.Opponent.Hero.Damage == 10, "survivor does not heal");
        var shield = Summon(p.Opponent, "EX1_008"); shield.HasDivineShield = true; Play(g, p, id, shield);
        Core2026Proof.Check(shield.Health == 1 && !shield.HasDivineShield && p.Opponent.Hero.Damage == 10, "shield prevents kill and heal");
        shield.IsImmune = true;
        var source = Add(p, id); source.Card.Power.PowerTask.Process(g, p, source, shield, new TaskStack());
        Core2026Proof.Check(shield.Health == 1 && p.Opponent.Hero.Damage == 10, "Immune target prevents kill and heal");
        shield.IsImmune = false; Summon(p, "CS2_052"); var six = Summon(p.Opponent, "CS2_182"); six.BaseHealth = 6;
        Play(g, p, id, six); Core2026Proof.Check(six.Zone.Type == Zone.GRAVEYARD && p.Opponent.Hero.Damage == 5, "spell damage affects damage but not healing");
        Summon(p, "EX1_591"); Play(g, p, id, shield);
        Core2026Proof.Check(shield.Zone.Type == Zone.GRAVEYARD && p.Opponent.Hero.Damage == 10, "conditional healing obeys conversion");
    });
}
